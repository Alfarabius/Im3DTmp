using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(PlayerBrain))]
public class PlayerActiveState : MonoBehaviour, ICharacterState
{
    [Header("Movement Settings")]
    [SerializeField] private float walkSpeed = 3f;
    [SerializeField] private float runSpeed = 6f;
    [SerializeField] private float crouchSpeed = 1.5f;
    [SerializeField] private float woundedSpeed = 2f;
    [SerializeField] private float movementSmoothTime = 0.1f;
    [SerializeField] private float gravity = -15f;

    [Header("Jump & Physics Settings")]
    [SerializeField] private float jumpForce = 5f;
    [SerializeField] private float jumpStaminaCost = 10f;
    [SerializeField] private float jumpBufferTime = 0.15f;
    [SerializeField] private float coyoteTime = 0.15f;
    [SerializeField] private LayerMask environmentMask;
    
    [Header("Collision Checks")]
    [SerializeField] private float ceilingCheckRadius = 0.3f;

    [Header("Crouch Settings")]
    [SerializeField] private float crouchSmoothTime = 0.1f;

    [Header("Stealth Settings")]
    private float _defaultTargetWeight;
    [SerializeField] private float crouchedTargetWeight = 1f;

    [Header("Camera & Look Settings")]
    [SerializeField] private float mouseSensitivity = 0.1f;
    [SerializeField] private float maxLookAngle = 80f;
    [SerializeField] private float camHeightSmoothTime = 0.1f;
    [SerializeField] private float leanAngle = 15f;
    [SerializeField] private float leanOffset = 0.3f;
    [SerializeField] private float leanSmoothTime = 0.1f;

    [Header("Stamina Settings")]
    [SerializeField] private float staminaDrain = 15f;
    [SerializeField] private float staminaRegen = 10f;
    [SerializeField] private float exhaustionThreshold = 15f;

    [Header("Inputs")]
    [SerializeField] private InputActionReference moveAction;
    [SerializeField] private InputActionReference lookAction;
    [SerializeField] private InputActionReference sprintAction;
    [SerializeField] private InputActionReference crouchAction;
    [SerializeField] private InputActionReference jumpAction;
    [SerializeField] private InputActionReference leanLeftAction;
    [SerializeField] private InputActionReference leanRightAction;

    private PlayerBrain _brain;
    private Vector3 _velocity;
    private Vector2 _currentInputVector;
    private Vector2 _smoothInputVelocity;
    
    private float _pitch;
    private float _currentLean;
    private float _leanVelocity;
    private float _currentCamHeight;
    private float _camHeightVelocity;
    private Vector3 _originalCamLocalPos;
    private Quaternion _originalCamLocalRot;
    
    private float _currentColliderHeight;
    private float _colliderHeightVelocity;

    private bool _isCrouching;
    private bool _isExhausted;
    private float _jumpBufferCounter;
    private float _coyoteTimeCounter;

    // Динамические высоты для камеры и коллайдера
    private float _standingHeight;
    private float _crouchingHeight;
    private float _camStandingHeight;
    private float _camCrouchingHeight;

    private void Awake()
    {
        _brain = GetComponent<PlayerBrain>();
        
        // Читаем начальную высоту коллайдера из инспектора и вычисляем высоту для приседа
        _standingHeight = _brain.Controller.height;
        _crouchingHeight = _standingHeight / 2f; 
        _currentColliderHeight = _standingHeight;
    }

    public void Enter()
    {
        _defaultTargetWeight = _brain.TargetWeight;
        
        if (_brain.BodycamTarget != null)
        {
            _originalCamLocalPos = _brain.BodycamTarget.localPosition;
            _originalCamLocalRot = _brain.BodycamTarget.localRotation;
            
            // Высчитываем высоту камеры и ее позицию в приседе относительно стартового Transform'а
            _camStandingHeight = _originalCamLocalPos.y;
            _camCrouchingHeight = _camStandingHeight - (_standingHeight - _crouchingHeight); 
            _currentCamHeight = _camStandingHeight;
        }
    }

    public void UpdateState()
    {
        CheckGrounded();
        HandleHeadBump();
        HandleCameraAndLook();
        HandleCrouch();
        CalculateFinalMovement();
    }

    public void Exit()
    {
        _brain.Anim.SetFloat("Speed", 0f);
    }

    private void CheckGrounded()
    {
        // Используем встроенную проверку CharacterController.
        // Задаем Coyote Time для возможности прыгнуть чуть позже схода с края.
        if (_brain.Controller.isGrounded)
        {
            _coyoteTimeCounter = coyoteTime; 
            if (_velocity.y < 0) _velocity.y = -2f; 
        }
        else
        {
            _coyoteTimeCounter -= Time.deltaTime;
        }
    }

    private void HandleHeadBump()
    {
        // Если игрок летит вверх и ударяется макушкой о препятствие, мгновенно гасим вертикальную скорость, чтобы он не "прилипал" к потолку.
        if ((_brain.Controller.collisionFlags & CollisionFlags.Above) != 0 && _velocity.y > 0)
        {
            _velocity.y = 0f;
        }
    }

    private void HandleCameraAndLook()
    {
        if (_brain.BodycamTarget == null) return;

        // Вращение персонажа по оси Y и камеры по оси X (с ограничением угла)
        Vector2 lookInput = lookAction.action.ReadValue<Vector2>() * mouseSensitivity;
        transform.Rotate(Vector3.up * lookInput.x);
        _pitch -= lookInput.y;
        _pitch = Mathf.Clamp(_pitch, -maxLookAngle, maxLookAngle);

        // Расчет целевого угла наклона (Q/E) с плавным сглаживанием через SmoothDamp
        float targetLean = 0f;
        if (leanLeftAction.action.IsPressed()) targetLean = 1f;
        else if (leanRightAction.action.IsPressed()) targetLean = -1f;

        _currentLean = Mathf.SmoothDamp(_currentLean, targetLean, ref _leanVelocity, leanSmoothTime);

        // Плавное изменение высоты камеры при приседе и вставании
        float targetCamHeight = _isCrouching ? _camCrouchingHeight : _camStandingHeight;
        _currentCamHeight = Mathf.SmoothDamp(_currentCamHeight, targetCamHeight, ref _camHeightVelocity, camHeightSmoothTime);

        // Комбинируем изначальную ротацию камеры, поворот мышью и наклон
        Quaternion pitchRotation = Quaternion.Euler(_pitch, 0f, 0f);
        Quaternion leanRotation = Quaternion.Euler(0f, 0f, _currentLean * leanAngle);
        _brain.BodycamTarget.localRotation = _originalCamLocalRot * pitchRotation * leanRotation;

        // Применяем финальную высоту и смещение по X из-за наклона
        Vector3 targetLocalPos = new Vector3(_originalCamLocalPos.x + (_currentLean * -leanOffset), _currentCamHeight, _originalCamLocalPos.z);
        _brain.BodycamTarget.localPosition = targetLocalPos;
    }

    private void HandleCrouch()
    {
        bool wantsToCrouch = crouchAction.action.IsPressed();

        // Заморозка состояния приседа в воздухе для предотвращения бага с переключением размера коллайдера во время полета
        if (!_brain.Controller.isGrounded)
        {
            wantsToCrouch = _isCrouching; 
        }

        // Если игрок отпустил кнопку приседа, но над головой потолок — принудительно оставляем его сидеть
        if (!wantsToCrouch && _isCrouching && !CanStandUp())
        {
            wantsToCrouch = true;
        }
        
        // --- ДОБАВЛЕНО: Меняем вес только если состояние приседа изменилось ---
        if (wantsToCrouch != _isCrouching)
        {
            _brain.TargetWeight = wantsToCrouch ? crouchedTargetWeight : _defaultTargetWeight;
        }

        _isCrouching = wantsToCrouch;
        _brain.Anim.SetBool("IsCrouching", _isCrouching);
        
        // Плавное изменение физических размеров коллайдера
        float targetColliderHeight = _isCrouching ? _crouchingHeight : _standingHeight;
        _currentColliderHeight = Mathf.SmoothDamp(_currentColliderHeight, targetColliderHeight, ref _colliderHeightVelocity, crouchSmoothTime);
        
        _brain.Controller.height = _currentColliderHeight;
        _brain.Controller.center = new Vector3(0, _currentColliderHeight / 2f, 0);
    }

    private bool CanStandUp()
    {
        // Пускаем сферу от текущего центра коллайдера вверх, чтобы проверить, достаточно ли места для полного роста
        float radius = _brain.Controller.radius;
        Vector3 rayStart = transform.position + Vector3.up * (_currentColliderHeight / 2f);
        float distance = _standingHeight - _currentColliderHeight;
        
        return !Physics.SphereCast(rayStart, radius, Vector3.up, out _, distance, environmentMask);
    }

    private void CalculateFinalMovement()
    {
        Vector2 targetInput = moveAction.action.ReadValue<Vector2>();
        
        // Сглаживание сырого инпута для создания инерции при ходьбе и остановке
        _currentInputVector = Vector2.SmoothDamp(_currentInputVector, targetInput, ref _smoothInputVelocity, movementSmoothTime);
        Vector3 move = transform.right * _currentInputVector.x + transform.forward * _currentInputVector.y;
        
        // Изоляция спринта: бег не может быть активирован в воздухе
        bool isSprintPressed = sprintAction.action.IsPressed() && _brain.Controller.isGrounded;
        bool isWounded = _brain.Condition == TargetCondition.Wounded;
        float currentSpeed = walkSpeed;

        // Отслеживание полного истощения: пока стамина не восстановится до порога, бежать нельзя
        if (_brain.CurrentStamina <= 0f)
        {
            _isExhausted = true;
            _brain.CurrentStamina = 0f;
        }
        else if (_brain.CurrentStamina >= exhaustionThreshold)
        {
            _isExhausted = false;
        }

        // Выбор итоговой скорости движения и трата/регенерация выносливости
        if (isWounded)
        {
            currentSpeed = woundedSpeed;
        }
        else if (_isCrouching)
        {
            currentSpeed = crouchSpeed;
            RegenStamina();
        }
        else if (isSprintPressed && targetInput.magnitude > 0.1f)
        {
            if (!_isExhausted)
            {
                currentSpeed = runSpeed;
                _brain.CurrentStamina -= staminaDrain * Time.deltaTime;
            }
            else
            {
                currentSpeed = walkSpeed; 
            }
        }
        else
        {
            RegenStamina();
        }

        // Буферизация прыжка: запоминаем нажатие пробела на короткое время, чтобы прыжок сработал при касании земли
        if (jumpAction.action.WasPressedThisFrame())
        {
            _jumpBufferCounter = jumpBufferTime;
        }
        else
        {
            _jumpBufferCounter -= Time.deltaTime;
        }

        // Выполнение прыжка: проверяем таймеры, состояние ранения, стамину и свободное место над головой (если сидим)
        if (_jumpBufferCounter > 0f && _coyoteTimeCounter > 0f && !isWounded && _brain.CurrentStamina >= jumpStaminaCost)
        {
            if (_isCrouching && !CanStandUp()) return;

            _velocity.y = Mathf.Sqrt(jumpForce * -2f * gravity);
            _brain.CurrentStamina -= jumpStaminaCost;
            
            _jumpBufferCounter = 0f;
            _coyoteTimeCounter = 0f;
            
            if (_brain.CurrentStamina <= 0f) _isExhausted = true;
        }

        _velocity.y += gravity * Time.deltaTime;

        // Собираем горизонтальное и вертикальное движение в один вектор и один раз вызываем Move
        Vector3 finalMotion = (move * currentSpeed) + _velocity;
        _brain.Controller.Move(finalMotion * Time.deltaTime);

        // Передача скорости в аниматор обнуляется в воздухе во избежание проигрывания анимаций шагов/бега во время прыжка
        Vector3 horizontalVelocity = new Vector3(_brain.Controller.velocity.x, 0, _brain.Controller.velocity.z);
        _brain.Anim.SetFloat("Speed", _brain.Controller.isGrounded ? horizontalVelocity.magnitude : 0f);
    }

    private void RegenStamina()
    {
        // Восстановление стамины в спокойном состоянии или при ходьбе
        if (_brain.CurrentStamina < _brain.MaxStamina)
        {
            _brain.CurrentStamina += staminaRegen * Time.deltaTime;
            _brain.CurrentStamina = Mathf.Clamp(_brain.CurrentStamina, 0f, _brain.MaxStamina);
        }
    }
}

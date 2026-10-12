using UnityEngine;

[DefaultExecutionOrder(-10)]
public sealed class ImmersiveSimFirstPersonController : MonoBehaviour
{
    [Header("Scene References")]
    [SerializeField] private CharacterController controller;
    [SerializeField] private Transform cameraPivot;
    [SerializeField] private Camera playerCamera;
    [SerializeField] private Transform bodyMesh;

    [Header("Collision Masks")]
    [SerializeField] private LayerMask collisionMask = ~0;
    [SerializeField] private LayerMask leanCollisionMask = ~0;

    [Header("Look")]
    [SerializeField] private float mouseSensitivity = 2.2f;
    [SerializeField] private float pitchMin = -80f;
    [SerializeField] private float pitchMax = 80f;
    [SerializeField] private float lookSmoothTime = 0.035f;

    [Header("Move Speeds")]
    [SerializeField] private float walkSpeed = 2.1f;
    [SerializeField] private float runSpeed = 4.0f;
    [SerializeField] private float crouchSpeed = 1.35f;
    [SerializeField] private float acceleration = 16f;
    [SerializeField] private float airAcceleration = 6f;

    [Header("Jump & Gravity")]
    [SerializeField] private float gravity = 22f;
    [SerializeField] private float jumpHeight = 1.05f;
    [SerializeField] private float groundedStickForce = 2.5f;
    [SerializeField] private float coyoteTime = 0.08f;
    [SerializeField] private float jumpBufferTime = 0.1f;

    [Header("Ground Probe")]
    [SerializeField] private float groundProbeExtraDistance = 0.22f;
    [SerializeField] private float groundedMinTime = 0.06f;

    [Header("Crouch")]
    [SerializeField] private bool crouchToggle = true;
    [SerializeField] private float standingHeight = 1.75f;
    [SerializeField] private float crouchHeight = 1.15f;
    [SerializeField] private float crouchSmoothTime = 0.09f;
    [SerializeField] private float cameraStandingLocalY = 0.72f;
    [SerializeField] private float cameraCrouchLocalY = 0.38f;
    [SerializeField] private float headClearancePadding = 0.03f;

    [Header("Lean")]
    [SerializeField] private KeyCode leanLeftKey = KeyCode.Q;
    [SerializeField] private KeyCode leanRightKey = KeyCode.E;
    [SerializeField] private float leanDistance = 0.28f;
    [SerializeField] private float leanAngle = 12f;
    [SerializeField] private float leanSmoothTime = 0.08f;
    [SerializeField] private float leanProbeRadius = 0.12f;

    [Header("Keys")]
    [SerializeField] private KeyCode runKey = KeyCode.LeftShift;
    [SerializeField] private KeyCode jumpKey = KeyCode.Space;
    [SerializeField] private KeyCode crouchKey = KeyCode.LeftControl;

    private float _yaw;
    private float _pitch;
    private float _yawVelocity;
    private float _pitchVelocity;

    private Vector3 _planarVelocity;
    private float _verticalVelocity;

    private bool _isGrounded;
    private float _groundedStableTimer;
    private float _timeSinceLastGrounded;

    private bool _wantsJump;
    private float _jumpBufferTimer;

    private bool _desiredCrouch;
    private bool _isCrouching;
    private float _heightVelocity;
    private float _currentHeight;

    private float _cameraYVelocity;
    private float _currentCameraLocalY;

    private float _leanValue;
    private float _leanVelocity;

    private Vector3 _cameraBaseLocalPos;

    private int _collisionMaskRuntime;
    private int _leanMaskRuntime;

    public void Awake()
    {
        if (controller == null) controller = GetComponent<CharacterController>();

        _collisionMaskRuntime = collisionMask.value & ~(1 << gameObject.layer);
        _leanMaskRuntime = leanCollisionMask.value & ~(1 << gameObject.layer);

        _yaw = transform.eulerAngles.y;
        _pitch = 0f;

        _cameraBaseLocalPos = playerCamera.transform.localPosition;

        _desiredCrouch = false;
        _isCrouching = false;

        ApplyStandingImmediate();
    }

    public void OnEnable()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    public void Update()
    {
        float dt = Time.deltaTime;

        UpdateLook(dt);
        UpdateInput(dt);
        UpdateGround(dt);
        UpdateCrouch(dt);
        UpdateMove(dt);
        UpdateLean(dt);
        ApplyVisuals(dt);
    }

    private void UpdateLook(float dt)
    {
        float mx = Input.GetAxisRaw("Mouse X") * mouseSensitivity;
        float my = Input.GetAxisRaw("Mouse Y") * mouseSensitivity;

        float targetYaw = _yaw + mx;
        float targetPitch = Mathf.Clamp(_pitch - my, pitchMin, pitchMax);

        _yaw = Mathf.SmoothDampAngle(_yaw, targetYaw, ref _yawVelocity, lookSmoothTime, Mathf.Infinity, dt);
        _pitch = Mathf.SmoothDampAngle(_pitch, targetPitch, ref _pitchVelocity, lookSmoothTime, Mathf.Infinity, dt);

        transform.rotation = Quaternion.Euler(0f, _yaw, 0f);
        cameraPivot.localRotation = Quaternion.Euler(_pitch, 0f, 0f);
    }

    private void UpdateInput(float dt)
    {
        if (Input.GetKeyDown(jumpKey))
        {
            _wantsJump = true;
            _jumpBufferTimer = jumpBufferTime;
        }

        if (_jumpBufferTimer > 0f) _jumpBufferTimer -= dt;
        if (_jumpBufferTimer <= 0f) _wantsJump = false;

        if (crouchToggle)
        {
            if (Input.GetKeyDown(crouchKey)) _desiredCrouch = !_desiredCrouch;
        }
        else
        {
            _desiredCrouch = Input.GetKey(crouchKey);
        }
    }

    private void UpdateGround(float dt)
    {
        bool controllerGrounded = controller.isGrounded;
        bool probedGrounded = ProbeGround(out Vector3 groundNormal);
        bool groundedNow = controllerGrounded || probedGrounded;

        if (groundedNow)
        {
            _groundedStableTimer += dt;
            _timeSinceLastGrounded = 0f;

            if (_groundedStableTimer >= groundedMinTime) _isGrounded = true;
        }
        else
        {
            _groundedStableTimer = 0f;
            _timeSinceLastGrounded += dt;

            if (_timeSinceLastGrounded > coyoteTime) _isGrounded = false;
        }

        if (_isGrounded && _verticalVelocity < 0f) _verticalVelocity = -groundedStickForce;
    }

    private void UpdateCrouch(float dt)
    {
        float targetHeight = _currentHeight;

        if (_desiredCrouch)
        {
            targetHeight = crouchHeight;
        }
        else
        {
            if (CanStandUp())
            {
                targetHeight = standingHeight;
            }
            else
            {
                targetHeight = Mathf.Min(_currentHeight, standingHeight);
            }
        }

        _currentHeight = Mathf.SmoothDamp(_currentHeight, targetHeight, ref _heightVelocity, crouchSmoothTime, Mathf.Infinity, dt);

        float t = Mathf.InverseLerp(crouchHeight, standingHeight, _currentHeight);
        float targetCameraY = Mathf.Lerp(cameraCrouchLocalY, cameraStandingLocalY, t);
        _currentCameraLocalY = Mathf.SmoothDamp(_currentCameraLocalY, targetCameraY, ref _cameraYVelocity, crouchSmoothTime, Mathf.Infinity, dt);

        controller.height = _currentHeight;

        Vector3 c = controller.center;
        c.y = _currentHeight * 0.5f;
        controller.center = c;

        Vector3 pivotLocal = cameraPivot.localPosition;
        pivotLocal.y = _currentCameraLocalY;
        cameraPivot.localPosition = pivotLocal;

        _isCrouching = _currentHeight < (standingHeight - 0.05f);

        if (!_desiredCrouch && !_isCrouching && !CanStandUp()) _desiredCrouch = true;
        if (_desiredCrouch && CanStandUp() && !Input.GetKey(crouchKey) && !crouchToggle) _desiredCrouch = false;
    }

    private void UpdateMove(float dt)
    {
        float ix = Input.GetAxisRaw("Horizontal");
        float iz = Input.GetAxisRaw("Vertical");
        Vector2 input = new Vector2(ix, iz);
        if (input.sqrMagnitude > 1f) input.Normalize();

        bool wantsRun = Input.GetKey(runKey) && !_isCrouching && input.y > 0.1f;
        float speed = _isCrouching ? crouchSpeed : (wantsRun ? runSpeed : walkSpeed);

        Vector3 wishLocal = new Vector3(input.x, 0f, input.y);
        Vector3 wishWorld = transform.TransformDirection(wishLocal);
        Vector3 wishPlanar = wishWorld * speed;

        float accel = _isGrounded ? acceleration : airAcceleration;
        _planarVelocity = Vector3.MoveTowards(_planarVelocity, wishPlanar, accel * dt);

        if (_isGrounded && _wantsJump)
        {
            _wantsJump = false;

            float jumpVelocity = Mathf.Sqrt(Mathf.Max(0f, 2f * gravity * jumpHeight));
            _verticalVelocity = jumpVelocity;
            _isGrounded = false;
            _timeSinceLastGrounded = coyoteTime + 0.001f;
            _groundedStableTimer = 0f;
        }

        _verticalVelocity -= gravity * dt;

        Vector3 motion = (_planarVelocity + Vector3.up * _verticalVelocity) * dt;
        CollisionFlags flags = controller.Move(motion);

        if ((flags & CollisionFlags.Above) != 0 && _verticalVelocity > 0f) _verticalVelocity = 0f;
    }

    private void UpdateLean(float dt)
    {
        float target = 0f;
        if (Input.GetKey(leanLeftKey)) target -= 1f;
        if (Input.GetKey(leanRightKey)) target += 1f;

        _leanValue = Mathf.SmoothDamp(_leanValue, target, ref _leanVelocity, leanSmoothTime, Mathf.Infinity, dt);
    }

    private void ApplyVisuals(float dt)
    {
        if (bodyMesh != null)
        {
            Quaternion target = Quaternion.Euler(0f, _yaw, 0f);
            bodyMesh.rotation = Quaternion.Slerp(bodyMesh.rotation, target, 1f - Mathf.Exp(-18f * dt));
        }

        Vector3 desiredLocal = _cameraBaseLocalPos + new Vector3(_leanValue * leanDistance, 0f, 0f);
        float desiredRoll = -_leanValue * leanAngle;

        Vector3 pivotWorld = cameraPivot.position;
        Vector3 desiredWorld = cameraPivot.TransformPoint(desiredLocal);

        Vector3 clampedWorld = ClampCameraToWalls(pivotWorld, desiredWorld);

        playerCamera.transform.position = clampedWorld;

        Quaternion baseRot = cameraPivot.rotation;
        Quaternion rollRot = Quaternion.AngleAxis(desiredRoll, cameraPivot.forward);
        playerCamera.transform.rotation = rollRot * baseRot;
    }

    private Vector3 ClampCameraToWalls(Vector3 pivotWorld, Vector3 desiredWorld)
    {
        Vector3 dir = desiredWorld - pivotWorld;
        float dist = dir.magnitude;
        if (dist <= 0.0001f) return desiredWorld;

        dir /= dist;

        bool hit = Physics.SphereCast(
            pivotWorld,
            leanProbeRadius,
            dir,
            out RaycastHit hitInfo,
            dist,
            _leanMaskRuntime,
            QueryTriggerInteraction.Ignore
        );

        if (!hit) return desiredWorld;

        float safeDist = Mathf.Max(0f, hitInfo.distance - 0.01f);
        return pivotWorld + dir * safeDist;
    }

    private bool ProbeGround(out Vector3 groundNormal)
    {
        groundNormal = Vector3.up;

        float radius = Mathf.Max(0.01f, controller.radius - 0.02f);

        Vector3 centerWorld = transform.TransformPoint(controller.center);
        float castStartUp = (controller.height * 0.5f) - radius + 0.01f;

        Vector3 origin = centerWorld + Vector3.up * castStartUp;
        float castDistance = castStartUp + groundProbeExtraDistance;

        bool hit = Physics.SphereCast(
            origin,
            radius,
            Vector3.down,
            out RaycastHit hitInfo,
            castDistance,
            _collisionMaskRuntime,
            QueryTriggerInteraction.Ignore
        );

        if (!hit) return false;

        groundNormal = hitInfo.normal;

        float maxSlope = controller.slopeLimit + 0.5f;
        float slopeAngle = Vector3.Angle(hitInfo.normal, Vector3.up);
        if (slopeAngle > maxSlope) return false;

        float bottomY = centerWorld.y - (controller.height * 0.5f) + radius;
        float groundY = hitInfo.point.y;

        float separation = bottomY - groundY;
        return separation <= groundProbeExtraDistance + 0.03f;
    }

    private bool CanStandUp()
    {
        float radius = Mathf.Max(0.01f, controller.radius - 0.02f);

        Vector3 centerWorld = transform.TransformPoint(controller.center);

        Vector3 bottomSphereCenter = centerWorld - Vector3.up * (controller.height * 0.5f - radius);
        Vector3 topSphereCenterStanding = bottomSphereCenter + Vector3.up * (standingHeight - 2f * radius);

        float extra = headClearancePadding;
        bottomSphereCenter.y += extra;
        topSphereCenterStanding.y -= extra;

        bool blocked = Physics.CheckCapsule(
            bottomSphereCenter,
            topSphereCenterStanding,
            radius,
            _collisionMaskRuntime,
            QueryTriggerInteraction.Ignore
        );

        return !blocked;
    }

    private void ApplyStandingImmediate()
    {
        _currentHeight = standingHeight;
        _currentCameraLocalY = cameraStandingLocalY;

        controller.height = standingHeight;

        Vector3 c = controller.center;
        c.y = standingHeight * 0.5f;
        controller.center = c;

        Vector3 pivotLocal = cameraPivot.localPosition;
        pivotLocal.y = cameraStandingLocalY;
        cameraPivot.localPosition = pivotLocal;
    }

    public bool IsGrounded()
    {
        return _isGrounded;
    }

    public bool IsCrouching()
    {
        return _isCrouching;
    }

    public Vector3 GetPlanarVelocity()
    {
        return _planarVelocity;
    }
}


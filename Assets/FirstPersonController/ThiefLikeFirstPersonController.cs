using UnityEngine;

[RequireComponent(typeof(Rigidbody), typeof(CapsuleCollider))]
public sealed class ThiefLikeFirstPersonController : MonoBehaviour
{
    [SerializeField] private Camera playerCamera;
    [SerializeField] private Transform cameraPivot;

    [SerializeField] private float mouseSensitivity = 2.2f;
    [SerializeField] private float maxPitch = 80f;

    [SerializeField] private float walkSpeed = 4.5f;
    [SerializeField] private float sprintSpeed = 6.5f;
    [SerializeField] private float crouchSpeed = 2.2f;

    [SerializeField] private float jumpForce = 5.5f;

    [SerializeField] private bool holdToCrouch = true;
    [SerializeField] private float crouchHeight = 1.1f;
    [SerializeField] private float crouchSmooth = 0.08f;

    [SerializeField] private float leanAngle = 15f;
    [SerializeField] private float leanOffset = 0.6f;
    [SerializeField] private float leanSmooth = 0.08f;

    private Rigidbody _rb;
    private CapsuleCollider _capsule;

    private float _yaw;
    private float _pitch;

    private bool _grounded;
    private bool _groundedFrame;
    private bool _crouched;
    private bool _crouchToggle;

    private float _cameraYVel;
    private float _lean;
    private float _leanVel;

    private float _standingHeight;

    public void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _capsule = GetComponent<CapsuleCollider>();

        _rb.freezeRotation = true;
        _rb.interpolation = RigidbodyInterpolation.Interpolate;
        _rb.collisionDetectionMode = CollisionDetectionMode.Discrete;

        _standingHeight = _capsule.height;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    public void Update()
    {
        HandleLook();
        HandleCrouchInput();
        HandleLean();
        UpdateCameraAndCollider();
    }

    public void FixedUpdate()
    {
        _grounded = _groundedFrame;
        _groundedFrame = false;

        if (_grounded && _rb.linearVelocity.y < 0f)
        {
            Vector3 v = _rb.linearVelocity;
            v.y = 0f;
            _rb.linearVelocity = v;
        }

        HandleMovement();
        HandleJump();
    }

    public void OnCollisionStay(Collision collision)
    {
        for (int i = 0; i < collision.contactCount; i++)
        {
            if (collision.contacts[i].normal.y > 0.6f)
            {
                _groundedFrame = true;
                break;
            }
        }
    }

    public void OnCollisionExit(Collision collision)
    {
        _groundedFrame = false;
    }

    private void HandleLook()
    {
        _yaw += Input.GetAxis("Mouse X") * mouseSensitivity;
        _pitch -= Input.GetAxis("Mouse Y") * mouseSensitivity;
        _pitch = Mathf.Clamp(_pitch, -maxPitch, maxPitch);

        transform.rotation = Quaternion.Euler(0f, _yaw, 0f);
        playerCamera.transform.localRotation = Quaternion.Euler(_pitch, 0f, -_lean * leanAngle);
    }

    private void HandleMovement()
    {
        Vector3 input =
            transform.forward * Input.GetAxisRaw("Vertical") +
            transform.right * Input.GetAxisRaw("Horizontal");

        if (input.sqrMagnitude > 1f)
        {
            input.Normalize();
        }

        float speed = _crouched ? crouchSpeed :
            Input.GetKey(KeyCode.LeftShift) ? sprintSpeed : walkSpeed;

        Vector3 target = input * speed;
        Vector3 velocity = _rb.linearVelocity;
        Vector3 change = target - new Vector3(velocity.x, 0f, velocity.z);

        _rb.AddForce(change, ForceMode.VelocityChange);
    }

    private void HandleJump()
    {
        if (!_grounded)
        {
            return;
        }

        if (Input.GetKeyDown(KeyCode.Space))
        {
            Vector3 v = _rb.linearVelocity;
            v.y = 0f;
            _rb.linearVelocity = v;
            _rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
            _grounded = false;
        }
    }

    private void HandleCrouchInput()
    {
        if (holdToCrouch)
        {
            _crouched = Input.GetKey(KeyCode.LeftControl);
        }
        else if (Input.GetKeyDown(KeyCode.LeftControl))
        {
            _crouchToggle = !_crouchToggle;
            _crouched = _crouchToggle;
        }
    }

    private void UpdateCameraAndCollider()
    {
        float targetHeight = _crouched ? crouchHeight : _standingHeight;
        _capsule.height = Mathf.Lerp(_capsule.height, targetHeight, Time.deltaTime / crouchSmooth);
        _capsule.center = new Vector3(0f, _capsule.height * 0.5f, 0f);

        Vector3 camPos = cameraPivot.localPosition;
        camPos.y = Mathf.SmoothDamp(
            camPos.y,
            _capsule.height - 0.1f,
            ref _cameraYVel,
            crouchSmooth);

        float leanTarget = 0f;
        if (Input.GetKey(KeyCode.Q)) leanTarget = -1f;
        if (Input.GetKey(KeyCode.E)) leanTarget = 1f;

        _lean = Mathf.SmoothDamp(_lean, leanTarget, ref _leanVel, leanSmooth);
        camPos.x = _lean * leanOffset;

        cameraPivot.localPosition = camPos;
    }

    private void HandleLean()
    {
    }
}


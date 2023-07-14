using Cinemachine;
using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class FirstPersonController : MonoBehaviour
{
    [Header("Player")]
    [Tooltip("Move speed of the character in m/s")]
    public float MoveSpeed = 4.0f;
    [Tooltip("Sprint speed of the character in m/s")]
    public float SprintSpeed = 6.0f;
    [Tooltip("Rotation speed of the character")]
    public float RotationSpeed = 1.0f;
    [Tooltip("Acceleration and deceleration")]
    public float SpeedChangeRate = 10.0f;

    [Space(10)]
    [Tooltip("The height the player can jump")]
    public float JumpHeight = 1.2f;
    [Tooltip("The character uses its own gravity value. The engine default is -9.81f")]
    public float Gravity = -15.0f;

    [Space(10)]
    [Tooltip("Time required to pass before being able to jump again. Set to 0f to instantly jump again")]
    public float JumpTimeout = 0.1f;
    [Tooltip("Time required to pass before entering the fall state. Useful for walking down stairs")]
    public float FallTimeout = 0.15f;

    [Header("Player Grounded")]
    [Tooltip("If the character is grounded or not. Not part of the CharacterController built in grounded check")]
    public bool Grounded = true;
    [Tooltip("Useful for rough ground")]
    public float GroundedOffset = -0.14f;
    [Tooltip("The radius of the grounded check. Should match the radius of the CharacterController")]
    public float GroundedRadius = 0.5f;
    [Tooltip("What layers the character uses as ground")]
    public LayerMask GroundLayers;

    [Header("Cinemachine")]
    [Tooltip("The follow target set in the Cinemachine Virtual Camera that the camera will follow")]
    public GameObject CinemachineCameraTarget;
    [Tooltip("How far in degrees can you move the camera up")]
    public float TopClamp = 90.0f;
    [Tooltip("How far in degrees can you move the camera down")]
    public float BottomClamp = -90.0f;

    // cinemachine
    private float _cinemachineTargetPitch;
    [SerializeField] private CinemachineVirtualCamera _virtualCamera; // Ссылка на компонент Cinemachine VirtualCamera


    // player
    private float _speed;
    private float _rotationVelocity;
    private float _verticalVelocity;
    private float _terminalVelocity = 53.0f;

    // timeout deltatime
    private float _jumpTimeoutDelta;
    private float _fallTimeoutDelta;

    private CharacterController _controller;
    private GameObject _mainCamera;

    private const float _threshold = 0.01f;

    public void OnValidate()
    {

    }

    private void Awake()
    {
        // get a reference to our main camera
        if (_mainCamera == null)
        {
            _mainCamera = GameObject.FindGameObjectWithTag("MainCamera");
        }
        if (_virtualCamera == null)
        {
            // Получаем компонент Cinemachine VirtualCamera из сцены
            _virtualCamera = GameObject.FindGameObjectWithTag("VirtualCamera").GetComponent<CinemachineVirtualCamera>();
        }
    }

    private void Start()
    {
        _controller = GetComponent<CharacterController>();

        // reset our timeouts on start
        _jumpTimeoutDelta = JumpTimeout;
        _fallTimeoutDelta = FallTimeout;
    }

    private void Update()
    {
        GroundCheck();
        Move();
        JumpAndGravity();
    }

    private void LateUpdate()
    {
        CameraRotation();
    }

    private void GroundCheck()
    {
        // set sphere position, with offset
        Vector3 spherePosition = new Vector3(transform.position.x, transform.position.y - GroundedOffset, transform.position.z);
        Grounded = Physics.CheckSphere(spherePosition, GroundedRadius, GroundLayers, QueryTriggerInteraction.Ignore);
    }

    private void CameraRotation()
    {
        float mouseX = Input.GetAxis("Mouse X");
        float mouseY = Input.GetAxis("Mouse Y");

        _cinemachineTargetPitch += -mouseY * RotationSpeed * Time.deltaTime;
        _rotationVelocity = mouseX * RotationSpeed * Time.deltaTime;

        // clamp our pitch rotation
        _cinemachineTargetPitch = ClampAngle(_cinemachineTargetPitch, BottomClamp, TopClamp);

        // Update Cinemachine camera target pitch
        CinemachineCameraTarget.transform.localRotation = Quaternion.Euler(_cinemachineTargetPitch, 0.0f, 0.0f);

        // rotate the player left and right
        transform.Rotate(Vector3.up * _rotationVelocity);
    }
    private static float ClampAngle(float lfAngle, float lfMin, float lfMax)
    {
        if (lfAngle < -360f) lfAngle += 360f;
        if (lfAngle > 360f) lfAngle -= 360f;
        return Mathf.Clamp(lfAngle, lfMin, lfMax);
    }

    private void OnDrawGizmosSelected()
    {
        Color transparentGreen = new Color(0.0f, 1.0f, 0.0f, 0.35f);
        Color transparentRed = new Color(1.0f, 0.0f, 0.0f, 0.35f);

        if (Grounded) Gizmos.color = transparentGreen;
        else Gizmos.color = transparentRed;

        // when selected, draw a gizmo in the position of, and matching radius of, the grounded collider
        Gizmos.DrawSphere(new Vector3(transform.position.x, transform.position.y - GroundedOffset, transform.position.z), GroundedRadius);
    }

    private void Move()
    {
        var movement = new Vector3(Input.GetAxisRaw("Horizontal"), 0, Input.GetAxisRaw("Vertical"));
        // set target speed based on move speed, sprint speed and if sprint is pressed
        float targetSpeed = Input.GetKey(KeyCode.LeftShift) ? SprintSpeed : MoveSpeed;

        if (movement.magnitude > Mathf.Abs(0.05f))
        {
            float rotationAngle = Mathf.Atan2(movement.x, movement.z) * Mathf.Rad2Deg + _mainCamera.transform.eulerAngles.y;
            float angle = Mathf.SmoothDampAngle(transform.eulerAngles.y, rotationAngle, ref _rotationVelocity, RotationSpeed);
            transform.rotation = Quaternion.Euler(0f, angle, 0f);
            movement = Quaternion.Euler(0f, rotationAngle, 0f) * Vector3.forward;
        }

        // a simplistic acceleration and deceleration designed to be easy to remove, replace, or iterate upon

        // note: Vector2's == operator uses approximation so is not floating point error prone, and is cheaper than magnitude
        // if there is no input, set the target speed to 0
        if (movement.magnitude <= 0) targetSpeed = 0.0f;

        // a reference to the players current horizontal velocity
        float currentHorizontalSpeed = new Vector3(_controller.transform.position.y, 0.0f, _controller.transform.position.z).magnitude;

        float speedOffset = 0.1f;

        //accelerate or decelerate to target speed
        //if (currentHorizontalSpeed < targetSpeed - speedOffset || currentHorizontalSpeed > targetSpeed + speedOffset)
        //{
        //    creates curved result rather than a linear one giving a more organic speed change
        //     note T in Lerp is clamped, so we don't need to clamp our speed
        //    _speed = Mathf.Lerp(currentHorizontalSpeed, targetSpeed * 1, Time.deltaTime * SpeedChangeRate);

        //    round speed to 3 decimal places
        //    _speed = Mathf.Round(_speed * 1000f) / 1000f;
        //}
        //else
        //{
        _speed = targetSpeed;
        //}

        // move the player
        _controller.Move(movement.normalized * (_speed * Time.deltaTime) + new Vector3(0.0f, _verticalVelocity, 0.0f) * Time.deltaTime);
    }

    private void JumpAndGravity()
    {
        if (Grounded)
        {
            // reset the fall timeout timer
            _fallTimeoutDelta = FallTimeout;

            // stop our velocity dropping infinitely when grounded
            if (_verticalVelocity < 0.0f)
            {
                _verticalVelocity = -2f;
            }

            if (Input.GetKeyDown(KeyCode.Space))
            {
                // Jump
                if (_jumpTimeoutDelta <= 0.0f)
                {
                    // the square root of H * -2 * G = how much velocity needed to reach desired height
                    _verticalVelocity = Mathf.Sqrt(JumpHeight * -2f * Gravity);
                }
            }

            // jump timeout
            if (_jumpTimeoutDelta >= 0.0f)
            {
                _jumpTimeoutDelta -= Time.deltaTime;
            }
        }
        else
        {
            // reset the jump timeout timer
            _jumpTimeoutDelta = JumpTimeout;

            // fall timeout
            if (_fallTimeoutDelta >= 0.0f)
            {
                _fallTimeoutDelta -= Time.deltaTime;
            }

            // if we are not grounded, do not jump
            //_input.jump = false;
        }

        // apply gravity over time if under terminal (multiply by delta time twice to linearly speed up over time)
        if (_verticalVelocity < _terminalVelocity)
        {
            _verticalVelocity += Gravity * Time.deltaTime;
        }
    }

}

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
    [Tooltip("Высота прыжка")]
    public float JumpHeight = 1.2f;
    [Tooltip("Гравитация")]
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
    [SerializeField] private CinemachineVirtualCamera _virtualCamera;


    // player
    private float _speed;
    private float _rotationVelocity;
    private float _verticalVelocity;
    private float _terminalVelocity = 53.0f;
    private float _targetSpeed;

    private Vector3 _movement;
    private float deltaX;
    private float deltaY;

    // timeout deltatime
    private float _jumpTimeoutDelta;
    private float _fallTimeoutDelta;

    private CharacterController _controller;
    private GameObject _mainCamera;
    private Animator _animator;
    public Transform _headTarget;

    public void OnValidate()
    {

    }

    private void Awake()
    {
        if (_mainCamera == null)
        {
            _mainCamera = GameObject.FindGameObjectWithTag("MainCamera");
        }
        if (_virtualCamera == null)
        {
            _virtualCamera = GameObject.FindGameObjectWithTag("VirtualCamera").GetComponent<CinemachineVirtualCamera>();
        }
    }

    private void Start()
    {
        _controller = GetComponent<CharacterController>();
        _animator = GetComponent<Animator>();

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
        Animation();
    }

    private void GroundCheck()
    {
        Vector3 spherePosition = new Vector3(transform.position.x, transform.position.y - GroundedOffset, transform.position.z);
        Grounded = Physics.CheckSphere(spherePosition, GroundedRadius, GroundLayers, QueryTriggerInteraction.Ignore);
    }

    private void CameraRotation()
    {
        float mouseX = Input.GetAxis("Mouse X");
        float mouseY = Input.GetAxis("Mouse Y");

        _cinemachineTargetPitch += -mouseY * RotationSpeed * Time.deltaTime;
        _rotationVelocity += mouseX * RotationSpeed * Time.deltaTime;
        _cinemachineTargetPitch = ClampAngle(_cinemachineTargetPitch, BottomClamp, TopClamp);
        _rotationVelocity = ClampAngle(_rotationVelocity, -30, 30);



        CinemachineCameraTarget.transform.localRotation = Quaternion.Euler(_cinemachineTargetPitch, _rotationVelocity, 0.0f);


        if (ClampAngle(_rotationVelocity, -30, 30) <= -30 || ClampAngle(_rotationVelocity, -30, 30) >= 30 || _movement.magnitude > 0)
        {
            transform.Rotate(Vector3.up * mouseX * RotationSpeed * Time.deltaTime);
        }

        Ray desiredAimRay = _mainCamera.GetComponent<Camera>().ScreenPointToRay(new Vector3(Screen.width / 2, Screen.height / 2));
        Vector3 desiredPosition = desiredAimRay.origin + desiredAimRay.direction * 0.7f;
        _headTarget.position = desiredPosition;
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

        Gizmos.DrawSphere(new Vector3(transform.position.x, transform.position.y - GroundedOffset, transform.position.z), GroundedRadius);
    }

    private void Move()
    {
        deltaY = Input.GetAxisRaw("Horizontal");
        deltaX = Input.GetAxisRaw("Vertical");

        _movement = new Vector3(deltaY, 0, deltaX);
        _targetSpeed = Input.GetKey(KeyCode.LeftShift) ? SprintSpeed : MoveSpeed;

        if (_movement.magnitude > Mathf.Abs(0.05f))
        {
            float rotationAngle = Mathf.Atan2(_movement.x, _movement.z) * Mathf.Rad2Deg + _mainCamera.transform.eulerAngles.y;
            float angle = Mathf.SmoothDampAngle(transform.eulerAngles.y, rotationAngle, ref _rotationVelocity, RotationSpeed);
            transform.rotation = Quaternion.Euler(0f, angle, 0f);
            _movement = Quaternion.Euler(0f, rotationAngle, 0f) * Vector3.forward;
        }
        if (_movement.magnitude <= 0) _targetSpeed = 0.0f;

        
        _speed = _targetSpeed;
        _controller.Move(_movement.normalized * (_speed * Time.deltaTime) + new Vector3(0.0f, _verticalVelocity, 0.0f) * Time.deltaTime);
    }

    private void JumpAndGravity()
    {
        if (Grounded)
        {
            _fallTimeoutDelta = FallTimeout;

            if (_verticalVelocity < 0.0f)
            {
                _verticalVelocity = -2f;
            }

            if (Input.GetKeyDown(KeyCode.Space))
            {
                if (_jumpTimeoutDelta <= 0.0f)
                {
                    _verticalVelocity = Mathf.Sqrt(JumpHeight * -2f * Gravity);
                }
            }

            if (_jumpTimeoutDelta >= 0.0f)
            {
                _jumpTimeoutDelta -= Time.deltaTime;
            }
        }
        else
        {
            _jumpTimeoutDelta = JumpTimeout;

            if (_fallTimeoutDelta >= 0.0f)
            {
                _fallTimeoutDelta -= Time.deltaTime;
            }
        }
        if (_verticalVelocity < _terminalVelocity)
        {
            _verticalVelocity += Gravity * Time.deltaTime;
        }
    }

   private void Animation()
   {
        if (_targetSpeed == MoveSpeed)
        {
            _animator.SetBool("isWalk", true);
            if (deltaX > 0)
            {
                _animator.SetFloat("Walking X", 1);
                _animator.SetFloat("Walking", 0);
            }
            else if (deltaX < 0)
            {
                _animator.SetFloat("Walking X", 0);
                _animator.SetFloat("Walking", 0);
            }

            if (deltaY > 0)
            {
                _animator.SetFloat("Walking Y", 1);
                _animator.SetFloat("Walking", 1);
            }
            else if (deltaY < 0)
            {
                _animator.SetFloat("Walking Y", 0);
                _animator.SetFloat("Walking", 1);
            }
        }
        else if (_targetSpeed == SprintSpeed)
        {
            //_animator.SetBool("isSprint", true);
            if (deltaX > 0)
            {

            }
            else if (deltaX < 0)
            {

            }
            else if (_movement.magnitude == 0)
            {

            }
        }
        else if(_targetSpeed == 0 && _movement.magnitude == 0)
        {
            _animator.SetBool("isWalk", false);
            //_animator.SetBool("isSprint", false);
        }
    }

}

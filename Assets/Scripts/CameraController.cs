using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

public class CameraController : MonoBehaviour
{
    // 依存関係の設定
    [SerializeField] private CinemachineCamera virtualCamera;
    [SerializeField] private Transform followTarget;

    // カメラ移動関連の設定
    [SerializeField] private float followSpeed;

    // ズーム関連の設定
    [SerializeField] private float zoomSpeed;
    [SerializeField] private float zoomSmoothing;
    [SerializeField] private float minZoom;
    [SerializeField] private float maxZoom;

    // 入力の受け取り
    [SerializeField] private InputAction middleClickAction;
    [SerializeField] private InputAction mouseDeltaAction;
    [SerializeField] private InputAction scrollAction;
    [SerializeField] private InputAction switchCameraAngleAction;

    private Vector2 _lastMousePos;
    private float _targetFOV;
    private bool _isPanning;

    private bool _isCameraAngleSwitched;
    private Vector3 _defaultCameraAngle;
    [SerializeField] private Vector3 cameraAngle;
    [SerializeField] private float cameraAngleSmoothing;

    private void Start()
    {
        _targetFOV = virtualCamera.Lens.OrthographicSize;
        _defaultCameraAngle = transform.localRotation.eulerAngles;
    }

    private void Update()
    {
        HandlePan();
        SmoothingUpdate();
    }

    private void OnEnable()
    {
        middleClickAction.Enable();
        mouseDeltaAction.Enable();
        scrollAction.Enable();
        switchCameraAngleAction.Enable();

        middleClickAction.started += OnPanStart;
        middleClickAction.canceled += OnPanEnd;
        scrollAction.performed += HandleZoom;
        switchCameraAngleAction.performed += OnSwitchCameraAngle;
    }

    private void OnDisable()
    {
        middleClickAction.started -= OnPanStart;
        middleClickAction.canceled -= OnPanEnd;
        scrollAction.performed -= HandleZoom;
        switchCameraAngleAction.performed -= OnSwitchCameraAngle;

        middleClickAction.Disable();
        mouseDeltaAction.Disable();
        scrollAction.Disable();
        switchCameraAngleAction.Disable();
    }

    private void OnSwitchCameraAngle(InputAction.CallbackContext ctx)
    {
        if (!ctx.performed) return;
        _isCameraAngleSwitched = !_isCameraAngleSwitched;
    }

    private void OnPanStart(InputAction.CallbackContext ctx)
    {
        _isPanning = true;
        _lastMousePos = Mouse.current.position.ReadValue(); // 初期位置記録
    }

    private void OnPanEnd(InputAction.CallbackContext ctx) => _isPanning = false;

    private void HandlePan()
    {
        if (!_isPanning) return;

        var currentMousePos = Mouse.current.position.ReadValue(); // または mouseDeltaAction.ReadValue<Vector2>() を積算してもよい
        var delta = currentMousePos - _lastMousePos;

        var camTransform = virtualCamera.transform;
        var right = camTransform.right;
        var forward = Vector3.ProjectOnPlane(camTransform.forward, Vector3.up).normalized;

        var move = (-right * delta.x - forward * delta.y) * followSpeed;
        followTarget.position += move;

        _lastMousePos = currentMousePos;
    }

    private void HandleZoom(InputAction.CallbackContext ctx)
    {
        var scroll = ctx.ReadValue<Vector2>().y;
        if (Mathf.Abs(scroll) <= 0.01f) return;
        _targetFOV -= scroll * zoomSpeed * Time.deltaTime;
        _targetFOV = Mathf.Clamp(_targetFOV, minZoom, maxZoom);
    }

    private void SmoothingUpdate()
    {
        virtualCamera.Lens.OrthographicSize =
            Mathf.Lerp(virtualCamera.Lens.OrthographicSize, _targetFOV, Time.deltaTime * zoomSmoothing);

        var endAngle = _isCameraAngleSwitched ? cameraAngle : _defaultCameraAngle;
        transform.localRotation = Quaternion.Lerp(
            transform.localRotation,
            Quaternion.Euler(endAngle),
            Time.deltaTime * cameraAngleSmoothing
        );
    }
}
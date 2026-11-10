using System;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

public class CameraControllerAdapter : MonoBehaviour
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
    [Header("一人称")]
    [SerializeField] private float eyeHeight = 1.6f;
    [SerializeField] private float walkSpeed = 3f;
    [SerializeField] private float lookSensitivity = .12f;
    public bool IsFirstPerson { get; private set; }
    public event Action<bool> FirstPersonChanged;
    private FirstPersonWalkAdapter _walk;
    private CinemachineFollow _follow;
    private CinemachineBrain _brain;
    private Camera _output;
    private LensSettings _savedLens;
    private CinemachineBrain.LensModeOverrideSettings _savedBrainMode;
    private Vector3 _savedPosition;
    private Quaternion _savedRotation;
    private bool _savedFollow, _savedOrthographic, _savedCursorVisible;
    private CursorLockMode _savedCursorLock;

    private void Start()
    {
        _targetFOV = virtualCamera.Lens.OrthographicSize;
        _defaultCameraAngle = transform.localRotation.eulerAngles;
    }

    public void FocusOn(Vector3 position)
    {
        // 開始時だけ目標を合わせる。以後のパン操作を毎フレーム上書きしない。
        followTarget.position = position;
        // 初回の古い追従位置から移動せず、最初の表示から換金先を見せる。
        virtualCamera.PreviousStateIsValid = false;
    }

    private void Update()
    {
        var keyboard = Keyboard.current;
        if (keyboard != null && keyboard.fKey.wasPressedThisFrame) ToggleFirstPerson();
        else if (IsFirstPerson && keyboard != null && keyboard.escapeKey.wasPressedThisFrame) SetFirstPerson(false);
        if (IsFirstPerson)
        {
            var movement = keyboard == null ? Vector2.zero : new Vector2(
                (keyboard.dKey.isPressed ? 1 : 0) - (keyboard.aKey.isPressed ? 1 : 0),
                (keyboard.wKey.isPressed ? 1 : 0) - (keyboard.sKey.isPressed ? 1 : 0));
            var look = Mouse.current != null ? Mouse.current.delta.ReadValue() : Vector2.zero;
            _walk.Step(movement, look, keyboard != null && (keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed), Time.unscaledDeltaTime);
            transform.SetPositionAndRotation(_walk.Position, _walk.Rotation);
            return;
        }
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
        SetFirstPerson(false);
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
        SwitchCameraAngle();
    }

    public void SwitchCameraAngle() { if (!IsFirstPerson) _isCameraAngleSwitched = !_isCameraAngleSwitched; }

    public void ToggleFirstPerson() => SetFirstPerson(!IsFirstPerson);

    public bool SetFirstPerson(bool active)
    {
        if (active == IsFirstPerson) return true;
        if (active)
        {
            var grid = GridFieldDatabaseAdapter.Instance;
            if (grid == null || virtualCamera == null || followTarget == null || Camera.main == null) return false;
            _walk = new FirstPersonWalkAdapter(grid, eyeHeight, walkSpeed, lookSensitivity);
            if (!_walk.TryStart(followTarget.position)) { Debug.LogWarning("散策を始められる床が見つかりません。", this); return false; }
            _output = Camera.main; _brain = _output.GetComponent<CinemachineBrain>();
            _follow = virtualCamera.GetComponent<CinemachineFollow>();
            _savedLens = virtualCamera.Lens; _savedPosition = transform.position; _savedRotation = transform.rotation;
            _savedFollow = _follow != null && _follow.enabled; _savedOrthographic = _output.orthographic;
            _savedCursorLock = Cursor.lockState; _savedCursorVisible = Cursor.visible;
            if (_follow != null) _follow.enabled = false;
            if (_brain != null)
            {
                _savedBrainMode = _brain.LensModeOverride;
                _brain.LensModeOverride = new CinemachineBrain.LensModeOverrideSettings { Enabled = true, DefaultMode = LensSettings.OverrideModes.Perspective };
            }
            virtualCamera.Lens.ModeOverride = LensSettings.OverrideModes.Perspective;
            virtualCamera.Lens.FieldOfView = 65; virtualCamera.Lens.NearClipPlane = .05f;
            transform.SetPositionAndRotation(_walk.Position, _walk.Rotation);
            Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false;
        }
        else
        {
            if (virtualCamera != null) virtualCamera.Lens = _savedLens;
            transform.SetPositionAndRotation(_savedPosition, _savedRotation);
            if (_follow != null) _follow.enabled = _savedFollow;
            if (_brain != null) _brain.LensModeOverride = _savedBrainMode;
            if (_output != null) _output.orthographic = _savedOrthographic;
            Cursor.lockState = _savedCursorLock; Cursor.visible = _savedCursorVisible;
        }
        IsFirstPerson = active;
        _isPanning = false;
        if (virtualCamera != null) virtualCamera.PreviousStateIsValid = false;
        FirstPersonChanged?.Invoke(active);
        return true;
    }

    private void OnApplicationFocus(bool focused) { if (!focused && IsFirstPerson) SetFirstPerson(false); }

    private void OnPanStart(InputAction.CallbackContext ctx)
    {
        if (IsFirstPerson) return;
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
        if (IsFirstPerson) return;
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

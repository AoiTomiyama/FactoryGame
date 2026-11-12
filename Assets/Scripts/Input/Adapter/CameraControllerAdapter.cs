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
    private bool _choosingStart;
    public void SetStartSelection(bool active) => _choosingStart=active;
    public event Action<bool> FirstPersonChanged;
    private FirstPersonWalkAdapter _walk;
    private CinemachineComponentBase _positionControl;
    private CinemachineBrain _brain;
    private Camera _output;
    private LensSettings _savedLens;
    private CinemachineBrain.LensModeOverrideSettings _savedBrainMode;
    private Vector3 _savedPosition;
    private Quaternion _savedRotation;
    private bool _savedPositionControl, _savedOrthographic, _savedCursorVisible;
    private int _enteredFirstPersonFrame;
    private CursorLockMode _savedCursorLock;
    private bool _firstPersonRequested, _transitionActive;
    private float _transitionElapsed, _transitionFromFov;
    private Vector3 _transitionFromPosition;
    private Quaternion _transitionFromRotation;
    private const float TransitionSeconds = .5f;
    public bool IsTransitioning => _transitionActive;

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
            if (_transitionActive) { UpdateFirstPersonTransition(); return; }
            var movement = keyboard == null ? Vector2.zero : new Vector2(
                (keyboard.dKey.isPressed ? 1 : 0) - (keyboard.aKey.isPressed ? 1 : 0),
                (keyboard.wKey.isPressed ? 1 : 0) - (keyboard.sKey.isPressed ? 1 : 0));
            // 切替クリックとカーソル固定で発生した差分を、最初の見回しへ持ち越さない。
            var look = Mouse.current != null && Time.frameCount > _enteredFirstPersonFrame + 1
                ? Mouse.current.delta.ReadValue() : Vector2.zero;
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
        RestoreOverview();
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

    public void ToggleFirstPerson() => SetFirstPerson(!_firstPersonRequested);

    public bool SetFirstPerson(bool active)
    {
        if(active && _choosingStart) return false;
        if (_firstPersonRequested == active && (IsFirstPerson == active || _transitionActive)) return true;
        if (active && !IsFirstPerson)
        {
            var grid = GridFieldDatabaseAdapter.Instance;
            if (grid == null || virtualCamera == null || followTarget == null || Camera.main == null) return false;
            _walk = new FirstPersonWalkAdapter(grid, eyeHeight, walkSpeed, lookSensitivity);
            if (!_walk.TryStart(followTarget.position)) { Debug.LogWarning("散策を始められる床が見つかりません。", this); return false; }
            _output = Camera.main; _brain = _output.GetComponent<CinemachineBrain>();
            // Sceneの実際のBodyを止め、Position Composerによる俯瞰位置への上書きを防ぐ。
            _positionControl = virtualCamera.GetCinemachineComponent(CinemachineCore.Stage.Body);
            _savedLens = virtualCamera.Lens;
            _savedPosition = _output.transform.position; _savedRotation = _output.transform.rotation;
            _savedPositionControl = _positionControl != null && _positionControl.enabled; _savedOrthographic = _output.orthographic;
            _savedCursorLock = Cursor.lockState; _savedCursorVisible = Cursor.visible;
            if (_positionControl != null) _positionControl.enabled = false;
            if (_brain != null)
            {
                _savedBrainMode = _brain.LensModeOverride;
                _brain.LensModeOverride = new CinemachineBrain.LensModeOverrideSettings { Enabled = true, DefaultMode = LensSettings.OverrideModes.Perspective };
            }
            // 注視点での大きさを揃えて透視投影へ移り、投影切替時の急な拡大を抑える。
            virtualCamera.Lens.FieldOfView = _savedOrthographic ? OverviewPerspectiveFov() : _output.fieldOfView;
            virtualCamera.Lens.ModeOverride = LensSettings.OverrideModes.Perspective;
            virtualCamera.Lens.NearClipPlane = .05f;
            transform.SetPositionAndRotation(_savedPosition, _savedRotation);
            Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false;
            IsFirstPerson = true;
            FirstPersonChanged?.Invoke(true);
        }
        else if (!active && !IsFirstPerson) return true;
        _firstPersonRequested = active;
        _isPanning = false;
        _transitionFromPosition = transform.position; _transitionFromRotation = transform.rotation;
        _transitionFromFov = virtualCamera.Lens.FieldOfView;
        _transitionElapsed = 0; _transitionActive = true;
        virtualCamera.PreviousStateIsValid = false;
        return true;
    }

    private float OverviewPerspectiveFov()
    {
        var depth = Mathf.Max(.1f, Vector3.Dot(followTarget.position - _savedPosition, _savedRotation * Vector3.forward));
        return Mathf.Clamp(2 * Mathf.Atan(_savedLens.OrthographicSize / depth) * Mathf.Rad2Deg, 5, 120);
    }

    private void UpdateFirstPersonTransition()
    {
        _transitionElapsed += Time.unscaledDeltaTime;
        var progress = Mathf.Clamp01(_transitionElapsed / TransitionSeconds);
        var eased = progress * progress * (3 - 2 * progress);
        var targetPosition = _firstPersonRequested ? _walk.Position : _savedPosition;
        var targetRotation = _firstPersonRequested ? _walk.Rotation : _savedRotation;
        var targetFov = _firstPersonRequested ? 65 : _savedOrthographic ? OverviewPerspectiveFov() : _savedLens.FieldOfView;
        transform.SetPositionAndRotation(Vector3.Lerp(_transitionFromPosition, targetPosition, eased),
            Quaternion.Slerp(_transitionFromRotation, targetRotation, eased));
        virtualCamera.Lens.FieldOfView = Mathf.Lerp(_transitionFromFov, targetFov, eased);
        if (progress < 1) return;
        _transitionActive = false;
        _enteredFirstPersonFrame = Time.frameCount;
        if (!_firstPersonRequested) RestoreOverview();
    }

    private void RestoreOverview()
    {
        if (!IsFirstPerson) return;
        _firstPersonRequested = _transitionActive = false;
        if (virtualCamera != null) virtualCamera.Lens = _savedLens;
        transform.SetPositionAndRotation(_savedPosition, _savedRotation);
        if (_positionControl != null) _positionControl.enabled = _savedPositionControl;
        if (_brain != null) _brain.LensModeOverride = _savedBrainMode;
        if (_output != null) _output.orthographic = _savedOrthographic;
        Cursor.lockState = _savedCursorLock; Cursor.visible = _savedCursorVisible;
        IsFirstPerson = false; _isPanning = false;
        if (virtualCamera != null) virtualCamera.PreviousStateIsValid = false;
        FirstPersonChanged?.Invoke(false);
    }

    private void OnApplicationFocus(bool focused) { if (!focused) RestoreOverview(); }

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

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

    private Vector3 _targetPosition;
    private Vector2 _lastMousePos;
    private float _targetFOV;

    private void Start()
    {
        _targetPosition = followTarget.position;
        _targetFOV = virtualCamera.Lens.OrthographicSize;
    }

    private void Update()
    {
        HandlePan();
        HandleZoom();
        SmoothUpdate();
    }

    private void HandlePan()
    {
        if (Mouse.current.middleButton.wasPressedThisFrame)
        {
            _lastMousePos = Mouse.current.position.ReadValue();
        }

        if (Mouse.current.middleButton.isPressed)
        {
            var currentMousePos = Mouse.current.position.ReadValue();
            var delta = currentMousePos - _lastMousePos;
            var camTransform = virtualCamera.transform;
            var right = camTransform.right;
            var forward = Vector3.ProjectOnPlane(camTransform.forward, Vector3.up).normalized; // 水平成分のみ

            var move = (-right * delta.x - forward * delta.y) * followSpeed;
            _targetPosition += move;
            _lastMousePos = currentMousePos;
        }
    }

    private void HandleZoom()
    {
        var scroll = Mouse.current.scroll.ReadValue().y;
        if (Mathf.Abs(scroll) > 0.01f)
        {
            _targetFOV -= scroll * zoomSpeed * Time.deltaTime;
            _targetFOV = Mathf.Clamp(_targetFOV, minZoom, maxZoom);
        }
    }

    private void SmoothUpdate()
    {
        followTarget.position = _targetPosition;
        virtualCamera.Lens.OrthographicSize =
            Mathf.Lerp(virtualCamera.Lens.OrthographicSize, _targetFOV, Time.deltaTime * zoomSmoothing);
    }
}
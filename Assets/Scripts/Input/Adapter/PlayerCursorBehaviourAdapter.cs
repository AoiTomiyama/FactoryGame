using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerCursorBehaviourAdapter : MonoBehaviour
{
    [SerializeField] private LayerMask layerMask;
    [SerializeField] private InputAction mouseAction;
    [SerializeField] private InputAction leftClickAction;
    [SerializeField] private InputAction rightClickAction;
    [SerializeField] private InputAction rotateAction;
    [SerializeField] private UIRaycasterAdapter raycaster;
    [SerializeField] private CellPlacerAdapter placer;
    [SerializeField] private OperationBarAdapter operationBar;

    private Camera _camera;
    private Vector2 _mousePosition;
    private bool _isLeftClickPressed;
    private bool _firstPerson;
    private CameraControllerAdapter _cameraController;

    private void Start()
    {
        if (placer == null)
        {
            placer = FindAnyObjectByType<CellPlacerAdapter>();
        }

        _camera = Camera.main;
        _cameraController = FindAnyObjectByType<CameraControllerAdapter>();
        if (_cameraController != null) _cameraController.FirstPersonChanged += SetFirstPerson;
        if (operationBar != null) operationBar.Configure(this, _cameraController);
    }
    private void SetFirstPerson(bool active)
    {
        _firstPerson = active; _isLeftClickPressed = false;
        placer.CancelPlacementPreview();
    }
    private void OnDestroy() { if (_cameraController != null) _cameraController.FirstPersonChanged -= SetFirstPerson; }

    private void Update()
    {
        if (_isLeftClickPressed)
        {
            OnLeftClickPressed();
        }
    }

    private void OnEnable()
    {
        mouseAction.Enable();
        leftClickAction.Enable();
        rightClickAction.Enable();
        rotateAction.Enable();

        mouseAction.performed += OnMouseMove;
        leftClickAction.started += OnLeftClickDown;
        leftClickAction.canceled += OnLeftClickUp;
        rightClickAction.performed += OnRightClick;
        rotateAction.performed += OnRotateObject;
    }

    private void OnDisable()
    {
        mouseAction.performed -= OnMouseMove;
        leftClickAction.started -= OnLeftClickDown;
        leftClickAction.canceled -= OnLeftClickUp;
        rightClickAction.performed -= OnRightClick;
        rotateAction.performed -= OnRotateObject;

        mouseAction.Disable();
        leftClickAction.Disable();
        rightClickAction.Disable();
        rotateAction.Disable();
    }

    private void OnMouseMove(InputAction.CallbackContext context)
    {
        if (_firstPerson || !context.performed) return;

        _mousePosition = context.ReadValue<Vector2>();
        if (raycaster.IsPointerOverUI(_mousePosition)) return;

        var ray = _camera.ScreenPointToRay(_mousePosition);
        if (Physics.Raycast(ray, out var hit, Mathf.Infinity, layerMask))
        {
            placer.SelectGrid(hit.collider.gameObject, hit.point);
        }
    }

    private void OnLeftClickDown(InputAction.CallbackContext context)
    {
        if (_firstPerson) return;
        if (raycaster.IsPointerOverUI(_mousePosition)) return;
        _isLeftClickPressed = true;
        placer.PointerBegin();
    }

    private void OnLeftClickPressed()
    {
        placer.PointerDrag();
    }

    private void OnLeftClickUp(InputAction.CallbackContext context)
    {
        if (!_isLeftClickPressed) return;
        _isLeftClickPressed = false;
        // UI上で離したドラッグは取消し、ボタン操作から配置を確定しない。
        if (raycaster.IsPointerOverUI(_mousePosition)) placer.CancelPlacementPreview();
        else placer.PointerEnd();
    }

    private void OnRightClick(InputAction.CallbackContext context)
    {
        if (_firstPerson || !context.performed) return;
        if (raycaster.IsPointerOverUI(_mousePosition)) return;
        placer.TransferDataToUI();
    }

    private void OnRotateObject(InputAction.CallbackContext context)
    {
        if (!context.performed) return;
        if (raycaster.IsPointerOverUI(_mousePosition)) return;

        RotatePlacement();
    }

    // ボタンとショートカットで同じ配置方向を更新する。
    public void RotatePlacement() { if (!_firstPerson) transform.Rotate(Vector3.up, 90f); }
}

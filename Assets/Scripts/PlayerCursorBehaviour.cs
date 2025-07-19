using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerCursorBehaviour : MonoBehaviour
{
    [SerializeField] private LayerMask layerMask;
    [SerializeField] private InputAction mouseAction;
    [SerializeField] private InputAction leftClickAction;
    [SerializeField] private InputAction rightClickAction;
    [SerializeField] private InputAction rotateAction;
    [SerializeField] private UIRaycaster raycaster;
    [SerializeField] private CellPlacer placer;

    private Camera _camera;
    private Vector2 _mousePosition;
    private bool _isLeftClickPressed;

    private void Start()
    {
        if (placer == null)
        {
            placer = FindAnyObjectByType<CellPlacer>();
        }
        _camera = Camera.main;
    }

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
        if (!context.performed) return;

        _mousePosition = context.ReadValue<Vector2>();
        if (raycaster.IsPointerOverUI(_mousePosition)) return;

        var ray = _camera.ScreenPointToRay(_mousePosition);
        if (Physics.Raycast(ray, out var hit, Mathf.Infinity, layerMask))
        {
            placer.SelectGrid(hit.collider.gameObject);
        }
    }

    private void OnLeftClickDown(InputAction.CallbackContext context)
    {
        if (raycaster.IsPointerOverUI(_mousePosition)) return;
        _isLeftClickPressed = true;
        placer.PointerBegin();
    }

    private void OnLeftClickPressed()
    {
        placer.PointerMove();
    }
    
    private void OnLeftClickUp(InputAction.CallbackContext context)
    {
        _isLeftClickPressed = false;
        placer.PointerEnd();
    }

    private void OnRightClick(InputAction.CallbackContext context)
    {
        if (!context.performed) return;
        if (raycaster.IsPointerOverUI(_mousePosition)) return;
        placer.TransferDataToUI();
    }

    private void OnRotateObject(InputAction.CallbackContext context)
    {
        if (!context.performed) return;
        if (raycaster.IsPointerOverUI(_mousePosition)) return;

        transform.Rotate(Vector3.up, 90f);
    }
}
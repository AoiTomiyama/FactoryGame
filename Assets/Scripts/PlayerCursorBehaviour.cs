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

    private void Start()
    {
        if (placer == null)
        {
            placer = FindAnyObjectByType<CellPlacer>();
        }
        _camera = Camera.main;
    }

    private void OnEnable()
    {
        mouseAction.Enable();
        leftClickAction.Enable();
        rightClickAction.Enable();
        rotateAction.Enable();

        mouseAction.performed += OnMouseMove;
        leftClickAction.performed += OnLeftClick;
        rightClickAction.performed += OnRightClick;
        rotateAction.performed += OnRotateObject;
    }

    private void OnDisable()
    {
        mouseAction.performed -= OnMouseMove;
        leftClickAction.performed -= OnLeftClick;
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

    private void OnLeftClick(InputAction.CallbackContext context)
    {
        if (!context.performed) return;
        if (raycaster.IsPointerOverUI(_mousePosition)) return;
        placer.ReplaceCell();
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
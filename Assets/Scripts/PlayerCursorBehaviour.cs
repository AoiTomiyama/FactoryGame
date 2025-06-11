using System;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(PlayerInput))]
public class PlayerCursorBehaviour : MonoBehaviour
{
    [SerializeField] private LayerMask _layerMask;
    private Camera _camera;
    private InputAction _mouseAction;
    private InputAction _clickAction;

    private void Start()
    {
        _camera = Camera.main;
        _mouseAction = new InputAction("PlayerActions", binding: "<Mouse>/position");
        _mouseAction.Enable();
        _mouseAction.performed += MouseMove;
        
        _clickAction = new InputAction("PlayerActions", binding: "<Mouse>/leftButton");
        _clickAction.Enable();
        _clickAction.performed += Click;
    }

    private void OnDisable()
    {
        _mouseAction.Disable();
        _clickAction.Disable();
    }

    private void MouseMove(InputAction.CallbackContext context)
    {
        
        var ray = _camera.ScreenPointToRay(context.ReadValue<Vector2>());
        if (Physics.Raycast(ray, out var hit, Mathf.Infinity, _layerMask))
        {
            var otherPos = hit.collider.gameObject.transform.position;
            transform.position = otherPos + Vector3.up;
        }
    }
    
    private void Click(InputAction.CallbackContext context)
    {
        if (!context.performed) return;
        var ray = _camera.ScreenPointToRay(_mouseAction.ReadValue<Vector2>());
        if (Physics.Raycast(ray, out var hit, Mathf.Infinity, _layerMask))
        {
            Debug.Log($"Clicked on: {hit.collider.gameObject.name}");
            // ここでクリック時の処理を追加できます
        }
    }
}
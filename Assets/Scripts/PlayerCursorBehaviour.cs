using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(PlayerInput))]
public class PlayerCursorBehaviour : MonoBehaviour
{
    [SerializeField] private LayerMask _layerMask;
    private Camera _camera;

    private void Start()
    {
        _camera = Camera.main;
    }

    public void MouseMove(InputAction.CallbackContext context)
    {
        
        var ray = _camera.ScreenPointToRay(context.ReadValue<Vector2>());
        if (Physics.Raycast(ray, out var hit, Mathf.Infinity, _layerMask))
        {
            var otherPos = hit.collider.gameObject.transform.position;
            transform.position = otherPos + Vector3.up;
        }
    }
}
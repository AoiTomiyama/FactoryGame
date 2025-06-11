using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerCursorBehaviour : MonoBehaviour
{
    [SerializeField] private LayerMask _layerMask;
    [SerializeField] private Color _selectedColor = Color.red;
    [SerializeField] private InputAction _mouseAction;
    [SerializeField] private InputAction _leftClickAction;
    [SerializeField] private InputAction _rightClickAction;
    [SerializeField] private GameObject _cellPrefab;
    [SerializeField] private GridFieldSO _fieldSo;
    private Camera _camera;
    private GameObject _selectedObject;

    private void Start()
    {
        _camera = Camera.main;
    }

    private void OnEnable()
    {
        _mouseAction.Enable();
        _leftClickAction.Enable();
        _rightClickAction.Enable();

        _mouseAction.performed += OnMouseMove;
        _leftClickAction.performed += OnLeftClick;
        _rightClickAction.performed += OnRightClick;
    }

    private void OnDisable()
    {
        _mouseAction.Disable();
        _leftClickAction.Disable();
        _rightClickAction.Disable();
    }

    private void OnMouseMove(InputAction.CallbackContext context)
    {
        if (!context.performed) return;

        var ray = _camera.ScreenPointToRay(context.ReadValue<Vector2>());
        if (Physics.Raycast(ray, out var hit, Mathf.Infinity, _layerMask))
        {
            // 直前に選択されていたオブジェクトと同じ場合は何もしない
            if (hit.collider.gameObject == _selectedObject) return;

            SelectGrid(hit.collider.gameObject);
        }
    }

    private void SelectGrid(GameObject target)
    {
        if (target == null) return;

        // 直前に選択されていたオブジェクトがある場合、その色を元に戻す
        if (_selectedObject != null &&
            _selectedObject.TryGetComponent<Renderer>(out var prevRenderer))
        {
            prevRenderer.material.color = Color.white;
        }

        _selectedObject = target;
        if (_selectedObject.TryGetComponent<Renderer>(out var currentRenderer))
        {
            currentRenderer.material.color = _selectedColor;
        }
    }

    private void OnLeftClick(InputAction.CallbackContext context)
    {
        if (!context.performed) return;

        if (_selectedObject.TryGetComponent<CellBehaviour>(out var cellBehaviour))
        {
            var x = cellBehaviour.xIndex;
            var z = cellBehaviour.zIndex;
            ReplaceCell(x, z);
        }
        else
        {
            Debug.LogError($"{typeof(CellBehaviour)}が未割り当てです。");
        }
    }

    /// <summary>
    /// 選択されているセルを新しいセルに置き換える
    /// </summary>
    private void ReplaceCell(int x, int z)
    {
        var objName = _selectedObject.name;
        var pos = _selectedObject.transform.position;
        var parent = _selectedObject.transform.parent;
        Destroy(_selectedObject);
        
        var newObj = Instantiate(_cellPrefab, pos, Quaternion.identity, parent);
        newObj.name = objName;
        _fieldSo.SetNewCell(x, z, newObj);
        SelectGrid(newObj);
    }

    private void OnRightClick(InputAction.CallbackContext context) {}
}
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
    [SerializeField] private GameObject _defaultCellPrefab;
    [SerializeField] private GridFieldDatabase fieldDatabase;
    private Camera _camera;
    private CellBase _selectedCell;

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
            SelectGrid(hit.collider.gameObject);
        }
    }

    private void SelectGrid(GameObject target)
    {
        if (target == null) return;
        

        // 直前に選択されていたオブジェクトがある場合、その色を元に戻す
        if (_selectedCell != null &&
            _selectedCell.cellModel.TryGetComponent<Renderer>(out var prevRenderer))
        {
            // 直前に選択されていたオブジェクトと同じ場合は何もしない
            if (target == _selectedCell.gameObject) return;
            prevRenderer.material.color = Color.white;
        }

        if (!target.TryGetComponent<CellBase>(out var cellBase)) return;
        _selectedCell = cellBase;
        if (_selectedCell.cellModel.TryGetComponent<Renderer>(out var currentRenderer))
        {
            currentRenderer.material.color = _selectedColor;
        }
        Debug.Log(_selectedCell.gameObject.name);
    }

    private void OnLeftClick(InputAction.CallbackContext context)
    {
        if (!context.performed) return;

        ReplaceCell(_cellPrefab);
    }

    private void OnRightClick(InputAction.CallbackContext context)
    {
        if (!context.performed) return;

        ReplaceCell(_defaultCellPrefab);
    }

    /// <summary>
    /// 選択されているセルを新しいセルに置き換える
    /// </summary>
    private void ReplaceCell(GameObject prefab)
    {
        if (prefab == null)
        {
            Debug.LogError("Prefabが未割り当てです。");
            return;
        }
        if (_selectedCell == null)
        {
            Debug.LogWarning("セルが選択されていません。");
            return;
        }

        // 選択されているセルの情報を取得
        var x = _selectedCell.xIndex;
        var z = _selectedCell.zIndex;
        var objName = _selectedCell.name;
        var pos = _selectedCell.transform.position;
        var parent = _selectedCell.transform.parent;
        
        // セルを削除
        Destroy(_selectedCell.gameObject);
        
        // 新しいセルを生成
        var newObj = Instantiate(prefab, pos, Quaternion.identity, parent);
        newObj.name = objName;
        
        // 新しいセルの情報を保存
        fieldDatabase.SetNewCell(x, z, newObj);
    }

}
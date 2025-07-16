using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerCursorBehaviour : MonoBehaviour
{
    [SerializeField] private LayerMask layerMask;
    [SerializeField] private InputAction mouseAction;
    [SerializeField] private InputAction leftClickAction;
    [SerializeField] private InputAction rightClickAction;
    [SerializeField] private InputAction rotateAction;
    [SerializeField] private CellDatabaseSO[] cellDatabaseArr;
    [SerializeField] private UIRaycaster raycaster;

    private Camera _camera;
    private CellBase _selectedCell;
    private GameObject _placeholderCell;
    private CellBase _cachedCell;
    private CellType _selectedCellType = CellType.Empty;
    private Vector2 _mousePosition;

    private void Start()
    {
        _camera = Camera.main;

        SetSelectedCellType(_selectedCellType);
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
            SelectGrid(hit.collider.gameObject);
        }
    }

    private void SelectGrid(GameObject target)
    {
        if (target == null) return;

        // 直前に選択されていたオブジェクトがある場合、その色を元に戻す
        if (_selectedCell != null)
        {
            // 直前に選択されていたオブジェクトと同じ場合は何もしない
            if (target == _selectedCell.gameObject) return;
            _selectedCell.CellModel.SetActive(true);
        }

        if (!target.TryGetComponent<CellBase>(out var cellBase)) return;
        _selectedCell = cellBase;
        transform.position = _selectedCell.transform.position;

        if (_selectedCell is not EmptyCell) return;
        _selectedCell.CellModel.SetActive(false);
    }

    private void OnLeftClick(InputAction.CallbackContext context)
    {
        if (!context.performed) return;
        if (raycaster.IsPointerOverUI(_mousePosition)) return;

        if (!TryReplaceCell(_cachedCell))
        {
            Debug.LogWarning("セルの置き換えに失敗しました。セルが選択されているか、適切なPrefabが割り当てられているか確認してください。");
        }
    }

    private bool TryReplaceCell(CellBase cachedCell)
    {
        if (cachedCell == null ||
            _selectedCell == null ||
            _selectedCellType != CellType.Empty && _selectedCell is not EmptyCell)
        {
            return false;
        }

        ReplaceCell(cachedCell);
        CellStatusView.Instance.UpdateUIStatusWindow(_selectedCell);
        return true;
    }

    /// <summary>
    /// 選択されているセルを新しいセルに置き換える
    /// </summary>
    private void ReplaceCell(CellBase cachedCell)
    {
        // 選択されているセルの情報を取得
        var x = _selectedCell.XIndex;
        var z = _selectedCell.ZIndex;
        var objName = _selectedCell.name;
        var pos = _selectedCell.transform.position;
        var parent = _selectedCell.transform.parent;
        var index = _selectedCell.transform.GetSiblingIndex();

        if (_selectedCell is ConnectableCellBase connectableCell)
        {
            connectableCell.OnDisconnect();
        }

        // セルを削除
        Destroy(_selectedCell.gameObject);
        _selectedCell = null;

        // 新しいセルを生成
        var newObj = Instantiate(cachedCell, pos, transform.rotation, parent);
        newObj.transform.SetSiblingIndex(index);
        newObj.name = objName;

        // 新しいセルの情報を保存
        GridFieldDatabase.Instance.SaveCell(x, z, newObj);
        newObj.gameObject.SetActive(true);
        newObj.InitializeSystem();

        _selectedCell = newObj;
    }

    private void OnRightClick(InputAction.CallbackContext context)
    {
        if (!context.performed) return;
        if (raycaster.IsPointerOverUI(_mousePosition)) return;
        CellStatusView.Instance.UpdateUIStatusWindow(_selectedCell);
    }

    public void SetSelectedCellType(CellType cellType)
    {
        foreach (var database in cellDatabaseArr)
        {
            if (!database.TryGetCellInfo(cellType, out var cellInfo)) continue;

            SetSelectedCellType(cellInfo.CellType, cellInfo.FieldCellPrefab, cellInfo.PlaceholderCellPrefab);
            return;
        }

        Debug.LogWarning($"CellType {_selectedCellType} の情報が見つかりません。");
    }

    public void SetSelectedCellType(CellType type, CellBase cellBase, GameObject placeholder)
    {
        if (cellBase == null || placeholder == null) return;

        _selectedCellType = type;
        Destroy(_placeholderCell);
        _placeholderCell = Instantiate(placeholder, transform.position, transform.rotation, transform);
        if (_cachedCell != null)
        {
            Destroy(_cachedCell.gameObject);
        }

        _cachedCell = Instantiate(cellBase, transform.position, transform.rotation, transform);
        _cachedCell.gameObject.SetActive(false);
        _cachedCell.name = $"InactiveCell_{_selectedCellType}";
    }

    private void OnRotateObject(InputAction.CallbackContext context)
    {
        if (!context.performed) return;
        if (raycaster.IsPointerOverUI(_mousePosition)) return;

        transform.Rotate(Vector3.up, 90f);
    }
}
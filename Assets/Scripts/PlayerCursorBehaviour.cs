using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerCursorBehaviour : MonoBehaviour
{
    [SerializeField] private LayerMask layerMask;
    [SerializeField] private InputAction mouseAction;
    [SerializeField] private InputAction leftClickAction;
    [SerializeField] private InputAction rightClickAction;
    [SerializeField] private InputAction rotateAction;
    [SerializeField] private CellDatabaseSO cellDatabaseSo;

    private Camera _camera;
    private CellBase _selectedCell;
    private GameObject _placeholderCell;
    private IUIRenderable _renderingCell;

    [SerializeField] private CellType selectedCellType;
    [SerializeField] private UIRaycaster raycaster;

    private Vector2 _mousePosition;

    private void Start()
    {
        _camera = Camera.main;

        SetSelectedCellType(selectedCellType);
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

        if (!cellDatabaseSo.TryGetCellInfo(selectedCellType, out var cellInfo)) return;
        var obj = cellInfo.FieldCellPrefab;

        if (!TryReplaceCell(obj))
        {
            Debug.LogWarning("セルの置き換えに失敗しました。セルが選択されているか、適切なPrefabが割り当てられているか確認してください。");
        }
    }

    private void OnRightClick(InputAction.CallbackContext context)
    {
        if (!context.performed) return;
        if (raycaster.IsPointerOverUI(_mousePosition)) return;
        if (_selectedCell is not IUIRenderable renderingCell) return;
        CellStatusView.Instance.SetStatusWindowActive(true);
        
        if (_renderingCell != null)
        {
            // 直前に選択されていたオブジェクトと同じ場合は何もしない
            if (renderingCell == _renderingCell) return;
                
            _renderingCell.IsUIActive = false;
            _renderingCell.ResetUI();
            CellStatusView.Instance.ResetStatusUI();
        }
        _renderingCell = renderingCell;
        _renderingCell.IsUIActive = true;
        _renderingCell.UpdateUI();
    }

    private bool TryReplaceCell(GameObject prefab)
    {
        if (prefab == null)
        {
            Debug.LogError("Prefabが未割り当てです。");
            return false;
        }

        if (_selectedCell == null)
        {
            Debug.LogWarning("セルが選択されていません。");
            return false;
        }

        if (selectedCellType != CellType.Empty && _selectedCell is not EmptyCell)
        {
            Debug.Log("既にセルが存在します。置き換えはできません");
            return false;
        }

        ReplaceCell(prefab);
        return true;
    }

    /// <summary>
    /// 選択されているセルを新しいセルに置き換える
    /// </summary>
    private void ReplaceCell(GameObject prefab)
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
        var newObj = Instantiate(prefab, pos, transform.rotation, parent);
        newObj.transform.SetSiblingIndex(index);
        newObj.name = objName;

        // 新しいセルの情報を保存
        GridFieldDatabase.Instance.SaveCell(x, z, newObj);
    }

    public void SetSelectedCellType(CellType cellType)
    {
        selectedCellType = cellType;
        if (cellDatabaseSo.TryGetCellInfo(selectedCellType, out var cellInfo))
        {
            Destroy(_placeholderCell);
            _placeholderCell = Instantiate(cellInfo.PlaceholderCellPrefab, transform.position, transform.rotation,
                transform);
        }
        else
        {
            Debug.LogWarning($"CellType {selectedCellType} の情報が見つかりません。");
        }
    }

    private void OnRotateObject(InputAction.CallbackContext context)
    {
        if (!context.performed) return;
        if (raycaster.IsPointerOverUI(_mousePosition)) return;

        transform.Rotate(Vector3.up, 90f);
    }
}
using System;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerCursorBehaviour : MonoBehaviour
{
    [SerializeField] private LayerMask layerMask;
    [SerializeField] private Color selectedColor = Color.red;
    [SerializeField] private InputAction mouseAction;
    [SerializeField] private InputAction leftClickAction;
    [SerializeField] private InputAction rightClickAction;
    [SerializeField] private GameObject cellPrefab;
    [SerializeField] private GameObject defaultCellPrefab;
    [SerializeField] private GridFieldDatabase fieldDatabase;

    private Camera _camera;
    private CellBase _selectedCell;
    
    [SerializeField] private CellInfo[] cellInfos;
    [Serializable]
    private struct CellInfo
    {
        public GameObject fieldCellPrefab;
        public GameObject placeholderCellPrefab;
    }
    
    private int _currentCellIndex = 0;

    private void Start()
    {
        _camera = Camera.main;
        Instantiate(cellInfos[_currentCellIndex].placeholderCellPrefab, transform.position, Quaternion.identity, transform);

        if (fieldDatabase != null) return;
        fieldDatabase = FindAnyObjectByType<GridFieldDatabase>();
        if (fieldDatabase == null)
        {
            Debug.LogError("GridFieldDatabaseが見つかりません。シーンに追加してください。");
        }
    }

    private void OnEnable()
    {
        mouseAction.Enable();
        leftClickAction.Enable();
        rightClickAction.Enable();

        mouseAction.performed += OnMouseMove;
        leftClickAction.performed += OnLeftClick;
        rightClickAction.performed += OnRightClick;
    }

    private void OnDisable()
    {
        mouseAction.performed -= OnMouseMove;
        leftClickAction.performed -= OnLeftClick;
        rightClickAction.performed -= OnRightClick;
        
        mouseAction.Disable();
        leftClickAction.Disable();
        rightClickAction.Disable();
    }

    private void OnMouseMove(InputAction.CallbackContext context)
    {
        if (!context.performed) return;

        var ray = _camera.ScreenPointToRay(context.ReadValue<Vector2>());
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
        if (_selectedCell is not EmptyCell) return;
        _selectedCell.CellModel.SetActive(false);
        transform.position = _selectedCell.transform.position;
    }

    private void OnLeftClick(InputAction.CallbackContext context)
    {
        if (!context.performed) return;

        var obj = cellInfos[_currentCellIndex].fieldCellPrefab;
        if (!TryReplaceCell(obj))
        {
            Debug.LogWarning("セルの置き換えに失敗しました。セルが選択されているか、適切なPrefabが割り当てられているか確認してください。");
        }
    }

    private void OnRightClick(InputAction.CallbackContext context)
    {
        if (!context.performed) return;

        // 右クリックでデフォルトのセルに置き換える
       ReplaceCell(defaultCellPrefab);
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
        if (_selectedCell is not EmptyCell)
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
        
        // セルを削除
        Destroy(_selectedCell.gameObject);
        _selectedCell = null;
        
        // 新しいセルを生成
        var newObj = Instantiate(prefab, pos, Quaternion.identity, parent);
        newObj.name = objName;
        
        // 新しいセルの情報を保存
        fieldDatabase.SaveCell(x, z, newObj);
    }

}
using System;
using UnityEngine;

public class CellPlacer : MonoBehaviour
{
    [SerializeField] private CellDatabaseSO[] cellDatabaseArr;
    private CellBase _selectedCell;
    private GameObject _placeholderCell;
    private CellBase _cachedCell;
    private CellType _selectedCellType = CellType.Empty;

    private void Start()
    {
        SetSelectedCellType(_selectedCellType);
    }

    /// <summary>
    /// カーソルが選択されているセルの位置に移動し、セルを選択する
    /// </summary>
    /// <param name="target"></param>
    public void SelectGrid(GameObject target)
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

    /// <summary>
    /// 選択されているセルを新しいセルに置き換える
    /// </summary>
    public void ReplaceCell()
    {
        // 置こうとしているセル、および選択されているセルがEmptyCellでない場合、上書きが発生してしまうため中断する
        if (_cachedCell == null ||
            _selectedCell == null ||
            _cachedCell is not EmptyCell && _selectedCell is not EmptyCell)
        {
            Debug.LogWarning("セルの置き換えに失敗しました。セルが選択されているか、適切なPrefabが割り当てられているか確認してください。");
            return;
        }
        
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
        var newObj = Instantiate(_cachedCell, pos, transform.rotation, parent);
        newObj.transform.SetSiblingIndex(index);
        newObj.name = objName;

        // 新しいセルの情報を保存
        GridFieldDatabase.Instance.SaveCell(x, z, newObj);
        newObj.gameObject.SetActive(true);
        newObj.InitializeSystem();

        _selectedCell = newObj;
        CellStatusView.Instance.UpdateUIStatusWindow(_selectedCell);
    }

    public void SetSelectedCellType(CellType cellType)
    {
        foreach (var database in cellDatabaseArr)
        {
            if (!database.TryGetCellInfo(cellType, out var cellInfo)) continue;

            _selectedCellType = cellInfo.CellType;
            Destroy(_placeholderCell);
            _placeholderCell = Instantiate(cellInfo.PlaceholderCellPrefab, transform.position, transform.rotation,
                transform);

            if (_cachedCell != null)
            {
                Destroy(_cachedCell.gameObject);
            }

            _cachedCell = Instantiate(cellInfo.FieldCellPrefab, transform.position, transform.rotation, transform);
            _cachedCell.gameObject.SetActive(false);
            return;
        }

        Debug.LogWarning($"CellType {_selectedCellType} の情報が見つかりません。");
    }

    public void UpdateCellData(Func<CellBase, GameObject, (CellBase, GameObject)> cellFunc)
    {
        if (_selectedCell == null || _cachedCell == null) return;

        var (updatedCell, updatedPlaceholder) = cellFunc(_cachedCell, _placeholderCell);
        if (updatedCell != null)
        {
            _cachedCell = updatedCell;
            _cachedCell.gameObject.SetActive(false);
        }

        if (updatedPlaceholder != null)
        {
            _placeholderCell = updatedPlaceholder;
        }
    }

    public void TransferDataToUI()
    {
        CellStatusView.Instance.UpdateUIStatusWindow(_selectedCell);
    }
}

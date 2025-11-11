using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class CellPlacer : MonoBehaviour
{
    [SerializeField] private CellDatabaseSO[] cellDatabaseArr;
    private CellBase _selectedCell;
    private CellBase _cachedCell;
    private GameObject _placeholderCell;
    private readonly HashSet<(CellBase cell, GameObject placeholder)> _selectedRangeCells = new();
    private CellType _selectedCellType = CellType.Empty;
    private Vector3Int _dragBeginPos;

    private void Start() => SetSelectedCellType(_selectedCellType);

    public void PointerBegin()
    {
        if (_selectedCell == null) return;
        _dragBeginPos = Vector3Int.RoundToInt(_selectedCell.transform.position);
    }

    public void PointerDrag()
    {
        if (_selectedCell == null) return;
        FindRangedGrid(_dragBeginPos, Vector3Int.RoundToInt(_selectedCell.transform.position));
    }

    public void PointerEnd() => ReplaceRangedCells();
    public void TransferDataToUI() => CellStatusView.Instance.UpdateUIStatusWindow(_selectedCell);

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

        if (cellBase is not EmptyCell) return;
        _selectedCell.CellModel.SetActive(false);
    }

    private void FindRangedGrid(Vector3Int from, Vector3Int to)
    {
        var newSelection = new HashSet<CellBase>();
        var dir = to - from;

        // 方向ベクトルの絶対値を比較して、どちらの軸を優先するか決定
        var isX = Mathf.Abs(dir.x) >= Mathf.Abs(dir.z);

        // 開始位置と終了位置を決定
        var start = isX ? from.x : from.z;
        var end = isX ? from.x + dir.x : from.z + dir.z;
        var fixedCoord = isX ? from.z : from.x;

        // 正負の方向に応じてステップを決定
        var step = start <= end ? 1 : -1;
        for (int i = start; i != end + step; i += step)
        {
            var x = isX ? i : fixedCoord;
            var z = isX ? fixedCoord : i;

            var cell = GridFieldDatabase.Instance.GetCell(x, z);
            if (cell == null) continue;
            // _cachedCellがEmptyCellの場合は、全て置き換える
            // そうでない場合は、置く先がEmptyCellでない場合、置き換えを行う
            if (_cachedCell is EmptyCell || cell is EmptyCell)
            {
                // 置き換え可能なセルを選択
                newSelection.Add(cell);
            }
        }

        SelectRangedGrid(newSelection);
    }

    private void SelectRangedGrid(HashSet<CellBase> newSelection)
    {
        var newSet = new HashSet<CellBase>(newSelection);
        var oldSet = _selectedRangeCells
            .ToDictionary(t => t.cell, t => t.placeholder);

        // 既存の選択範囲から新しい選択範囲に含まれないセルを削除
        foreach (var (cell, placeholder) in _selectedRangeCells)
        {
            if (newSet.Contains(cell)) continue;

            // すでに置いてあるセルの表示を元に戻す
            cell.CellModel.SetActive(true);

            // プレースホルダーを削除
            // Destroy(placeholder.gameObject);
            placeholder.SetActive(false);
        }


        // 新しい選択範囲に含まれるセルを追加
        foreach (var cell in newSelection)
        {
            cell.CellModel.SetActive(false);
            if (!oldSet.TryGetValue(cell, out var placeholder))
            {
                placeholder = Instantiate(_placeholderCell, cell.transform.position, transform.rotation);
            }
            else
            {
                placeholder.SetActive(true);
            }

            _selectedRangeCells.Add((cell, placeholder));
        }
    }

    private void ReplaceRangedCells()
    {
        // 選択されているセルの中で、選択されていないセルを削除
        foreach (var (cell, placeholder) in _selectedRangeCells)
        {
            // 選択されているセルを置き換える
            if (placeholder.activeSelf)
            {
                ReplaceCell(cell);
            }

            Destroy(placeholder);
        }

        _selectedRangeCells.Clear();
    }

    /// <summary>
    /// 選択されているセルを新しいセルに置き換える
    /// </summary>
    private void ReplaceCell(CellBase replaceTarget)
    {
        // 置こうとしているセル、および選択されているセルがEmptyCellでない場合、上書きが発生してしまうため中断する
        if (_cachedCell == null || replaceTarget == null ||
            _cachedCell is not EmptyCell && replaceTarget is not EmptyCell)
        {
            Debug.LogWarning("セルの置き換えに失敗しました。セルが選択されているか、適切なPrefabが割り当てられているか確認してください。");
            return;
        }

        // 選択されているセルの情報を取得
        var x = replaceTarget.XIndex;
        var z = replaceTarget.ZIndex;
        var objName = replaceTarget.name;
        var pos = replaceTarget.transform.position;
        var parent = replaceTarget.transform.parent;
        var index = replaceTarget.transform.GetSiblingIndex();

        if (replaceTarget is ConnectableCellBase connectableCell)
        {
            connectableCell.OnDisconnect();
        }

        // セルを削除
        Destroy(replaceTarget.gameObject);

        // 新しいセルを生成
        var newCell = Instantiate(_cachedCell, pos, transform.rotation, parent);
        newCell.transform.SetSiblingIndex(index);
        newCell.name = objName;

        // 新しいセルの情報を保存
        GridFieldDatabase.Instance.SaveCell(x, z, newCell);
        newCell.gameObject.SetActive(true);
        newCell.InitializeSystem();

        if (_cachedCell is not EmptyCell)
        {
            CellStatusView.Instance.UpdateUIStatusWindow(newCell);
        }
        else
        {
            CellStatusView.Instance.SetStatusWindowActive(false);
        }

        if (_selectedCell == replaceTarget)
        {
            _selectedCell = newCell;
        }
    }

    public void SetSelectedCellType(CellType cellType)
    {
        foreach (var database in cellDatabaseArr)
        {
            if (!database.TryGetCellInfo(cellType, out var cellInfo)) continue;

            _selectedCellType = cellInfo.CellType;
            Destroy(_placeholderCell);
            _placeholderCell = Instantiate(cellInfo.PlaceholderCellPrefab,
                transform.position, transform.rotation, transform);

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
}
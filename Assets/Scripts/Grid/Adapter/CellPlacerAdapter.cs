using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class CellPlacerAdapter : MonoBehaviour
{
    [SerializeField] private CellDatabaseDefinition[] cellDatabaseArr;
    private CellBaseAdapter _selectedCell;
    private CellBaseAdapter _cachedCell;
    private GameObject _placeholderCell;
    private readonly HashSet<(CellBaseAdapter cell, GameObject placeholder)> _selectedRangeCells = new();
    private Vector3Int _dragBeginPos;
    private Vector3Int _dragEndPos;
    private CellTypeDomain _selectedType;
    private EquipmentPlacementApplication _placement;
    private CurrencyPriceCatalogDomain _prices;
    private bool _placing;
    private CurrencyWalletApplication _wallet;
    public CellTypeDomain SelectedType => _selectedType;
    public int PreviewCount => _selectedRangeCells.Count(t => t.placeholder.activeSelf);
    public long PreviewCost => _prices != null && _prices.TryGetPlacementAmount(_selectedType, PreviewCount, out var cost) ? cost : 0;
    public int LastPlacedCount { get; private set; }
    public long LastPlacementCost { get; private set; }
    public EquipmentPlacementApplication.Result LastPlacementResult { get; private set; }

    public void Configure(CurrencyWalletApplication wallet, CurrencyPriceCatalogDomain prices)
    {
        if (_placing) throw new InvalidOperationException("Cannot configure placement during a transaction.");
        _prices = prices;
        _wallet = wallet;
        _placement = new EquipmentPlacementApplication(wallet, prices);
    }

    private void Start() => SetSelectedCellType(CellTypeDomain.Empty);

    public void PointerBegin()
    {
        if (_placing || _selectedCell == null) return;
        _dragBeginPos = Vector3Int.RoundToInt(_selectedCell.transform.position);
        _dragEndPos = _dragBeginPos;
    }

    public void PointerDrag()
    {
        if (_placing || _selectedCell == null) return;
        _dragEndPos = Vector3Int.RoundToInt(_selectedCell.transform.position);
        FindRangedGrid(_dragBeginPos, _dragEndPos);
    }

    public void CancelPlacementPreview() { if (!_placing) ClearRange(); }
    public void PointerEnd() => ReplaceRangedCells();
    public void TransferDataToUI() => CellStatusViewAdapter.Instance.UpdateUIStatusWindow(_selectedCell);

    /// <summary>
    /// カーソルが選択されているセルの位置に移動し、セルを選択する
    /// </summary>
    /// <param name="target"></param>
    public void SelectGrid(GameObject target)
    {
        if (_placing || target == null) return;

        // 直前に選択されていたオブジェクトがある場合、その色を元に戻す
        if (_selectedCell != null)
        {
            // 直前に選択されていたオブジェクトと同じ場合は何もしない
            if (target == _selectedCell.gameObject) return;
            _selectedCell.CellModel.SetActive(true);
        }

        if (!target.TryGetComponent<CellBaseAdapter>(out var cellBase)) return;
        _selectedCell = cellBase;
        transform.position = _selectedCell.transform.position;

        if (cellBase is not EmptyCellAdapter) return;
        _selectedCell.CellModel.SetActive(false);
    }

    private void FindRangedGrid(Vector3Int from, Vector3Int to)
    {
        var newSelection = new HashSet<CellBaseAdapter>();
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

            if (!GridFieldDatabaseAdapter.Instance.IsWithinBounds(x, z)) continue;
            var cell = GridFieldDatabaseAdapter.Instance.GetCell(x, z);
            if (cell == null) continue;
            // 撤去は設備だけ、設置は空セルだけを対象にする。
            if (cell is not ResourceCellAdapter && cell is not SingularityCellAdapter &&
                (_cachedCell is EmptyCellAdapter && cell is not EmptyCellAdapter ||
                 _cachedCell is not EmptyCellAdapter && cell is EmptyCellAdapter))
            {
                // 置き換え可能なセルを選択
                newSelection.Add(cell);
            }
        }

        SelectRangedGrid(newSelection);
    }

    private void SelectRangedGrid(HashSet<CellBaseAdapter> newSelection)
    {
        var newSet = new HashSet<CellBaseAdapter>(newSelection);
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
        if (_placing || _selectedRangeCells.Count == 0) return;
        _placing = true;
        LastPlacedCount = 0;
        LastPlacementCost = 0;
        LastPlacementResult = EquipmentPlacementApplication.Result.Success;
        try
        {
            // HashSetの列挙順に依存せず、ドラッグ開始から終端へ一列で確定する。
            var direction = _dragEndPos - _dragBeginPos;
            var isX = Mathf.Abs(direction.x) >= Mathf.Abs(direction.z);
            var sign = (isX ? direction.x : direction.z) < 0 ? -1 : 1;
            var targets = _selectedRangeCells.Where(t => t.placeholder.activeSelf)
                .OrderBy(t => sign * (isX ? t.cell.XIndex : t.cell.ZIndex)).ToArray();
            foreach (var (cell, _) in targets)
            {
                LastPlacementResult = TryReplaceCell(cell, out var cost);
                if (LastPlacementResult != EquipmentPlacementApplication.Result.Success) break;
                LastPlacedCount++;
                LastPlacementCost = checked(LastPlacementCost + cost);
            }
        }
        finally { ClearRange(); _placing = false; }
    }

    private void ClearRange()
    {
        foreach (var (cell, placeholder) in _selectedRangeCells)
        {
            if (cell != null) cell.CellModel.SetActive(true);
            if (placeholder != null) Destroy(placeholder);
        }
        _selectedRangeCells.Clear();
    }

    // 既存の直接呼出しも同じ取引を通す。
    private void ReplaceCell(CellBaseAdapter replaceTarget)
    {
        if (_placing) return;
        _placing = true;
        try
        {
            LastPlacementResult = TryReplaceCell(replaceTarget, out var cost);
            LastPlacedCount = LastPlacementResult == EquipmentPlacementApplication.Result.Success ? 1 : 0;
            LastPlacementCost = LastPlacedCount == 1 ? cost : 0;
        }
        finally { _placing = false; }
    }

    private EquipmentPlacementApplication.Result TryReplaceCell(CellBaseAdapter replaceTarget, out long cost)
    {
        cost = 0;
        if (_placement == null || replaceTarget == null) return EquipmentPlacementApplication.Result.InvalidTarget;
        var x = replaceTarget.XIndex;
        var z = replaceTarget.ZIndex;
        var grid = GridFieldDatabaseAdapter.Instance;
        CellBaseAdapter newCell = null;
        bool Validate() => this != null && isActiveAndEnabled && replaceTarget != null &&
            replaceTarget.isActiveAndEnabled && _cachedCell != null &&
            replaceTarget is not ResourceCellAdapter && replaceTarget is not SingularityCellAdapter &&
            _cachedCell is not ResourceCellAdapter && _cachedCell is not SingularityCellAdapter &&
            (_cachedCell is EmptyCellAdapter ? replaceTarget is not EmptyCellAdapter : replaceTarget is EmptyCellAdapter) &&
            grid.IsWithinBounds(x, z) && grid.GetCell(x, z) == replaceTarget;

        var result = _placement.Execute(_selectedType, Validate, () =>
        {
            // 非アクティブな雛形から準備し、旧セルは初期化成功まで保持する。
            newCell = Instantiate(_cachedCell, replaceTarget.transform.position, transform.rotation, replaceTarget.transform.parent);
            newCell.name = replaceTarget.name;
            newCell.transform.SetSiblingIndex(replaceTarget.transform.GetSiblingIndex());
            grid.SaveCell(x, z, newCell);
            if (grid.GetCell(x, z) != newCell) throw new InvalidOperationException("Cell registration failed.");
            newCell.gameObject.SetActive(true);
            newCell.InitializeSystem();
            if (newCell == null || !newCell.isActiveAndEnabled || grid.GetCell(x, z) != newCell)
                throw new InvalidOperationException("Cell initialization changed the registration.");
        }, () =>
        {
            try { if (newCell is ConnectableCellBaseAdapter connected) connected.OnDisconnect(); }
            finally
            {
                grid.SaveCell(x, z, replaceTarget);
                if (newCell != null) { newCell.gameObject.SetActive(false); Destroy(newCell.gameObject); }
            }
        }, () =>
        {
            // 退役処理の例外でも、成立した設置と支出の対応を保つ。
            try { if (replaceTarget is ConnectableCellBaseAdapter connected) connected.OnDisconnect(); }
            catch (Exception exception) { Debug.LogException(exception); }
            replaceTarget.gameObject.SetActive(false);
            Destroy(replaceTarget.gameObject);
            if (_selectedCell == replaceTarget) _selectedCell = newCell;
        }, out cost);

        if (result == EquipmentPlacementApplication.Result.Success)
        {
            try
            {
                if (_cachedCell is EmptyCellAdapter) CellStatusViewAdapter.Instance.SetStatusWindowActive(false);
                else CellStatusViewAdapter.Instance.UpdateUIStatusWindow(newCell);
            }
            catch (Exception exception) { Debug.LogException(exception); }
        }
        return result;
    }

    public void SetSelectedCellType(CellTypeDomain cellType)
    {
        if (_placing || cellType == CellTypeDomain.Singularity ||
            cellType == CellTypeDomain.ResourceWood || cellType == CellTypeDomain.ResourceStone ||
            cellType == CellTypeDomain.ResourceIron) return;
        foreach (var database in cellDatabaseArr)
        {
            if (!database.TryGetCellInfo(cellType, out var cellInfo)) continue;
            ClearRange();
            _selectedType = cellType;

            // _selectedCellType = cellInfo.CellTypeDomain;
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

        // Debug.LogWarning($"CellTypeDomain {_selectedCellType} の情報が見つかりません。");
    }

    private void OnDisable() { if (!_placing) ClearRange(); }

    public string PreviewText
    {
        get
        {
            if (_selectedType == CellTypeDomain.Empty) return $"撤去予定 {PreviewCount}個 / 無料・返金なし";
            if (_prices == null || !_prices.TryGetPlacementAmount(_selectedType, 1, out var unit)) return "設置価格が無効です";
            return $"{_selectedType} 単価 {unit}\n設置予定 {PreviewCount}個 / 費用 {PreviewCost}" +
                (PreviewCost > (_wallet?.Current.AvailableBalance ?? 0) ? " / 残高不足で途中停止" : "");
        }
    }
    public string ResultText => $"確定 {LastPlacedCount}個 / 支出 {LastPlacementCost}" +
        (LastPlacementResult == EquipmentPlacementApplication.Result.InsufficientFunds ? " / 残高不足" :
         LastPlacementResult == EquipmentPlacementApplication.Result.Success ? "" :
         LastPlacementResult == EquipmentPlacementApplication.Result.InvalidTarget ? " / 設置先または価格が無効です" :
         LastPlacementResult == EquipmentPlacementApplication.Result.Busy ? " / 設置処理中" : " / 設置に失敗しました");
}

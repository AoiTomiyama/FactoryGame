using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class CellPlacerAdapter : MonoBehaviour
{
    [SerializeField] private CellDatabaseDefinition[] cellDatabaseArr;
    private CellBaseAdapter _selectedCell;
    private Vector3Int _selectedCoordinate;
    private CellBaseAdapter _cachedCell;
    private GameObject _placeholderCell;
    private readonly HashSet<(CellBaseAdapter cell, GameObject placeholder)> _selectedRangeCells = new();
    private readonly HashSet<CellBaseAdapter> _previewHiddenOwners = new();
    private Vector3Int _dragBeginPos;
    private Vector3Int _dragEndPos;
    private CellTypeDomain _selectedType;
    private EquipmentPlacementApplication _placement;
    private CurrencyPriceCatalogDomain _prices;
    private bool _placing;
    private bool _placementEnabled, _dragging, _overlap;
    private string _placementNotice = "";
    public string PlacementNotice => _overlap ? "既存セルと重なっています" : _placementNotice;
    public bool PlacementEnabled => _placementEnabled;
    public void SetPlacementEnabled(bool enabled)
    {
        _placementEnabled = enabled;
        // タブを離れたドラッグは取消し、戻った後のマウス解放で確定させない。
        if (!enabled) { _dragging = false; CancelPlacementPreview(); }
        if (_placeholderCell != null) _placeholderCell.SetActive(enabled);
        if (_selectedCell != null) _selectedCell.CellModel.SetActive(!enabled || _selectedCell is not EmptyCellAdapter);
    }
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
        if (!_placementEnabled || _placing || _selectedCell == null) return;
        _placementNotice = "";
        _dragging = true;
        _dragBeginPos = _selectedCoordinate;
        _dragEndPos = _dragBeginPos;
    }

    public void PointerDrag()
    {
        if (!_placementEnabled || !_dragging || _placing || _selectedCell == null) return;
        _dragEndPos = _selectedCoordinate;
        FindRangedGrid(_dragBeginPos, _dragEndPos);
    }

    public void CancelPlacementPreview() { _dragging = false; _placementNotice = ""; if (!_placing) ClearRange(); }
    public void PointerEnd()
    {
        if (!_placementEnabled || !_dragging) return;
        _placementNotice = _overlap ? "既存セルと重なっています" : "";
        _dragging = false; ReplaceRangedCells();
    }
    public void TransferDataToUI() => CellStatusViewAdapter.Instance.UpdateUIStatusWindow(_selectedCell);

    /// <summary>
    /// カーソルが選択されているセルの位置に移動し、セルを選択する
    /// </summary>
    /// <param name="target"></param>
    public void SelectGrid(GameObject target, Vector3? hitPoint = null)
    {
        if (_placing || target == null) return;

        // 直前に選択されていたオブジェクトがある場合、その色を元に戻す
        if (_selectedCell != null)
        {
            // 直前に選択されていたオブジェクトと同じ場合は何もしない
            _selectedCell.CellModel.SetActive(true);
        }

        var cellBase = target.GetComponentInParent<CellBaseAdapter>();
        if (cellBase == null) return;
        _selectedCell = cellBase;
        _selectedCoordinate = Vector3Int.RoundToInt(hitPoint ?? cellBase.transform.position);
        _selectedCoordinate.y = 0;
        if (!cellBase.OccupiedCoordinates.Contains((_selectedCoordinate.x, _selectedCoordinate.z)))
            _selectedCoordinate = new Vector3Int(cellBase.XIndex, 0, cellBase.ZIndex);
        transform.position = _selectedCoordinate;

        if (!_placementEnabled || cellBase is not EmptyCellAdapter) return;
        _selectedCell.CellModel.SetActive(false);
    }

    private void FindRangedGrid(Vector3Int from, Vector3Int to)
    {
        _overlap = false;
        var newSelection = new HashSet<CellBaseAdapter>();
        // 複数マス設備はカーソル位置へ1台ずつ置く。1マス設備の連続配置は維持する。
        if (_cachedCell is not EmptyCellAdapter && _cachedCell.Footprint.Area > 1) from = to;
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
            if (_cachedCell is not EmptyCellAdapter && !CanPlaceAt(x, z)) { _overlap = true; continue; }
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
        RestorePreviewModels();
        var newSet = new HashSet<CellBaseAdapter>(newSelection);
        var oldSet = _selectedRangeCells
            .ToDictionary(t => t.cell, t => t.placeholder);

        // 既存の選択範囲から新しい選択範囲に含まれないセルを削除
        foreach (var (cell, placeholder) in _selectedRangeCells)
        {
            if (newSet.Contains(cell)) continue;

            placeholder.SetActive(false);
        }


        // 新しい選択範囲に含まれるセルを追加
        foreach (var cell in newSelection)
        {
            HidePreviewModels(cell);
            if (!oldSet.TryGetValue(cell, out var placeholder))
            {
                placeholder = Instantiate(_placeholderCell, cell.transform.position, transform.rotation);
            }
            else
            {
                placeholder.SetActive(true);
            }
            ConfigurePreview(placeholder, cell);
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
        // プレイヤーが修正できる不足と重なりはHUDへ、処理上の失敗はConsoleへ分ける。
        if (LastPlacementResult == EquipmentPlacementApplication.Result.InsufficientFunds) _placementNotice = "残高不足";
        else if (LastPlacementResult != EquipmentPlacementApplication.Result.Success) Debug.LogWarning($"配置失敗：{FailureText}", this);
    }

    private void ClearRange()
    {
        RestorePreviewModels();
        foreach (var (cell, placeholder) in _selectedRangeCells)
        {
            if (placeholder != null) Destroy(placeholder);
        }
        _selectedRangeCells.Clear();
        _overlap = false;
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

    private bool CanPlaceAt(int x, int z)
    {
        var grid = GridFieldDatabaseAdapter.Instance;
        return _cachedCell.Footprint.Coordinates(x, z, Mathf.RoundToInt(transform.eulerAngles.y / 90f))
            .All(p => grid.IsWithinBounds(p.x, p.z) && grid.GetCell(p.x, p.z) is EmptyCellAdapter);
    }

    private void HidePreviewModels(CellBaseAdapter target)
    {
        var grid = GridFieldDatabaseAdapter.Instance;
        var points = _cachedCell is EmptyCellAdapter ? target.OccupiedCoordinates :
            _cachedCell.Footprint.Coordinates(target.XIndex, target.ZIndex, Mathf.RoundToInt(transform.eulerAngles.y / 90f));
        foreach (var point in points)
            if (grid.IsWithinBounds(point.x, point.z) && grid.GetCell(point.x, point.z) is { } owner)
            {
                owner.CellModel.SetActive(false);
                _previewHiddenOwners.Add(owner);
            }
    }

    private void RestorePreviewModels()
    {
        foreach (var owner in _previewHiddenOwners)
            if (owner != null) owner.CellModel.SetActive(true);
        _previewHiddenOwners.Clear();
    }

    private void ConfigurePreview(GameObject preview, CellBaseAdapter target)
    {
        var removing = _cachedCell is EmptyCellAdapter && target != null;
        var shape = removing ? target.Footprint : _cachedCell.Footprint;
        var rotation = removing ? target.transform.rotation : transform.rotation;
        var anchor = target != null ? target.transform.position : transform.position;
        preview.transform.SetPositionAndRotation(anchor + rotation * new Vector3(
            shape.OffsetX + (shape.Width - 1) * .5f, 0, shape.OffsetZ + (shape.Depth - 1) * .5f), rotation);
        // 寸法の編集元をField Prefabへ集約し、Placeholder側の重複設定を作らない。
        preview.transform.localScale = new Vector3(shape.Width, 1, shape.Depth);
    }

    private EquipmentPlacementApplication.Result TryReplaceCell(CellBaseAdapter replaceTarget, out long cost)
    {
        cost = 0;
        if (_placement == null || replaceTarget == null || _cachedCell == null)
            return EquipmentPlacementApplication.Result.InvalidTarget;
        var removing = _cachedCell is EmptyCellAdapter;
        var x = replaceTarget.XIndex;
        var z = replaceTarget.ZIndex;
        var grid = GridFieldDatabaseAdapter.Instance;
        var points = (removing ? replaceTarget.OccupiedCoordinates : _cachedCell.Footprint.Coordinates(x, z,
            Mathf.RoundToInt(transform.eulerAngles.y / 90f))).ToArray();
        if (points.Any(p => !grid.IsWithinBounds(p.x, p.z))) return EquipmentPlacementApplication.Result.InvalidTarget;
        var previous = grid.CaptureCells(points);
        var retired = previous.Select(e => e.Owner).Distinct().ToArray();
        var created = new List<CellBaseAdapter>();
        bool Validate() => this != null && isActiveAndEnabled && replaceTarget != null &&
            replaceTarget.isActiveAndEnabled && _cachedCell != null &&
            replaceTarget is not ResourceCellAdapter && replaceTarget is not SingularityCellAdapter &&
            _cachedCell is not ResourceCellAdapter && _cachedCell is not SingularityCellAdapter &&
            (removing ? replaceTarget is not EmptyCellAdapter && retired.All(c => c == replaceTarget) :
                retired.All(c => c is EmptyCellAdapter)) &&
            previous.All(e => grid.GetCell(e.X, e.Z) == e.Owner && e.Owner != null && e.Owner.isActiveAndEnabled);

        var result = _placement.Execute(_selectedType, Validate, () =>
        {
            var anchors = removing ? points : new[] { (x, z) };
            foreach (var point in anchors)
            {
                var cell = Instantiate(_cachedCell, new Vector3(point.Item1, 0, point.Item2),
                    removing ? Quaternion.identity : transform.rotation, grid.transform);
                cell.name = $"Tile_{point.Item1}_{point.Item2}";
                created.Add(cell);
            }
            if (!grid.TryReplaceCells(created.ToArray(), previous)) throw new InvalidOperationException("Cell registration failed.");
            foreach (var cell in created)
            {
                cell.gameObject.SetActive(true);
                cell.InitializeSystem();
            }
            if (created.Any(c => c == null || !c.isActiveAndEnabled ||
                    c.OccupiedCoordinates.Any(p => grid.GetCell(p.x, p.z) != c)))
                throw new InvalidOperationException("Cell initialization changed the registration.");
        }, () =>
        {
            try
            {
                foreach (var cell in created)
                    if (cell is ConnectableCellBaseAdapter connected) connected.OnDisconnect();
            }
            finally
            {
                grid.RestoreCells(previous);
                foreach (var cell in created)
                    if (cell != null) { cell.gameObject.SetActive(false); Destroy(cell.gameObject); }
            }
        }, () =>
        {
            foreach (var old in retired)
            {
                if (old == null) continue;
                try { if (old is ConnectableCellBaseAdapter connected) connected.OnDisconnect(); }
                catch (Exception exception) { Debug.LogException(exception); }
                old.gameObject.SetActive(false);
                Destroy(old.gameObject);
            }
            if (retired.Contains(_selectedCell))
                _selectedCell = grid.GetCell(_selectedCoordinate.x, _selectedCoordinate.z);
        }, out cost);

        if (result == EquipmentPlacementApplication.Result.Success)
        {
            try
            {
                if (removing) CellStatusViewAdapter.Instance.SetStatusWindowActive(false);
                else CellStatusViewAdapter.Instance.UpdateUIStatusWindow(created[0]);
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
            _placementNotice = "";
            _selectedType = cellType;

            // _selectedCellType = cellInfo.CellTypeDomain;
            Destroy(_placeholderCell);
            _placeholderCell = Instantiate(cellInfo.PlaceholderCellPrefab,
                transform.position, transform.rotation, transform);

            if (_cachedCell != null)
            {
                Destroy(_cachedCell.gameObject);
            }

            _placeholderCell.SetActive(_placementEnabled);
            _cachedCell = Instantiate(cellInfo.FieldCellPrefab, transform.position, transform.rotation, transform);
            _cachedCell.gameObject.SetActive(false);
            ConfigurePreview(_placeholderCell, null);
            return;
        }

        // Debug.LogWarning($"CellTypeDomain {_selectedCellType} の情報が見つかりません。");
    }

    private void OnDisable() { if (!_placing) ClearRange(); }

    public string PreviewText
    {
        get
        {
            if (_selectedType == CellTypeDomain.Empty) return PreviewCount > 0 ? $"撤去予定 {PreviewCount}個 / 無料・返金なし" : "撤去 / 無料・返金なし";
            if (_prices == null || !_prices.TryGetPlacementAmount(_selectedType, 1, out var unit)) return "設置価格が無効です";
            if (PreviewCount == 0) return $"{_selectedType} 単価 {unit}";
            return $"{_selectedType} 単価 {unit}\n設置予定 {PreviewCount}個 / 費用 {PreviewCost}" +
                (PreviewCost > (_wallet?.Current.AvailableBalance ?? 0) ? " / 残高不足で途中停止" : "");
        }
    }
    public string FailureText => LastPlacementResult switch
    {
        EquipmentPlacementApplication.Result.InsufficientFunds => "残高不足",
        EquipmentPlacementApplication.Result.InvalidTarget => "設置先または価格が無効です",
        EquipmentPlacementApplication.Result.Busy => "設置処理中",
        EquipmentPlacementApplication.Result.Failed => "設置に失敗しました",
        _ => ""
    };
    public string ResultText => $"確定 {LastPlacedCount}個 / 支出 {LastPlacementCost}" +
        (string.IsNullOrEmpty(FailureText) ? "" : " / " + FailureText);
}

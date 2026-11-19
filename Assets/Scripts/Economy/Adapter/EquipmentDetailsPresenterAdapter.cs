using UnityEngine;

/// <summary>選択セルの状態を読み、設備詳細へ表示する。</summary>
public sealed class EquipmentDetailsPresenterAdapter
{
    private readonly EquipmentDetailsViewAdapter _view;
    public EquipmentDetailsPresenterAdapter(EquipmentDetailsViewAdapter view) => _view = view;
    public void Refresh(CellBaseAdapter selected)
    {
        _view.Feedback = "";
        _view.Heading.gameObject.SetActive(selected != null);
        var state = selected == null ? "" : !selected.isActiveAndEnabled ? "停止中" :
            selected is CrafterCellAdapter crafter ? crafter.ProcessTime > 0 ? "加工中" : "材料・空き容量を待機" : "";
        _view.Heading.text = selected == null ? "" : $"{CellName(selected)}\n{state}".TrimEnd();
        // セル選択は読み取り専用。設備情報に購入操作を表示しない。
        _view.UpgradeRoot.gameObject.SetActive(false);
        _view.EquipmentText = "";
        _view.UpgradeButton.interactable = false;
        UpdateLayout();
    }
    public void UpdateLayout() => _view.UpdateLayout();
    public void SetActive(bool active) { _view.Heading.gameObject.SetActive(active); _view.UpgradeRoot.gameObject.SetActive(false); }
    public static string CellName(CellBaseAdapter cell) => cell switch
    {
        ExtractorCellAdapter extractor => SelectButtonBuilderAdapter.PlacementLabel(extractor.ProgressionType),
        StorageCellAdapter => "保管庫", CrafterCellAdapter crafter => crafter.ProgressionType == CellTypeDomain.AdvancedCrafter ? "上位加工設備" : "基本加工設備",
        ConveyorCellAdapter conveyor when conveyor.IsExportConveyor => "搬出路", ConveyorCellAdapter => "搬送路", CrossingCellAdapter => "交差搬送路",
        ConveyorJunctionCellAdapter junction when junction.LocalRoute.Type == ConveyorRouteRulesDomain.Kind.Merge => "合流搬送路",
        ConveyorJunctionCellAdapter => "分岐搬送路",
        ConstructionDepotCellAdapter => "建設資材庫", SingularityCellAdapter => "特異点", ShippingCellAdapter => "出荷セル", ResourceCellAdapter => "資源", _ => "セル"
    };
}

using System;
using UnityEngine;
using UnityEngine.Events;

/// <summary>選択セルの状態を読み、スキルと共用する詳細枠の適用リスナーを保持する。</summary>
public sealed class EquipmentDetailsPresenterAdapter : IDisposable
{
    private readonly EquipmentDetailsViewAdapter _view;
    private readonly UnityAction _apply;
    public EquipmentDetailsPresenterAdapter(EquipmentDetailsViewAdapter view, UnityAction apply)
    {
        _view = view; _apply = apply;
        _view.UpgradeButton.onClick.AddListener(_apply);
    }
    public void Refresh(CellBaseAdapter selected)
    {
        _view.Feedback = "";
        _view.Heading.gameObject.SetActive(selected != null);
        var state = selected == null ? "" : !selected.isActiveAndEnabled ? "停止中" :
            selected is CrafterCellAdapter crafter ? crafter.ProcessTime > 0 ? "加工中" : "材料・空き容量を待機" : "";
        _view.Heading.text = selected == null ? "" : $"{CellName(selected)}\n{state}".TrimEnd();
        // セル選択は読み取り専用。購入ボタンはスキルを選択したときだけ表示する。
        _view.UpgradeRoot.gameObject.SetActive(false);
        _view.EquipmentText = "";
        _view.UpgradeButton.interactable = false;
        UpdateLayout();
    }
    public void UpdateLayout() => _view.UpdateLayout();
    public void SetActive(bool active) { _view.Heading.gameObject.SetActive(active); _view.UpgradeRoot.gameObject.SetActive(false); }
    public void Dispose() { if (_view.UpgradeButton != null) _view.UpgradeButton.onClick.RemoveListener(_apply); }
    public static string CellName(CellBaseAdapter cell) => cell switch
    {
        ExtractorCellAdapter extractor => SelectButtonBuilderAdapter.PlacementLabel(extractor.ProgressionType),
        StorageCellAdapter => "保管庫", CrafterCellAdapter crafter => crafter.ProgressionType == CellTypeDomain.AdvancedCrafter ? "上位加工設備" : "基本加工設備",
        ExportConveyorCellAdapter => "搬出路", ConveyorCellAdapter => "搬送路", CrossingCellAdapter => "交差搬送路",
        ConstructionDepotCellAdapter => "建設資材庫", SingularityCellAdapter => "特異点", ShippingCellAdapter => "出荷セル", ResourceCellAdapter => "資源", _ => "セル"
    };
}

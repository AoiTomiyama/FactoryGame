using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>選択セルの情報と強化見積りを表示し、購入は既存Adapterへ渡す。</summary>
public sealed class EquipmentDetailsPresenterAdapter : IDisposable
{
    private readonly EquipmentUpgradeAdapter _upgrades;
    private readonly EquipmentDetailsViewAdapter _view;
    private CellBaseAdapter _selected;
    private int _quotedLevel;
    public EquipmentDetailsPresenterAdapter(EquipmentDetailsViewAdapter view, EquipmentUpgradeAdapter upgrades)
    {
        _upgrades = upgrades; _view = view;
        _view.UpgradeButton.onClick.AddListener(OnPurchase);
    }
    private void OnPurchase() => Purchase();
    public void Refresh(CellBaseAdapter selected)
    {
        if (_selected != selected) _view.Feedback = "";
        _selected = selected;
        _view.Heading.gameObject.SetActive(selected != null);

        _view.Heading.text = selected == null ? "" : $"{CellName(selected)} ({selected.XIndex}, {selected.ZIndex})　Lv.{selected.EquipmentLevel}\n{(!selected.isActiveAndEnabled ? "停止中" : selected is CrafterCellAdapter crafter ? (crafter.ProcessTime > 0 ? "加工中" : "材料・空き容量を待機") : "稼働中")}";
        _quotedLevel = selected != null ? selected.EquipmentLevel : 0;
        var result = _upgrades.CanPurchase(selected, _quotedLevel, out var cost);
        _view.UpgradeRoot.gameObject.SetActive(selected != null && selected.SupportsProgression);
        _view.EquipmentText = selected != null && selected.SupportsProgression
            ? $"設備 ({selected.XIndex}, {selected.ZIndex})　Lv.{_quotedLevel}/3\n現在: {Stats(selected, _quotedLevel)}\n{(_quotedLevel < EffectiveStatsRulesDomain.MaxLevel ? "強化後: " + Stats(selected, _quotedLevel + 1) : "最大レベル")}\n強化費 {(cost > 0 ? cost.ToString() : "—")}　{UpgradeReason(result)}"
            : "";
        _view.UpgradeButton.interactable = result == EquipmentUpgradeResultApplication.Success;
        UpdateLayout();
    }
    public EquipmentUpgradeResultApplication Purchase()
    {
        var result = _upgrades.Purchase(_selected, _quotedLevel);
        _view.Feedback = result == EquipmentUpgradeResultApplication.Success ? "設備を強化しました" : UpgradeReason(result);
        Refresh(_selected);
        return result;
    }
    public void UpdateLayout() => _view.UpdateLayout();
    public void SetActive(bool active) { _view.Heading.gameObject.SetActive(active); _view.UpgradeRoot.gameObject.SetActive(active && _selected != null && _selected.SupportsProgression); }
    public void Dispose() { if (_view.UpgradeButton != null) _view.UpgradeButton.onClick.RemoveListener(OnPurchase); }
    private static string CellName(CellBaseAdapter cell) => cell switch
    {
        ExtractorCellAdapter => "採取設備", StorageCellAdapter => "保管庫", CrafterCellAdapter => "加工設備",
        ExportConveyorCellAdapter => "搬出路", ConveyorCellAdapter => "搬送路", CrossingCellAdapter => "交差搬送路",
        SingularityCellAdapter => "特異点", ResourceCellAdapter => "資源", _ => "セル"
    };
    private static string Stats(CellBaseAdapter cell, int level) => cell switch
    {
        ExtractorCellAdapter extractor => $"抽出量 {extractor.ExtractionAmountAtLevel(level)}　容量 {extractor.StorageCapacityAtLevel(level)}",
        ConveyorCellAdapter conveyor => $"搬送量 {conveyor.TransferAmountAtLevel(level)}",
        StorageCellAdapter storage => $"容量 {storage.CapacityAtLevel(level)}",
        CrafterCellAdapter crafter => $"入力容量 {crafter.IngredientCapacityAtLevel(level)}　出力容量 {crafter.ExporterCapacityAtLevel(level)}",
        CrossingCellAdapter => "接続元の搬送量を使用", _ => ""
    };
    public static string UpgradeReason(EquipmentUpgradeResultApplication result) => result switch
    {
        EquipmentUpgradeResultApplication.Success => "強化できます",
        EquipmentUpgradeResultApplication.Busy => "購入処理中",
        EquipmentUpgradeResultApplication.InvalidTarget => "対象設備がありません",
        EquipmentUpgradeResultApplication.LevelChanged => "レベルが変わりました。選択し直してください",
        EquipmentUpgradeResultApplication.MaxLevel => "最大レベル",
        EquipmentUpgradeResultApplication.InvalidPrice => "価格が無効です",
        EquipmentUpgradeResultApplication.InsufficientFunds => "残高不足",
        _ => "強化に失敗しました"
    };
}

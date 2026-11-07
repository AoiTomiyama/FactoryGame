using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>選択セルの情報と強化見積りを表示し、購入は既存Adapterへ渡す。</summary>
public sealed class EquipmentDetailsViewAdapter : IDisposable
{
    private readonly EquipmentUpgradeAdapter _upgrades;
    private readonly Text _heading, _equipment, _feedback;
    private readonly Button _button;
    private readonly GameObject _group;
    private CellBaseAdapter _selected;
    private int _quotedLevel;
    public string EquipmentText => _equipment.text;
    public bool Interactable => _button.interactable;
    public Transform UpgradeRoot => _group.transform;
    public Text Heading => _heading;
    public Button UpgradeButton => _button;
    public EquipmentDetailsViewAdapter(Transform parent, Font font, EquipmentUpgradeAdapter upgrades)
    {
        _upgrades = upgrades;
        _heading = HudUIAdapter.MakeText(font, parent, "", 18, 56);
        _group = new GameObject("EquipmentUpgrade", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        _group.transform.SetParent(parent, false);
        var layout = _group.GetComponent<VerticalLayoutGroup>();
        layout.spacing = 8; layout.childControlHeight = true; layout.childForceExpandHeight = false;
        _group.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        _equipment = HudUIAdapter.MakeText(font, _group.transform, "", 16, 104);
        _button = HudUIAdapter.MakeButton(font, _group.transform, "選択設備を強化", () => Purchase(), 36);
        _feedback = HudUIAdapter.MakeText(font, _group.transform, "", 14, 24);
    }
    public void Refresh(CellBaseAdapter selected)
    {
        if (_selected != selected) _feedback.text = "";
        _selected = selected;
        _heading.gameObject.SetActive(selected != null);
        _heading.text = selected == null ? "" : $"{CellName(selected)} ({selected.XIndex}, {selected.ZIndex})　Lv.{selected.EquipmentLevel}\n{(!selected.isActiveAndEnabled ? "停止中" : selected is CrafterCellAdapter crafter ? (crafter.ProcessTime > 0 ? "加工中" : "材料・空き容量を待機") : "稼働中")}";
        _quotedLevel = selected != null ? selected.EquipmentLevel : 0;
        var result = _upgrades.CanPurchase(selected, _quotedLevel, out var cost);
        _group.SetActive(selected != null && selected.SupportsProgression);
        _equipment.text = selected != null && selected.SupportsProgression
            ? $"設備 ({selected.XIndex}, {selected.ZIndex})　Lv.{_quotedLevel}/3\n現在: {Stats(selected, _quotedLevel)}\n{(_quotedLevel < EffectiveStatsRulesDomain.MaxLevel ? "強化後: " + Stats(selected, _quotedLevel + 1) : "最大レベル")}\n強化費 {(cost > 0 ? cost.ToString() : "—")}　{UpgradeReason(result)}"
            : "";
        _button.interactable = result == EquipmentUpgradeResultApplication.Success;
        UpdateLayout();
    }
    public EquipmentUpgradeResultApplication Purchase()
    {
        var result = _upgrades.Purchase(_selected, _quotedLevel);
        _feedback.text = result == EquipmentUpgradeResultApplication.Success ? "設備を強化しました" : UpgradeReason(result);
        Refresh(_selected);
        return result;
    }
    public void UpdateLayout()
    {
        // 状態行の生成後も、見出し→在庫・状態→強化の順を保つ。
        _heading.transform.SetAsFirstSibling();
        _group.transform.SetAsLastSibling();
        foreach (var text in new[] { _heading, _equipment, _feedback })
            text.GetComponent<LayoutElement>().preferredHeight = Mathf.Max(text.GetComponent<LayoutElement>().minHeight, text.preferredHeight);
    }
    public void SetActive(bool active) { _heading.gameObject.SetActive(active); _group.SetActive(active && _selected != null && _selected.SupportsProgression); }
    public void Dispose() { UnityEngine.Object.Destroy(_heading.gameObject); UnityEngine.Object.Destroy(_group); }
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

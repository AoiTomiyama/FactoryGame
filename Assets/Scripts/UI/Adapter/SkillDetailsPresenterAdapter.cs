using System;
using System.Linq;
using UnityEngine;

/// <summary>スキルの詳細と購入操作を全画面の表示部品へ接続する。</summary>
public sealed class SkillDetailsPresenterAdapter : IDisposable
{
    private readonly SkillTreeApplication _skills;
    private readonly SharedStatEffectsApplication _effects;
    private readonly DeliveryProgressApplication _deliveries;
    private readonly EquipmentDetailsViewAdapter _view;
    private readonly Func<string> _clearingPrices;
    public string SelectedUid { get; private set; }
    public SkillDetailsPresenterAdapter(SkillTreeApplication skills, SharedStatEffectsApplication effects,
        DeliveryProgressApplication deliveries, EquipmentDetailsViewAdapter view, Func<string> clearingPrices)
    {
        _skills = skills; _effects = effects; _deliveries = deliveries; _view = view; _clearingPrices = clearingPrices;
        _view.UpgradeButton.onClick.AddListener(Apply);
    }
    private void Apply() => PurchaseSelected();
    public void Select(string uid) { SelectedUid = uid; Refresh(); }
    public void Refresh()
    {
        var selected = SelectedUid != null && _skills.Catalog.TryGet(SelectedUid, out _);
        _view.Heading.gameObject.SetActive(selected); _view.UpgradeRoot.gameObject.SetActive(selected);
        if (!selected) return;
        _skills.Catalog.TryGet(SelectedUid, out var node);
        var state = _skills.CanPurchase(node.Uid);
        _view.Heading.text = node.Label;
        _view.EquipmentText = node.Uid == TerrainClearingApplication.SkillUid ? "木・石を整地して空き地にする\n" + _clearingPrices() :
            node.TargetCellTypes.Count > 0 ? EffectText(node) : "能力別の強化を解放";
        _view.Feedback = state == SkillPurchaseResultApplication.PrerequisiteMissing && _skills.Catalog.TryGet(node.Prerequisite, out var parent) ? parent.Label + "が必要" :
            state == SkillPurchaseResultApplication.DeliveryStageMissing ? _deliveries.Catalog.Stages.First(s => s.Id == node.RequiredDeliveryStage).Label + "の達成が必要" :
            state == SkillPurchaseResultApplication.InsufficientFunds ? "残高不足" : "";
        _view.UpgradeButton.GetComponentInChildren<UnityEngine.UI.Text>(true).text = _skills.IsOwned(node.Uid) ? "適用済み" : $"適用する　{node.Price}";
        _view.UpgradeButton.interactable = state == SkillPurchaseResultApplication.Success;
    }
    public SkillPurchaseResultApplication PurchaseSelected()
    {
        if (SelectedUid == null) return SkillPurchaseResultApplication.UnknownNode;
        var result = _skills.Purchase(SelectedUid);
        if (result is SkillPurchaseResultApplication.EffectsChanged or SkillPurchaseResultApplication.InvalidEffect or SkillPurchaseResultApplication.UnknownNode)
            Debug.LogError($"Skill purchase failed ({SelectedUid}): {result}");
        Refresh(); return result;
    }
    public static string StatName(EquipmentStatDomain stat) => stat switch
    {
        EquipmentStatDomain.ExtractionAmount => "抽出量", EquipmentStatDomain.Capacity => "容量",
        EquipmentStatDomain.InputCapacity => "入力容量", EquipmentStatDomain.OutputCapacity => "出力容量",
        EquipmentStatDomain.CraftingSpeed => "加工速度", EquipmentStatDomain.TransportSpeed => "輸送速度", _ => "搬出量"
    };
    private string EffectText(SkillNodeDomain node)
    {
        string Value(StatModifierDomain modifier) => $"×{modifier.Multiplier:0.###}" + (modifier.Addition == 0 ? "" : $" ＋{modifier.Addition}");
        var current = _effects.GetModifier(node.TargetCellTypes[0],node.Stat);
        return StatName(node.Stat) + " " + (_skills.IsOwned(node.Uid) ? Value(node.Modifier) : Value(current)+" → "+Value(node.Modifier)) + (node.TargetCellTypes.Count > 1 ? "\n全搬送路に適用" : "\n同種セルすべてに適用");
    }
    public void Dispose() { if (_view.UpgradeButton != null) _view.UpgradeButton.onClick.RemoveListener(Apply); }
}

using System;
using System.Collections.Generic;

public enum SkillPurchaseResultApplication { Success, Busy, UnknownNode, AlreadyOwned, PrerequisiteMissing, InsufficientFunds, EffectsChanged, InvalidEffect }

/// <summary>一プレイの取得状態と共有残高の支出をまとめて確定する。</summary>
public sealed class SkillTreeApplication
{
    private readonly CurrencyWalletApplication _wallet;
    private readonly SharedStatEffectsApplication _effects;
    private readonly HashSet<string> _owned = new(StringComparer.Ordinal);
    private readonly Dictionary<(CellTypeDomain, EquipmentStatDomain), StatModifierDomain> _modifiers = new();
    private bool _purchasing;
    public SkillTreeCatalogDomain Catalog { get; }
    public event Action Changed;
    public Exception LastNotificationException { get; private set; }

    public SkillTreeApplication(SkillTreeCatalogDomain catalog, CurrencyWalletApplication wallet, SharedStatEffectsApplication effects)
    {
        Catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _wallet = wallet ?? throw new ArgumentNullException(nameof(wallet));
        _effects = effects ?? throw new ArgumentNullException(nameof(effects));
        if (!EffectsMatch()) throw new ArgumentException("Skill tree requires fresh shared effects.");
    }

    public bool IsOwned(string uid) => uid != null && _owned.Contains(uid);

    // 通知中も確定済みの取得条件を返し、UIが次の操作可否を読み取れるようにする。
    public SkillPurchaseResultApplication CanPurchase(string uid) => Check(uid);

    private SkillPurchaseResultApplication Check(string uid)
    {
        if (!Catalog.TryGet(uid, out var node)) return SkillPurchaseResultApplication.UnknownNode;
        if (IsOwned(uid)) return SkillPurchaseResultApplication.AlreadyOwned;
        if (node.Prerequisite != null && !IsOwned(node.Prerequisite)) return SkillPurchaseResultApplication.PrerequisiteMissing;
        if (!EffectsMatch()) return SkillPurchaseResultApplication.EffectsChanged;
        if (node.CellType.HasValue && !_effects.CanApply(node.CellType.Value, node.Stat, node.Modifier)) return SkillPurchaseResultApplication.InvalidEffect;
        return _wallet.Current.AvailableBalance < node.Price
            ? SkillPurchaseResultApplication.InsufficientFunds : SkillPurchaseResultApplication.Success;
    }

    public SkillPurchaseResultApplication Purchase(string uid)
    {
        if (_purchasing) return SkillPurchaseResultApplication.Busy;
        var result = CanPurchase(uid);
        if (result != SkillPurchaseResultApplication.Success) return result;
        _purchasing = true;
        CurrencyTransactionApplication expense = null;
        try
        {
            Catalog.TryGet(uid, out var node);
            if (!_wallet.TryReserveExpense(node.Price, out expense)) return SkillPurchaseResultApplication.InsufficientFunds;
            // 支出予約通知から戻った後に取得状態・前提・共有状態を再確認する。
            if (IsOwned(uid)) return SkillPurchaseResultApplication.AlreadyOwned;
            if (node.Prerequisite != null && !IsOwned(node.Prerequisite)) return SkillPurchaseResultApplication.PrerequisiteMissing;
            if (!EffectsMatch()) return SkillPurchaseResultApplication.EffectsChanged;
            if (node.CellType.HasValue)
            {
                if (!_effects.CanApply(node.CellType.Value, node.Stat, node.Modifier)) return SkillPurchaseResultApplication.InvalidEffect;
                _effects.ApplyWithoutNotification(node.CellType.Value, node.Stat, node.Modifier);
                _modifiers[(node.CellType.Value, node.Stat)] = node.Modifier;
            }
            _owned.Add(uid);
            // 残高の確定通知でも取得状態と能力が揃う。全通知が終わるまで購入の再入を拒否する。
            expense.TryCommit();
            if (node.CellType.HasValue) _effects.NotifyChanged();
            var handlers = Changed;
            if (handlers != null)
                foreach (Action handler in handlers.GetInvocationList())
                    try { handler(); } catch (Exception exception) { LastNotificationException = exception; }
            return SkillPurchaseResultApplication.Success;
        }
        finally
        {
            expense?.TryCancel();
            _purchasing = false;
        }
    }

    private bool EffectsMatch()
    {
        foreach (CellTypeDomain type in Enum.GetValues(typeof(CellTypeDomain)))
        foreach (EquipmentStatDomain stat in Enum.GetValues(typeof(EquipmentStatDomain)))
        {
            var expected = _modifiers.TryGetValue((type, stat), out var value) ? value : StatModifierDomain.Default;
            var actual = _effects.GetModifier(type, stat);
            if (actual.Multiplier != expected.Multiplier || actual.Addition != expected.Addition) return false;
        }
        return true;
    }
}

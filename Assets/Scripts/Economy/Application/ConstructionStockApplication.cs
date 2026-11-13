using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>一回のプレイの建設在庫。資材庫の寿命と分け、受入と建設支出の全量を予約する。</summary>
public sealed class ConstructionStockApplication
{
    public sealed class Reservation
    {
        internal bool Pending = true;
        internal bool Incoming;
        private readonly ConstructionStockApplication _owner;
        public IReadOnlyList<ResourceCostDomain> Items { get; }
        internal Reservation(ConstructionStockApplication owner, ResourceCostDomain[] items, bool incoming)
        { _owner = owner; Items = Array.AsReadOnly(items); Incoming = incoming; }
        public bool TryCommit() => _owner.Complete(this, true);
        public bool TryCancel() => _owner.Complete(this, false);
    }
    private readonly HashSet<ResourceTypeDomain> _accepted;
    private readonly Dictionary<ResourceTypeDomain, long> _stored = new(), _incoming = new(), _expenses = new();
    private bool _notifying, _notifyAgain;
    public event Action Changed;
    public Exception LastNotificationException { get; private set; }
    public ConstructionStockApplication(IEnumerable<ResourceTypeDomain> accepted)
    {
        _accepted = new HashSet<ResourceTypeDomain>(accepted ?? throw new ArgumentNullException(nameof(accepted)));
        if (_accepted.Any(r => r == ResourceTypeDomain.None || !Enum.IsDefined(typeof(ResourceTypeDomain), r)))
            throw new ArgumentException("Construction stock requires registered resources.");
    }
    private static long Count(Dictionary<ResourceTypeDomain, long> values, ResourceTypeDomain resource)
        => values.TryGetValue(resource, out var amount) ? amount : 0;
    public long Stored(ResourceTypeDomain resource) => Count(_stored, resource);
    public long Available(ResourceTypeDomain resource) => Stored(resource) - Count(_expenses, resource);
    public IReadOnlyDictionary<ResourceTypeDomain,long> CaptureStored() => new Dictionary<ResourceTypeDomain,long>(_stored);
    public void RestoreStored(IReadOnlyDictionary<ResourceTypeDomain,long> values)
    {
        if (_stored.Count != 0 || _incoming.Count != 0 || _expenses.Count != 0) throw new InvalidOperationException("Restore requires fresh construction stock.");
        if (values == null || values.Any(p => !_accepted.Contains(p.Key) || p.Value <= 0)) throw new ArgumentException("Invalid saved construction stock.");
        foreach (var pair in values) _stored.Add(pair.Key, pair.Value);
    }
    public bool TryReserveDeposit(ResourceTypeDomain resource, long amount, out Reservation reservation)
    {
        reservation = null;
        // 支出予約を受入余地として使うと、支出取消で上限を超えるため、確定在庫で判定する。
        if (!_accepted.Contains(resource) || amount <= 0 || amount > long.MaxValue - Stored(resource) - Count(_incoming, resource)) return false;
        _incoming[resource] = Count(_incoming, resource) + amount;
        reservation = new Reservation(this, new[] { new ResourceCostDomain(resource, amount) }, true); return true;
    }
    public bool TryReserveExpense(IReadOnlyList<ResourceCostDomain> costs, out Reservation reservation)
    {
        reservation = null;
        var items = costs.ToArray();
        if (items.Select(i => i.Resource).Distinct().Count() != items.Length) throw new ArgumentException("Duplicate material costs.");
        foreach (var item in items)
            if (!_accepted.Contains(item.Resource) || item.Amount > Available(item.Resource)) return false;
        if (items.Length == 0) return true;
        foreach (var item in items) _expenses[item.Resource] = Count(_expenses, item.Resource) + item.Amount;
        reservation = new Reservation(this, items, false); return true;
    }
    private bool Complete(Reservation reservation, bool commit)
    {
        if (!reservation.Pending) return false;
        reservation.Pending = false;
        var held = reservation.Incoming ? _incoming : _expenses;
        foreach (var item in reservation.Items)
        {
            held[item.Resource] -= item.Amount;
            if (commit) _stored[item.Resource] = reservation.Incoming ? Stored(item.Resource) + item.Amount : Stored(item.Resource) - item.Amount;
        }
        // 全材料を更新した後に通知し、再入した建設も完成した在庫を読む。
        if (commit) Notify(); return true;
    }
    private void Notify()
    {
        _notifyAgain = true; if (_notifying) return;
        _notifying = true;
        try
        {
            while (_notifyAgain)
            {
                _notifyAgain = false;
                if (Changed == null) continue;
                foreach (Action handler in Changed.GetInvocationList())
                    try { handler(); } catch (Exception exception) { LastNotificationException = exception; }
            }
        }
        finally { _notifying = false; }
    }
}

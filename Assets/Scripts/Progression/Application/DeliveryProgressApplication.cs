using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

/// <summary>一回のプレイの累計納品。搬送の確定前に全量を予約し、取消では加算しない。</summary>
public sealed class DeliveryProgressApplication
{
    public sealed class Snapshot
    {
        public IReadOnlyDictionary<ResourceTypeDomain, long> Delivered { get; }
        public int CompletedStages { get; }
        public bool IsComplete { get; }
        internal Snapshot(Dictionary<ResourceTypeDomain, long> delivered, int completed, int total)
        { Delivered = new ReadOnlyDictionary<ResourceTypeDomain, long>(new Dictionary<ResourceTypeDomain, long>(delivered)); CompletedStages = completed; IsComplete = completed == total; }
        public long Count(ResourceTypeDomain type) => Delivered.TryGetValue(type, out var count) ? count : 0;
    }
    public sealed class Reservation
    {
        private readonly DeliveryProgressApplication _owner;
        internal bool Pending = true;
        public ResourceTypeDomain Resource { get; }
        public long Amount { get; }
        internal Reservation(DeliveryProgressApplication owner, ResourceTypeDomain resource, long amount)
        { _owner = owner; Resource = resource; Amount = amount; }
        public bool TryCommit() => _owner.Complete(this, true);
        public bool TryCancel() => _owner.Complete(this, false);
    }
    private readonly Dictionary<ResourceTypeDomain, long> _delivered = new(), _reserved = new();
    private readonly Queue<Action> _notifications = new();
    private bool _notifying;
    public DeliveryGoalCatalogDomain Catalog { get; }
    public Snapshot Current { get; private set; }
    public Exception LastNotificationException { get; private set; }
    public event Action<Snapshot> Changed;
    public event Action<DeliveryGoalCatalogDomain.Stage> StageCompleted;
    public event Action Completed;
    public DeliveryProgressApplication(DeliveryGoalCatalogDomain catalog)
    { Catalog = catalog ?? throw new ArgumentNullException(nameof(catalog)); Current = new Snapshot(_delivered, 0, catalog.Stages.Count); }
    public bool TryReserve(ResourceTypeDomain type, long amount, out Reservation reservation)
    {
        reservation = null;
        if (type == ResourceTypeDomain.None || !Enum.IsDefined(typeof(ResourceTypeDomain), type) || amount <= 0) return false;
        var reserved = _reserved.TryGetValue(type, out var held) ? held : 0;
        // 他ポートの未確定分も含め、確定後に累計値が溢れないことを保証する。
        if (amount > long.MaxValue - Current.Count(type) - reserved) return false;
        _reserved[type] = reserved + amount;
        reservation = new Reservation(this, type, amount); return true;
    }
    private bool Complete(Reservation reservation, bool commit)
    {
        if (!reservation.Pending) return false;
        reservation.Pending = false;
        _reserved[reservation.Resource] -= reservation.Amount;
        if (!commit) return true;
        _delivered[reservation.Resource] = Current.Count(reservation.Resource) + reservation.Amount;
        var previous = Current.CompletedStages;
        var completed = previous;
        while (completed < Catalog.Stages.Count && Catalog.Stages[completed].Requirements.All(r =>
            _delivered.TryGetValue(r.Resource, out var count) && count >= r.Amount)) completed++;
        Current = new Snapshot(_delivered, completed, Catalog.Stages.Count);
        var snapshot = Current;
        // 再入した納品も順番に通知し、達成通知を重複させない。
        _notifications.Enqueue(() => Notify(Changed, snapshot));
        for (var i = previous; i < completed; i++)
        {
            var stage = Catalog.Stages[i];
            _notifications.Enqueue(() => Notify(StageCompleted, stage));
        }
        if (previous < Catalog.Stages.Count && snapshot.IsComplete)
            _notifications.Enqueue(() => Notify(Completed));
        if (_notifying) return true;
        _notifying = true;
        try { while (_notifications.Count > 0) _notifications.Dequeue()(); }
        finally { _notifying = false; }
        return true;
    }
    private void Notify<T>(Action<T> handlers, T value)
    {
        if (handlers == null) return;
        foreach (Action<T> handler in handlers.GetInvocationList())
            try { handler(value); } catch (Exception exception) { LastNotificationException = exception; }
    }
    private void Notify(Action handlers)
    {
        if (handlers == null) return;
        foreach (Action handler in handlers.GetInvocationList())
            try { handler(); } catch (Exception exception) { LastNotificationException = exception; }
    }
}

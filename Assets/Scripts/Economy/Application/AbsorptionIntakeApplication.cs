using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>換金しない吸収先の入力予約。全量・種別を固定し、確定と取消を一度だけ行う。</summary>
public sealed class AbsorptionIntakeApplication
{
    private readonly Dictionary<int, DeliveryProgressApplication.Reservation> _inputs;
    private readonly DeliveryProgressApplication _progress;
    public bool Accepting { get; set; }
    public event Action<ResourceTypeDomain, int> Absorbed;
    public Exception LastNotificationException { get; private set; }
    public AbsorptionIntakeApplication(IEnumerable<int> ports, DeliveryProgressApplication progress)
    {
        _progress = progress ?? throw new ArgumentNullException(nameof(progress));
        _inputs = ports.ToDictionary(id => id, _ => (DeliveryProgressApplication.Reservation)null);
    }
    public bool TryReserve(int port, int amount, ResourceTypeDomain type)
    {
        if (!Accepting || amount <= 0 || type == ResourceTypeDomain.None || !Enum.IsDefined(typeof(ResourceTypeDomain), type) ||
            !_inputs.TryGetValue(port, out var input) || input != null || !_progress.TryReserve(type, amount, out var reservation)) return false;
        _inputs[port] = reservation; return true;
    }
    public bool Commit(int port, int amount) => Complete(port, amount, ResourceTypeDomain.None, true);
    public bool Cancel(int port, int amount, ResourceTypeDomain type) => Complete(port, amount, type, false);
    private bool Complete(int port, int amount, ResourceTypeDomain type, bool commit)
    {
        if (!_inputs.TryGetValue(port, out var input) || input == null || input.Amount != amount || (!commit && input.Resource != type)) return false;
        // 先に予約を外し、通知からの再入でも同じ資源を再確定しない。
        _inputs[port] = null;
        if (!(commit ? input.TryCommit() : input.TryCancel())) return false;
        if (commit && Absorbed != null)
            foreach (Action<ResourceTypeDomain, int> notify in Absorbed.GetInvocationList())
                try { notify(input.Resource, amount); } catch (Exception exception) { LastNotificationException = exception; }
        return true;
    }
    public void CancelAll()
    {
        var pending = _inputs.Values.Where(r => r != null).ToArray();
        foreach (var port in _inputs.Keys.ToArray()) _inputs[port] = null;
        foreach (var reservation in pending) reservation.TryCancel();
    }
}

using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>換金しない吸収先の入力予約。全量・種別を固定し、確定と取消を一度だけ行う。</summary>
public sealed class AbsorptionIntakeApplication
{
    private readonly Dictionary<int, (int amount, ResourceTypeDomain type)> _inputs;
    public bool Accepting { get; set; }
    public event Action<ResourceTypeDomain, int> Absorbed;
    public Exception LastNotificationException { get; private set; }
    public AbsorptionIntakeApplication(IEnumerable<int> ports)
        => _inputs = ports.ToDictionary(id => id, _ => (0, ResourceTypeDomain.None));
    public bool TryReserve(int port, int amount, ResourceTypeDomain type)
    {
        if (!Accepting || amount <= 0 || type == ResourceTypeDomain.None || !Enum.IsDefined(typeof(ResourceTypeDomain), type) ||
            !_inputs.TryGetValue(port, out var input) || input.amount != 0) return false;
        _inputs[port] = (amount, type); return true;
    }
    public bool Commit(int port, int amount) => Complete(port, amount, ResourceTypeDomain.None, true);
    public bool Cancel(int port, int amount, ResourceTypeDomain type) => Complete(port, amount, type, false);
    private bool Complete(int port, int amount, ResourceTypeDomain type, bool commit)
    {
        if (!_inputs.TryGetValue(port, out var input) || input.amount == 0 || input.amount != amount || (!commit && input.type != type)) return false;
        // 先に予約を外し、通知からの再入でも同じ資源を再確定しない。
        _inputs[port] = (0, ResourceTypeDomain.None);
        if (commit && Absorbed != null)
            foreach (Action<ResourceTypeDomain, int> notify in Absorbed.GetInvocationList())
                try { notify(input.type, amount); } catch (Exception exception) { LastNotificationException = exception; }
        return true;
    }
    public void CancelAll()
    { foreach (var port in _inputs.Keys.ToArray()) _inputs[port] = (0, ResourceTypeDomain.None); }
}

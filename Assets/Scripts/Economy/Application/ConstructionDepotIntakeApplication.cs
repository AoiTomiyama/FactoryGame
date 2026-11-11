using System.Collections.Generic;
using System.Linq;

/// <summary>資材庫の各ポートを共有在庫の受入予約へ結び、取消と確定を一度だけ行う。</summary>
public sealed class ConstructionDepotIntakeApplication
{
    private readonly ConstructionStockApplication _stock;
    private readonly Dictionary<int, ConstructionStockApplication.Reservation> _inputs;
    public bool Accepting { get; set; }
    public ConstructionDepotIntakeApplication(ConstructionStockApplication stock, IEnumerable<int> ports)
    { _stock = stock; _inputs = ports.ToDictionary(id => id, _ => (ConstructionStockApplication.Reservation)null); }
    public bool TryReserve(int port, int amount, ResourceTypeDomain resource)
    {
        if (!Accepting || !_inputs.TryGetValue(port, out var current) || current != null ||
            !_stock.TryReserveDeposit(resource, amount, out var reservation)) return false;
        _inputs[port] = reservation; return true;
    }
    public bool Commit(int port, int amount) => Complete(port, amount, ResourceTypeDomain.None, true);
    public bool Cancel(int port, int amount, ResourceTypeDomain resource) => Complete(port, amount, resource, false);
    private bool Complete(int port, int amount, ResourceTypeDomain resource, bool commit)
    {
        if (!_inputs.TryGetValue(port, out var input) || input == null || input.Items[0].Amount != amount ||
            !commit && input.Items[0].Resource != resource) return false;
        _inputs[port] = null;
        return commit ? input.TryCommit() : input.TryCancel();
    }
    public void CancelAll()
    {
        var inputs = _inputs.Values.Where(r => r != null).ToArray();
        foreach (var port in _inputs.Keys.ToArray()) _inputs[port] = null;
        foreach (var input in inputs) input.TryCancel();
    }
}

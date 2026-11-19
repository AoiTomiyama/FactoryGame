using System;
using System.Collections.Generic;

/// <summary>入口ごとの受入枠と、直近に確定した搬送の次に試す順番を保持する。</summary>
internal sealed class ConveyorJunctionApplication
{
    internal sealed class Batch
    {
        public readonly int Port, Amount;
        public readonly ResourceTypeDomain Type;
        public int Id;
        public bool Ready;
        public Batch(int port, int amount, ResourceTypeDomain type) { Port = port; Amount = amount; Type = type; }
    }

    private readonly Dictionary<int, Batch> _batches = new();
    private readonly int[] _inputs, _outputs;
    public IEnumerable<Batch> Batches => _batches.Values;
    public int InputCursor { get; private set; }
    public int OutputCursor { get; private set; }

    public ConveyorJunctionApplication(int[] inputs, int[] outputs, int inputCursor, int outputCursor)
    { _inputs = inputs; _outputs = outputs; InputCursor = inputCursor; OutputCursor = outputCursor; }

    public bool Reserve(int port, int amount, ResourceTypeDomain type) =>
        amount > 0 && type != ResourceTypeDomain.None && Array.IndexOf(_inputs, port) >= 0 &&
        _batches.TryAdd(port, new Batch(port, amount, type));

    public Batch At(int port) => _batches.TryGetValue(port, out var batch) ? batch : null;
    public void Release(int port) => _batches.Remove(port);
    public Batch NextBatch()
    {
        for (var i = 0; i < _inputs.Length; i++)
            if (At(_inputs[(InputCursor + i) % _inputs.Length]) is { Ready: true } batch) return batch;
        return null;
    }

    public bool TryDispatch(Func<int, ResourceReservationApplication> tryReserve, out int port,
        out ResourceReservationApplication reservation)
    {
        for (var i = 0; i < _outputs.Length; i++)
        {
            port = _outputs[(OutputCursor + i) % _outputs.Length];
            reservation = tryReserve(port);
            if (reservation != null) return true;
        }
        port = -1;
        reservation = null;
        return false;
    }

    public void Complete(Batch batch, int output)
    {
        InputCursor = (Array.IndexOf(_inputs, batch.Port) + 1) % _inputs.Length;
        OutputCursor = (Array.IndexOf(_outputs, output) + 1) % _outputs.Length;
        Release(batch.Port);
    }
}

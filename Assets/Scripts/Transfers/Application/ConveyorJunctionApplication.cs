using System;
/// <summary>分岐の受入枠と、次に使う出口の順番を保持する。</summary>
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

    private readonly int _input;
    private readonly int[] _outputs;
    public Batch CurrentBatch { get; private set; }
    public int OutputCursor { get; private set; }

    public ConveyorJunctionApplication(int input, int[] outputs, int outputCursor)
    { _input = input; _outputs = outputs; OutputCursor = outputCursor; }

    public bool Reserve(int port, int amount, ResourceTypeDomain type)
    {
        if (port != _input || amount <= 0 || type == ResourceTypeDomain.None || CurrentBatch != null) return false;
        CurrentBatch = new Batch(port, amount, type);
        return true;
    }

    public Batch At(int port) => port == _input ? CurrentBatch : null;
    public void Release() => CurrentBatch = null;

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

    public void Complete(int output)
    {
        OutputCursor = (Array.IndexOf(_outputs, output) + 1) % _outputs.Length;
        Release();
    }
}

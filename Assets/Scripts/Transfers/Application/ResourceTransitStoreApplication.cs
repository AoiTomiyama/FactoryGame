using System;
using System.Collections.Generic;
using System.Threading;

/// <summary>セル間を搬送中の資源を、表示オブジェクトの寿命とは独立して保持する。</summary>
public sealed class ResourceTransitStoreApplication
{
    public readonly struct Data
    {
        public ResourceTypeDomain Type { get; }
        public int Amount { get; }

        public Data(ResourceTypeDomain type, int amount) { Type = type; Amount = amount; }
    }

    private static int _nextId;
    private readonly Dictionary<int, Data> _resources = new();

    public int Create(ResourceTypeDomain type, int amount)
    {
        if (type == ResourceTypeDomain.None || amount <= 0)
            throw new ArgumentException("搬送資源の種別と正の数量を指定してください。");
        // 表示を再利用しても、終了した搬送のIDを新しい資源に割り当てない。
        var id = Interlocked.Increment(ref _nextId);
        if (id <= 0) throw new InvalidOperationException("搬送資源IDを使い切りました。");
        _resources.Add(id, new Data(type, amount));
        return id;
    }

    public bool TryGet(int id, out Data data) => _resources.TryGetValue(id, out data);
    public bool Remove(int id) => _resources.Remove(id);
    public void Clear() => _resources.Clear();
}

using System;
using System.Collections.Generic;

/// <summary>整地できる資源と単価を保持し、重複しない座標で一括費用を求める。</summary>
public sealed class TerrainClearingRulesDomain
{
    public readonly struct Target
    {
        public int X { get; }
        public int Z { get; }
        public ResourceTypeDomain Resource { get; }
        public Target(int x, int z, ResourceTypeDomain resource) { X = x; Z = z; Resource = resource; }
    }

    public sealed class Quote
    {
        public IReadOnlyList<Target> Targets { get; }
        public long Cost { get; }
        internal Quote(Target[] targets, long cost) { Targets = Array.AsReadOnly(targets); Cost = cost; }
    }

    public long WoodPrice { get; }
    public long StonePrice { get; }
    public TerrainClearingRulesDomain(long woodPrice, long stonePrice)
    {
        if (woodPrice <= 0 || stonePrice <= 0) throw new ArgumentOutOfRangeException(nameof(woodPrice), "Clearing prices must be positive.");
        WoodPrice = woodPrice; StonePrice = stonePrice;
    }

    public bool CanClear(ResourceTypeDomain resource) => resource is ResourceTypeDomain.Wood or ResourceTypeDomain.Stone;

    public bool TryQuote(IEnumerable<Target> candidates, out Quote quote)
    {
        quote = null;
        if (candidates == null) return false;
        var points = new Dictionary<(int, int), ResourceTypeDomain>();
        var targets = new List<Target>();
        long cost = 0;
        foreach (var target in candidates)
        {
            if (!CanClear(target.Resource)) continue;
            if (target.X < 0 || target.Z < 0) return false;
            if (points.TryGetValue((target.X, target.Z), out var previous))
            {
                if (previous != target.Resource) return false;
                continue;
            }
            var unit = target.Resource == ResourceTypeDomain.Wood ? WoodPrice : StonePrice;
            if (cost > long.MaxValue - unit) return false;
            points.Add((target.X, target.Z), target.Resource);
            targets.Add(target); cost += unit;
        }
        if (targets.Count == 0) return false;
        quote = new Quote(targets.ToArray(), cost);
        return true;
    }
}

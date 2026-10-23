using System;
using System.Collections.Generic;

/// <summary>固定uidで参照する不変のノード。根は効果を持たない。</summary>
public sealed class SkillNodeDomain
{
    public string Uid { get; }
    public string Label { get; }
    public long Price { get; }
    public string Prerequisite { get; }
    public StatBranchDomain? Branch { get; }
    public int BonusPercent { get; }

    public SkillNodeDomain(string uid, string label, long price, string prerequisite,
        StatBranchDomain? branch, int bonusPercent)
    {
        Uid = uid; Label = label; Price = price; Prerequisite = prerequisite;
        Branch = branch; BonusPercent = bonusPercent;
    }
}

/// <summary>購入前に全定義と最大合成率を検証し、設定から独立したカタログを保持する。</summary>
public sealed class SkillTreeCatalogDomain
{
    private readonly Dictionary<string, SkillNodeDomain> _nodes = new(StringComparer.Ordinal);
    public IReadOnlyList<SkillNodeDomain> Nodes { get; }
    public bool TryGet(string uid, out SkillNodeDomain node)
    {
        node = null;
        return uid != null && _nodes.TryGetValue(uid, out node);
    }

    public SkillTreeCatalogDomain(IEnumerable<SkillNodeDomain> nodes)
    {
        if (nodes == null) throw new ArgumentNullException(nameof(nodes));
        var totals = new long[3];
        var ordered = new List<SkillNodeDomain>();
        foreach (var node in nodes)
        {
            if (node == null || string.IsNullOrWhiteSpace(node.Uid) || string.IsNullOrWhiteSpace(node.Label) ||
                node.Price <= 0 || !_nodes.TryAdd(node.Uid, node))
                throw new ArgumentException("Invalid or duplicate skill node.");
            if (node.Branch.HasValue)
            {
                var index = (int)node.Branch.Value;
                if (index < 0 || index >= totals.Length || node.BonusPercent <= 0)
                    throw new ArgumentException("Invalid skill effect.");
                totals[index] += node.BonusPercent;
                if (totals[index] > EffectiveStatsRulesDomain.MaxBonusPercent)
                    throw new ArgumentException("Skill effects exceed the supported maximum.");
            }
            else if (node.BonusPercent != 0) throw new ArgumentException("Root cannot have an effect.");
            ordered.Add(node);
        }
        if (ordered.Count == 0) throw new ArgumentException("Skill tree is empty.");
        foreach (var node in ordered)
        {
            var path = new HashSet<string>(StringComparer.Ordinal);
            var current = node;
            while (true)
            {
                if (!path.Add(current.Uid)) throw new ArgumentException("Skill dependency cycle.");
                if (current.Prerequisite == null) break;
                if (!_nodes.TryGetValue(current.Prerequisite, out current))
                    throw new ArgumentException("Missing skill prerequisite.");
            }
        }
        Nodes = ordered.AsReadOnly();
    }
}

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
    public CellTypeDomain? CellType { get; }
    public int TargetLevel { get; }

    public SkillNodeDomain(string uid, string label, long price, string prerequisite,
        StatBranchDomain? branch, int bonusPercent, CellTypeDomain? cellType = null, int targetLevel = 0)
    {
        Uid = uid; Label = label; Price = price; Prerequisite = prerequisite;
        Branch = branch; BonusPercent = bonusPercent; CellType = cellType; TargetLevel = targetLevel;
    }
}

/// <summary>購入前に全定義と最大合成率を検証し、設定から独立したカタログを保持する。</summary>
public sealed class SkillTreeCatalogDomain
{
    public static bool SupportsType(CellTypeDomain type) => type is CellTypeDomain.ExtractorWood or CellTypeDomain.ExtractorStone or
        CellTypeDomain.Storage or CellTypeDomain.Crafter or CellTypeDomain.ExportConveyor;
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
        var typeTiers = new HashSet<(CellTypeDomain, int)>();
        var ordered = new List<SkillNodeDomain>();
        foreach (var node in nodes)
        {
            if (node == null || string.IsNullOrWhiteSpace(node.Uid) || string.IsNullOrWhiteSpace(node.Label) ||
                node.Price <= 0 || !_nodes.TryAdd(node.Uid, node))
                throw new ArgumentException("Invalid or duplicate skill node.");
            if (node.CellType.HasValue)
            {
                if (!SupportsType(node.CellType.Value) || node.TargetLevel < 2 || node.TargetLevel > EffectiveStatsRulesDomain.MaxLevel ||
                    node.Branch.HasValue || node.BonusPercent != 0 || !typeTiers.Add((node.CellType.Value, node.TargetLevel)))
                    throw new ArgumentException("Invalid or duplicate cell-type skill.");
            }
            else if (node.TargetLevel != 0) throw new ArgumentException("Type level requires a cell type.");
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
            if (node.CellType.HasValue && node.TargetLevel == 3 &&
                (node.Prerequisite == null || !_nodes.TryGetValue(node.Prerequisite, out var previous) ||
                 previous.CellType != node.CellType || previous.TargetLevel != 2))
                throw new ArgumentException("Type level three requires level two of the same type.");
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

using System;
using System.Collections.Generic;

/// <summary>固定uidで参照する不変のノード。根は効果を持たない。</summary>
public sealed class SkillNodeDomain
{
    public string Uid { get; }
    public string Label { get; }
    public long Price { get; }
    public string Prerequisite { get; }
    public CellTypeDomain? CellType { get; }
    public EquipmentStatDomain Stat { get; }
    public StatModifierDomain Modifier { get; }

    public SkillNodeDomain(string uid, string label, long price, string prerequisite,
        CellTypeDomain? cellType = null, EquipmentStatDomain stat = EquipmentStatDomain.ExtractionAmount, decimal multiplier = 1, int addition = 0)
    {
        Uid = uid; Label = label; Price = price; Prerequisite = prerequisite;
        CellType = cellType; Stat = stat; Modifier = new StatModifierDomain(multiplier, addition);
    }
}

/// <summary>購入前に種類別の定義と前提を検証し、設定から独立したカタログを保持する。</summary>
public sealed class SkillTreeCatalogDomain
{
    public static bool SupportsType(CellTypeDomain type) => ResourceCellRulesDomain.IsExtractor(type) || type is
        CellTypeDomain.Storage or CellTypeDomain.Crafter or CellTypeDomain.AdvancedCrafter or CellTypeDomain.ExportConveyor;
    public static bool SupportsStat(CellTypeDomain type, EquipmentStatDomain stat) => type switch
    {
        CellTypeDomain.ExtractorWood or CellTypeDomain.ExtractorStone or CellTypeDomain.ExtractorIron or
        CellTypeDomain.ExtractorCopper or CellTypeDomain.ExtractorCoal => stat is EquipmentStatDomain.ExtractionAmount or EquipmentStatDomain.Capacity,
        CellTypeDomain.Storage => stat == EquipmentStatDomain.Capacity,
        CellTypeDomain.Crafter or CellTypeDomain.AdvancedCrafter => stat is EquipmentStatDomain.InputCapacity or EquipmentStatDomain.OutputCapacity,
        CellTypeDomain.ExportConveyor => stat == EquipmentStatDomain.TransferAmount,
        _ => false
    };
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
        var ordered = new List<SkillNodeDomain>();
        foreach (var node in nodes)
        {
            if (node == null || string.IsNullOrWhiteSpace(node.Uid) || string.IsNullOrWhiteSpace(node.Label) ||
                node.Price <= 0 || !_nodes.TryAdd(node.Uid, node))
                throw new ArgumentException("Invalid or duplicate skill node.");
            if (node.CellType.HasValue && !SupportsStat(node.CellType.Value, node.Stat))
                throw new ArgumentException("Unsupported ability for cell type.");
            if (!node.CellType.HasValue && (node.Modifier.Multiplier != 1 || node.Modifier.Addition != 0))
                throw new ArgumentException("Unlock node cannot modify an ability.");
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
        // 同じ能力の取得順を前提関係で一意にし、別々の枝で設定値を上書きし合う定義を拒否する。
        bool Ancestor(SkillNodeDomain ancestor, SkillNodeDomain child)
        {
            while (child.Prerequisite != null) { child = _nodes[child.Prerequisite]; if (child.Uid == ancestor.Uid) return true; }
            return false;
        }
        for (var i = 0; i < ordered.Count; i++) for (var j = i + 1; j < ordered.Count; j++)
            if (ordered[i].CellType.HasValue && ordered[i].CellType == ordered[j].CellType && ordered[i].Stat == ordered[j].Stat &&
                !Ancestor(ordered[i], ordered[j]) && !Ancestor(ordered[j], ordered[i]))
                throw new ArgumentException("Skills for the same ability must share a prerequisite chain.");
        Nodes = ordered.AsReadOnly();
    }
}

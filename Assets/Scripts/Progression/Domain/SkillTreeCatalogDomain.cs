using System;
using System.Collections.Generic;
using System.Linq;

public enum ExtractionSkillGroupDomain { None, All, Basic, Industrial }

/// <summary>固定uidで参照する不変のノード。解放ノードは能力補正を持たない。</summary>
public sealed class SkillNodeDomain
{
    public string Uid { get; }
    public string Label { get; }
    public long Price { get; }
    public string Prerequisite { get; }
    public string RequiredDeliveryStage { get; }
    public IReadOnlyList<CellTypeDomain> TargetCellTypes { get; }
    public EquipmentStatDomain Stat { get; }
    public ExtractionSkillGroupDomain ExtractionGroup { get; }
    public StatModifierDomain Modifier { get; }

    public SkillNodeDomain(string uid, string label, long price, string prerequisite,
        IEnumerable<CellTypeDomain> cellTypes = null, EquipmentStatDomain stat = EquipmentStatDomain.ExtractionAmount, decimal multiplier = 1, int addition = 0,
        string requiredDeliveryStage = null, ExtractionSkillGroupDomain extractionGroup = ExtractionSkillGroupDomain.None)
    {
        Uid = uid; Label = label; Price = price; Prerequisite = prerequisite;
        RequiredDeliveryStage = requiredDeliveryStage;
        ExtractionGroup = extractionGroup;
        TargetCellTypes = Array.AsReadOnly(cellTypes?.ToArray() ?? Array.Empty<CellTypeDomain>());
        Stat = stat; Modifier = new StatModifierDomain(multiplier, addition);
    }
}

/// <summary>購入前に種類別の定義と前提を検証し、設定から独立したカタログを保持する。</summary>
public sealed class SkillTreeCatalogDomain
{
    public static bool SupportsType(CellTypeDomain type) => ResourceCellRulesDomain.IsExtractor(type) || type is
        CellTypeDomain.Storage or CellTypeDomain.Crafter or CellTypeDomain.AdvancedCrafter or CellTypeDomain.ExportConveyor or CellTypeDomain.Conveyor or CellTypeDomain.Crossing;
    public static bool SupportsStat(CellTypeDomain type, EquipmentStatDomain stat) => type switch
    {
        _ when ResourceCellRulesDomain.IsExtractor(type) => stat is
            EquipmentStatDomain.ExtractionAmount or EquipmentStatDomain.ExtractionSpeed or EquipmentStatDomain.Capacity,
        CellTypeDomain.Storage => stat == EquipmentStatDomain.Capacity,
        CellTypeDomain.Crafter or CellTypeDomain.AdvancedCrafter => stat is EquipmentStatDomain.InputCapacity or EquipmentStatDomain.OutputCapacity or EquipmentStatDomain.CraftingSpeed,
        CellTypeDomain.ExportConveyor => stat is EquipmentStatDomain.TransferAmount or EquipmentStatDomain.TransportSpeed,
        CellTypeDomain.Conveyor or CellTypeDomain.Crossing => stat == EquipmentStatDomain.TransportSpeed,
        _ => false
    };
    private readonly Dictionary<string, SkillNodeDomain> _nodes = new(StringComparer.Ordinal);
    public IReadOnlyList<SkillNodeDomain> Nodes { get; }
    public bool TryGet(string uid, out SkillNodeDomain node)
    {
        node = null;
        return uid != null && _nodes.TryGetValue(uid, out node);
    }

    public void ValidateDeliveryPrerequisites(DeliveryGoalCatalogDomain deliveries)
    {
        foreach (var node in Nodes)
        {
            if (node.RequiredDeliveryStage == null) continue;
            var found = false;
            if (deliveries != null)
                foreach (var stage in deliveries.Stages) if (stage.Id == node.RequiredDeliveryStage) { found = true; break; }
            if (!found) throw new ArgumentException($"Unknown delivery prerequisite '{node.RequiredDeliveryStage}' for skill '{node.Uid}'.");
        }
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
            if (node.TargetCellTypes.Count != node.TargetCellTypes.Distinct().Count() || node.TargetCellTypes.Any(t => !SupportsStat(t,node.Stat)))
                throw new ArgumentException("Unsupported ability for cell type.");
            if (node.TargetCellTypes.Count > 0 && EffectiveStatsRulesDomain.IsSpeed(node.Stat))
                EffectiveStatsRulesDomain.CalculateDuration(1,node.Modifier);
            if (node.TargetCellTypes.Count == 0 && (node.Modifier.Multiplier != 1 || node.Modifier.Addition != 0))
                throw new ArgumentException("Unlock node cannot modify an ability.");
            if (node.RequiredDeliveryStage != null && string.IsNullOrWhiteSpace(node.RequiredDeliveryStage))
                throw new ArgumentException("Delivery prerequisite must have an id.");
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
            if (ordered[i].TargetCellTypes.Intersect(ordered[j].TargetCellTypes).Any() && ordered[i].Stat == ordered[j].Stat &&
                !Ancestor(ordered[i], ordered[j]) && !Ancestor(ordered[j], ordered[i]))
                throw new ArgumentException("Skills for the same ability must share a prerequisite chain.");
        Nodes = ordered.AsReadOnly();
    }
}

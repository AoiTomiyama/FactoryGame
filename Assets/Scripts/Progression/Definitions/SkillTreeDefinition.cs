using System;
using System.Linq;
using UnityEngine;

[CreateAssetMenu(fileName = "SkillTree", menuName = "FactoryGame/Skill Tree")]
public sealed class SkillTreeDefinition : ScriptableObject
{
    [Serializable]
    public struct Node
    {
        public string uid;
        public string label;
        public long price;
        public string prerequisite;
        public string requiredDeliveryStage;
        public CellTypeDomain[] cellTypes;
        public ExtractionSkillGroupDomain extractionGroup;
        public EquipmentStatDomain stat;
        public float multiplier;
        public int addition;
    }

    // 所属は段階ごとのノードで繰り返さず、この一覧で管理する。
    [SerializeField] private CellTypeDomain[] basicExtractors;
    [SerializeField] private CellTypeDomain[] industrialExtractors;
    [SerializeField] private Node[] nodes;

    public SkillTreeCatalogDomain CreateCatalog()
    {
        if (nodes == null) throw new InvalidOperationException("Skill nodes must be assigned.");
        var allExtractors = ResolveExtractionGroups();
        var snapshot = new SkillNodeDomain[nodes.Length];
        for (var index = 0; index < nodes.Length; index++)
        {
            var node = nodes[index];
            if (float.IsNaN(node.multiplier) || float.IsInfinity(node.multiplier)) throw new InvalidOperationException("Skill multiplier must be finite.");
            snapshot[index] = new SkillNodeDomain(node.uid, node.label, node.price,
                string.IsNullOrEmpty(node.prerequisite) ? null : node.prerequisite,
                ResolveTargets(node, allExtractors), node.stat, (decimal)node.multiplier, node.addition,
                string.IsNullOrEmpty(node.requiredDeliveryStage) ? null : node.requiredDeliveryStage, node.extractionGroup);
        }
        return new SkillTreeCatalogDomain(snapshot);
    }

    private CellTypeDomain[] ResolveExtractionGroups()
    {
        if (basicExtractors == null || basicExtractors.Length == 0 ||
            industrialExtractors == null || industrialExtractors.Length == 0)
            throw new InvalidOperationException("Both extraction skill groups must contain extractors.");

        var grouped = basicExtractors.Concat(industrialExtractors).ToArray();
        var expected = Enum.GetValues(typeof(CellTypeDomain)).Cast<CellTypeDomain>()
            .Where(ResourceCellRulesDomain.IsExtractor).ToHashSet();
        // 対象外・重複・未分類を起動時に検出し、購入した効果の対象が欠けることを防ぐ。
        if (grouped.Length != expected.Count || !expected.SetEquals(grouped))
            throw new InvalidOperationException("Assign each extractor to exactly one extraction skill group.");
        return grouped;
    }

    private CellTypeDomain[] ResolveTargets(Node node, CellTypeDomain[] allExtractors)
    {
        if (node.extractionGroup == ExtractionSkillGroupDomain.None) return node.cellTypes;
        if (node.cellTypes?.Length > 0)
            throw new InvalidOperationException($"Skill '{node.uid}' must use either an extraction group or explicit targets.");

        return node.extractionGroup switch
        {
            ExtractionSkillGroupDomain.All => allExtractors,
            ExtractionSkillGroupDomain.Basic => basicExtractors,
            ExtractionSkillGroupDomain.Industrial => industrialExtractors,
            _ => throw new InvalidOperationException($"Skill '{node.uid}' has an unknown extraction group.")
        };
    }
}

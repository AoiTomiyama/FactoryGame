using System;
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
        public bool hasEffect;
        public StatBranchDomain branch;
        public int bonusPercent;
        public bool hasCellTypeEffect;
        public CellTypeDomain cellType;
        public int targetLevel;
    }

    [SerializeField] private Node[] nodes;

    public SkillTreeCatalogDomain CreateCatalog()
    {
        if (nodes == null) throw new InvalidOperationException("Skill nodes must be assigned.");
        var snapshot = new SkillNodeDomain[nodes.Length];
        for (var index = 0; index < nodes.Length; index++)
        {
            var node = nodes[index];
            snapshot[index] = new SkillNodeDomain(node.uid, node.label, node.price,
                string.IsNullOrEmpty(node.prerequisite) ? null : node.prerequisite,
                node.hasEffect ? node.branch : null, node.bonusPercent,
                node.hasCellTypeEffect ? node.cellType : null, node.targetLevel);
        }
        return new SkillTreeCatalogDomain(snapshot);
    }
}

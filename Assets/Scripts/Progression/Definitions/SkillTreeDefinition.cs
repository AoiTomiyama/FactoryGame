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
        public CellTypeDomain cellType;
        public EquipmentStatDomain stat;
        public float multiplier;
        public int addition;
    }

    [SerializeField] private Node[] nodes;

    public SkillTreeCatalogDomain CreateCatalog()
    {
        if (nodes == null) throw new InvalidOperationException("Skill nodes must be assigned.");
        var snapshot = new SkillNodeDomain[nodes.Length];
        for (var index = 0; index < nodes.Length; index++)
        {
            var node = nodes[index];
            if (float.IsNaN(node.multiplier) || float.IsInfinity(node.multiplier)) throw new InvalidOperationException("Skill multiplier must be finite.");
            snapshot[index] = new SkillNodeDomain(node.uid, node.label, node.price,
                string.IsNullOrEmpty(node.prerequisite) ? null : node.prerequisite,
                node.cellType == CellTypeDomain.None ? null : node.cellType, node.stat, (decimal)node.multiplier, node.addition);
        }
        return new SkillTreeCatalogDomain(snapshot);
    }
}

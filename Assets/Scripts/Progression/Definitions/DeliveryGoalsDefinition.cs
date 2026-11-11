using System;
using System.Linq;
using UnityEngine;

[CreateAssetMenu(fileName = "DeliveryGoals", menuName = "FactoryGame/Delivery Goals")]
public sealed class DeliveryGoalsDefinition : ScriptableObject
{
    [Serializable]
    public struct Requirement
    {
        public ResourceTypeDomain resource;
        [Min(1)] public long cumulativeAmount;
    }
    [Serializable]
    public struct Stage
    {
        public string id;
        public string label;
        public Requirement[] requirements;
    }
    [SerializeField] private Stage[] stages;
    // 定義を実行時に書き換えず、一回のプレイ用に検証済みのコピーを渡す。
    public DeliveryGoalCatalogDomain CreateCatalog() => new DeliveryGoalCatalogDomain(
        (stages ?? throw new InvalidOperationException("Assign delivery stages.")).Select(s =>
            new DeliveryGoalCatalogDomain.Stage(s.id, s.label, s.requirements?.Select(r =>
                new DeliveryGoalCatalogDomain.Requirement(r.resource, r.cumulativeAmount)))));
}

using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>段階ごとの累計目標。実行中の納品数と編集用データを分離する。</summary>
public sealed class DeliveryGoalCatalogDomain
{
    public sealed class Requirement
    {
        public ResourceTypeDomain Resource { get; }
        public long Amount { get; }
        public Requirement(ResourceTypeDomain resource, long amount)
        {
            if (resource == ResourceTypeDomain.None || !Enum.IsDefined(typeof(ResourceTypeDomain), resource) || amount <= 0)
                throw new ArgumentException("Delivery requirements need a valid resource and positive count.");
            Resource = resource; Amount = amount;
        }
    }
    public sealed class Stage
    {
        public string Id { get; }
        public string Label { get; }
        public IReadOnlyList<Requirement> Requirements { get; }
        public Stage(string id, string label, IEnumerable<Requirement> requirements)
        {
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(label) || requirements == null)
                throw new ArgumentException("Delivery stage needs an id, label and requirements.");
            var copy = requirements.ToArray();
            if (copy.Length == 0 || copy.Any(r => r == null) || copy.Select(r => r.Resource).Distinct().Count() != copy.Length)
                throw new ArgumentException("Delivery stage needs distinct resource requirements.");
            Id = id; Label = label; Requirements = Array.AsReadOnly(copy);
        }
    }
    public IReadOnlyList<Stage> Stages { get; }
    public DeliveryGoalCatalogDomain(IEnumerable<Stage> stages)
    {
        var copy = stages?.ToArray() ?? throw new ArgumentNullException(nameof(stages));
        if (copy.Length == 0 || copy.Any(s => s == null) || copy.Select(s => s.Id).Distinct().Count() != copy.Length)
            throw new ArgumentException("Delivery stages need distinct ids.");
        var targets = new Dictionary<ResourceTypeDomain, long>();
        foreach (var stage in copy)
            foreach (var requirement in stage.Requirements)
            {
                if (targets.TryGetValue(requirement.Resource, out var previous) && previous > requirement.Amount)
                    throw new ArgumentException("Cumulative delivery targets cannot decrease.");
                targets[requirement.Resource] = requirement.Amount;
            }
        Stages = Array.AsReadOnly(copy);
    }
}

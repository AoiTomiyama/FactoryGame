using System;

/// <summary>一種類の建設材料費。定義から複製し、取引中の必要量を固定する。</summary>
public readonly struct ResourceCostDomain
{
    public ResourceTypeDomain Resource { get; }
    public long Amount { get; }
    public ResourceCostDomain(ResourceTypeDomain resource, long amount)
    {
        if (resource == ResourceTypeDomain.None || !Enum.IsDefined(typeof(ResourceTypeDomain), resource) || amount <= 0)
            throw new ArgumentException("Material cost requires a resource and a positive amount.");
        Resource = resource; Amount = amount;
    }
}

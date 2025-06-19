using System;
using UnityEngine;

public sealed class ResourceCell : CellBase
{
    [SerializeField] private ResourceType resourceType;

    public ResourceType ResourceType => resourceType;
}
public enum ResourceType
{
    None,
    Stone,
    Wood,
    Iron,
    Gold
}
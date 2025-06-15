using System;
using UnityEngine;

public sealed class ResourceCell : CellBase
{
    [SerializeField] private ResourceType resourceType;
}

public enum ResourceType
{
    Stone,
    Wood,
    Iron,
    Gold
}

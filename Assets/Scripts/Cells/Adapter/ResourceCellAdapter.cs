using UnityEngine;
using System;

public sealed class ResourceCellAdapter : CellBaseAdapter, IDataProvidableApplication
{
    [SerializeField] private ResourceTypeDomain resourceType;
    [SerializeField] private ResourceProviderDefinition resourceProvider;

    public ResourceTypeDomain ResourceTypeDomain => resourceType;
    public event Action StatusChanged { add { } remove { } }
    public IUIDataProviderApplication GetDataProvider() => CellStatusProviderAdapter.Create(this, resourceProvider);
}
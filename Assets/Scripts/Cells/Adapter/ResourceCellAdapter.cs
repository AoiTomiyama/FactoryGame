using UnityEngine;

public sealed class ResourceCellAdapter : CellBaseAdapter, IDataProvidableApplication
{
    [SerializeField] private ResourceTypeDomain resourceType;
    [SerializeField] private ResourceProviderDefinition resourceProvider;

    public ResourceTypeDomain ResourceTypeDomain => resourceType;
    public bool IsUIActive { set { } }
    public IUIDataProviderApplication GetDataProvider() => resourceProvider;
}
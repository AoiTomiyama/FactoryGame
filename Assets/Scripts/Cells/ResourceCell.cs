using UnityEngine;

public sealed class ResourceCell : CellBase, IDataProvidable
{
    [SerializeField] private ResourceType resourceType;
    [SerializeField] private ResourceProvider resourceProvider;

    public ResourceType ResourceType => resourceType;
    public bool IsUIActive { set { } }
    public IUIDataProvider GetDataProvider() => resourceProvider;
}
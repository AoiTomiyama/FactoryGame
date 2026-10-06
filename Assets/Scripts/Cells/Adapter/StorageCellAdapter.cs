using UnityEngine;

public sealed class StorageCellAdapter : ConnectableCellBaseAdapter, IContainableApplication, IExportableApplication, IDataProvidableApplication
{
    [Header("ストレージセルの設定")]
    [SerializeField] private int capacity;
    [SerializeField] private StorageProviderDefinition dataProvider;

    public int Capacity => capacity;

    public int CurrentLoad { get; private set; }
    public int AllocatedAmount { get; private set; }
    public bool IsUIActive { get; set; }
    public ResourceTypeDomain StoredResourceType { get; private set; } = ResourceTypeDomain.None;
    public IUIDataProviderApplication GetDataProvider() => dataProvider;

    private ResourceStorageRulesDomain.Stock Stock => new(StoredResourceType, CurrentLoad, AllocatedAmount);

    private void ApplyStock(ResourceStorageRulesDomain.Stock stock)
    {
        StoredResourceType = stock.Type;
        CurrentLoad = stock.Amount;
        AllocatedAmount = stock.Reserved;
        UpdateUI();
    }

    private void UpdateUI()
    {
        if (!IsUIActive) return;
        CellStatusViewAdapter.Instance.UpdateUI();
    }

    public bool AllocateStorage(Vector3Int dir, int amount, ResourceTypeDomain resourceType)
    {
        if (!ResourceStorageRulesDomain.TryReserve(capacity, Stock, amount, resourceType, out var next)) return false;
        ApplyStock(next);
        return true;
    }

    public void StoreResource(Vector3Int dir, int amount)
    {
        if (ResourceStorageRulesDomain.TryCommit(capacity, Stock, amount, out var next)) ApplyStock(next);
    }

    public void CancelStorage(Vector3Int dir, int amount, ResourceTypeDomain resourceType)
    {
        if (ResourceStorageRulesDomain.TryCancel(Stock, amount, resourceType, out var next)) ApplyStock(next);
    }

    public Vector3 GetPosition() => transform.position;

    public bool TryExport(Vector3 from, int requestedAmount, out int amount, out ResourceTypeDomain type)
    {
        if (!ResourceStorageRulesDomain.TryExport(Stock, requestedAmount, out var next, out amount, out type)) return false;
        ApplyStock(next);
        return true;
    }
}

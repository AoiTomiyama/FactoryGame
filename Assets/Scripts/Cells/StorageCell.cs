using UnityEngine;

public sealed class StorageCell : ConnectableCellBase, IContainable, IExportable, IDataProvidable
{
    [Header("ストレージセルの設定")]
    [SerializeField] private int capacity;
    [SerializeField] private StorageProvider dataProvider;

    public int Capacity => capacity;

    public int CurrentLoad { get; private set; }
    public int AllocatedAmount { get; private set; }
    public bool IsUIActive { get; set; }
    public ResourceType StoredResourceType { get; private set; } = ResourceType.None;
    public IUIDataProvider GetDataProvider() => dataProvider;

    private ResourceStorageRules.Stock Stock => new(StoredResourceType, CurrentLoad, AllocatedAmount);

    private void ApplyStock(ResourceStorageRules.Stock stock)
    {
        StoredResourceType = stock.Type;
        CurrentLoad = stock.Amount;
        AllocatedAmount = stock.Reserved;
        UpdateUI();
    }

    private void UpdateUI()
    {
        if (!IsUIActive) return;
        CellStatusView.Instance.UpdateUI();
    }

    public bool AllocateStorage(Vector3Int dir, int amount, ResourceType resourceType)
    {
        if (!ResourceStorageRules.TryReserve(capacity, Stock, amount, resourceType, out var next)) return false;
        ApplyStock(next);
        return true;
    }

    public void StoreResource(Vector3Int dir, int amount)
    {
        if (ResourceStorageRules.TryCommit(capacity, Stock, amount, out var next)) ApplyStock(next);
    }

    public void CancelStorage(Vector3Int dir, int amount, ResourceType resourceType)
    {
        if (ResourceStorageRules.TryCancel(Stock, amount, resourceType, out var next)) ApplyStock(next);
    }

    public Vector3 GetPosition() => transform.position;

    public bool TryExport(Vector3 from, int requestedAmount, out int amount, out ResourceType type)
    {
        if (!ResourceStorageRules.TryExport(Stock, requestedAmount, out var next, out amount, out type)) return false;
        ApplyStock(next);
        return true;
    }
}

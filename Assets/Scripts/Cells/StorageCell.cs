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
    
    private void UpdateUI()
    {
        if (!IsUIActive) return;
        CellStatusView.Instance.UpdateUI();
    }

    public int AllocateStorage(Vector3Int dir, int amount, ResourceType resourceType)
    {
        // 既に容量限界に達している場合は0を返す
        // 入れようとしている値が空き容量を越えている場合は空き容量を返す
        // そうでない場合は指定された量を予約する
        var available = capacity - CurrentLoad - AllocatedAmount;
        var allocated = Mathf.Min(available, amount);
        AllocatedAmount += allocated;

        if (allocated > 0)
        {
            // 初めてのリソース追加
            if (StoredResourceType == ResourceType.None)
            {
                StoredResourceType = resourceType;
            }

            // 設定済みのリソースタイプと異なる場合、追加しない
            if (StoredResourceType != resourceType) return 0;
            UpdateUI();
        }
        
        return allocated;
    }

    public void StoreResource(Vector3Int dir, int amount)
    {
        if (amount > AllocatedAmount) return;

        // 現在量に追加し、予約量を減らす。
        CurrentLoad += amount;
        AllocatedAmount -= amount;
        
        UpdateUI();
    }
    
    public Vector3 GetPosition() => transform.position;

    public bool TryExport(Vector3 from, int requestedAmount, out int amount, out ResourceType type)
    {
        amount = 0;
        type = StoredResourceType;
        
        // 出力可能な量がない、または要求量がない場合はfalseを返す
        if (CurrentLoad <= 0 || requestedAmount <= 0) return false;
        
        // 返却量を計算し、現在量を減らす
        amount = Mathf.Min(requestedAmount, CurrentLoad);
        CurrentLoad = Mathf.Max(0, CurrentLoad - requestedAmount);
        
        // 現在量が0になった場合、リソースタイプをリセットする
        if (CurrentLoad == 0)
        {
            StoredResourceType = ResourceType.None;
        }
        
        UpdateUI();
        return true;
    }
}
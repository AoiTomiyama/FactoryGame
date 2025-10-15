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

    public bool AllocateStorage(Vector3Int dir, int amount, ResourceType resourceType)
    {
        var available = capacity - CurrentLoad - AllocatedAmount;
        var allocated = Mathf.Min(available, amount);

        // 予約可能量が0以下の場合は予約失敗
        if (allocated <= 0) return false;

        // 初めてのリソース追加
        if (StoredResourceType == ResourceType.None)
        {
            StoredResourceType = resourceType;
        }

        // 設定済みのリソースタイプと異なる場合は予約失敗
        if (StoredResourceType != resourceType) return false;

        AllocatedAmount += allocated;

        UpdateUI();

        return true;
    }

    public void StoreResource(Vector3Int dir, int amount)
    {
        // 予約量を超えて入れようとした場合は中断
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
using UnityEngine;

public sealed class StorageCell : ConnectableCellBase, IContainable, IDataProvidable
{
    [Header("ストレージセルの設定")]
    [SerializeField] private int capacity;
    [SerializeField] private StorageProvider dataProvider;

    public int Capacity => capacity;
    public int CurrentLoad { get; private set; }
    public int AllocatedAmount { get; private set; }
    public int ReservedAmount { get; private set; }
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
        // 初めてのリソース追加
        if (StoredResourceType == ResourceType.None)
        {
            StoredResourceType = resourceType;
        }

        // 設定済みのリソースタイプと異なる場合、追加しない
        if (StoredResourceType != resourceType) return 0;

        // 既に容量限界に達している場合は0を返す
        // 入れようとしている値が空き容量を越えている場合は空き容量を返す
        // そうでない場合は指定された量を予約する
        var available = capacity - CurrentLoad - AllocatedAmount;
        var allocated = Mathf.Min(available, amount);
        AllocatedAmount += allocated;
        
        if (allocated > 0) UpdateUI();
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

    /// <summary>
    /// 指定した量のリソースをストレージから予約する。
    /// 予約可能な最大量は現在のストレージ内のリソース量に制限される。
    /// </summary>
    /// <param name="amount">予約したいリソース量</param>
    /// <param name="resourceType">取り出すリソースの種類</param>
    /// <returns>実際に予約できたリソース量</returns>
    public int ReserveResource(int amount, out ResourceType resourceType)
    {
        resourceType = StoredResourceType;
        if (StoredResourceType == ResourceType.None) return 0;

        // 予約可能な量を計算（現在のリソース量から既予約量を引いた分だけ予約可能）
        var maxReservable = CurrentLoad - ReservedAmount;
        var reservable = Mathf.Min(amount, Mathf.Max(0, maxReservable));
        ReservedAmount += reservable;
        if (reservable > 0) UpdateUI();
        return reservable;
    }

    /// <summary>
    /// ストレージからリソースを取り出します。取り出せる量は現在の容量に依存する
    /// </summary>
    /// <param name="amount">取り出す要求値</param>
    public void TakeResource(int amount)
    {
        if (amount > ReservedAmount) return;

        // 現在の容量から取り出す
        CurrentLoad -= amount;
        ReservedAmount -= amount;
        UpdateUI();
        if (CurrentLoad == 0) StoredResourceType = ResourceType.None;
    }
}
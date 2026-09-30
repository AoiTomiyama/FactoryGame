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
        if (amount <= 0 || resourceType == ResourceType.None) return false;

        var available = capacity - CurrentLoad - AllocatedAmount;
        // 搬送側は要求量をそのまま確定するため、一部だけの予約は受け付けない。
        if (amount > available ||
            (StoredResourceType != ResourceType.None && StoredResourceType != resourceType)) return false;

        // 初めてのリソース追加
        if (StoredResourceType == ResourceType.None)
        {
            StoredResourceType = resourceType;
        }

        AllocatedAmount += amount;

        UpdateUI();

        return true;
    }

    public void StoreResource(Vector3Int dir, int amount)
    {
        // 予約していない量は確定せず、容量と予約量を保つ。
        if (amount <= 0 || amount > AllocatedAmount || amount > capacity - CurrentLoad) return;

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
        if (CurrentLoad <= 0 || requestedAmount <= 0 || StoredResourceType == ResourceType.None) return false;

        // 返却量を計算し、現在量を減らす
        amount = Mathf.Min(requestedAmount, CurrentLoad);
        CurrentLoad = Mathf.Max(0, CurrentLoad - requestedAmount);

        // 現在量と予約量が両方0になった時だけ資源種別を解放する。
        if (CurrentLoad == 0 && AllocatedAmount == 0)
        {
            StoredResourceType = ResourceType.None;
        }

        UpdateUI();
        return true;
    }
}

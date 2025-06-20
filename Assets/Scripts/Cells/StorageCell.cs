using UnityEngine;
using UnityEngine.UI;

public sealed class StorageCell : ConnectableCellBase
{
    [Header("ストレージセルの設定")]
    [SerializeField] [InspectorReadOnly] private int currentLoad;
    [SerializeField] private int capacity;
    
    [Header("UI設定")]
    [SerializeField] private Image storageAmountBar;
    [SerializeField] private Image resourceIconImage;
    [SerializeField] private ResourceIconSO resourceIconSo;

    private ResourceType _storedResourceType = ResourceType.None;
    private ResourceType StoredResourceType
    {
        get => _storedResourceType;
        set
        {
            _storedResourceType = value;
            UpdateResourceIcon();
        }
    }
    
    private int CurrentLoad
    {
        get => currentLoad;
        set
        {
            currentLoad = value;
            UpdateUI();
        }
    }

    protected override void Start()
    {
        base.Start();
        UpdateUI();
        UpdateResourceIcon();
    }

    /// <summary>
    /// ストレージにリソースを追加します。入りきらなかった分は戻り値として返される
    /// </summary>
    /// <param name="amount">ストレージに入れる量</param>
    /// <param name="resourceType">リソースの種類</param>
    /// <returns>ストレージに入りきらなかった量</returns>
    public int StoreResource(int amount, ResourceType resourceType)
    {
        // 初めてのリソース追加
        if (StoredResourceType == ResourceType.None)
        {
            StoredResourceType = resourceType;
        }

        if (StoredResourceType != resourceType)
        {
            // 設定済みのリソースタイプと異なる場合、追加できないので全量を戻す
            return amount;
        }

        if (CurrentLoad + amount > capacity)
        {
            // 容量を超えないように調整
            var overflow = CurrentLoad + amount - capacity;
            CurrentLoad = capacity;
            return overflow;
        }

        CurrentLoad += amount;
        return 0;
    }

    /// <summary>
    /// ストレージからリソースを取り出します。取り出せる量は現在の容量に依存する
    /// </summary>
    /// <param name="amount">取り出す要求値</param>
    /// <param name="resourceType">取り出すリソースの種類</param>
    /// <returns>取り出しに成功した量</returns>
    public int TakeResource(int amount, ResourceType resourceType)
    {
        if (StoredResourceType != resourceType)
        {
            // 取り出せないので、0を返す
            return 0;
        }

        // 現在の容量から取り出す
        if (CurrentLoad - amount >= 0)
        {
            CurrentLoad -= amount;
            if (CurrentLoad == 0)
            {
                // 取り出した後に容量が0になった場合、リソースタイプをリセット
                StoredResourceType = ResourceType.None;
            }
            return amount;
        }

        // 現在の容量が不足している場合は、現在の容量を全て取り出す
        var takenAmount = CurrentLoad;
        CurrentLoad = 0;
        
        // リソースを全部取り出した後は、リソースタイプをリセット
        StoredResourceType = ResourceType.None;

        // 取り出せる量は現在の容量まで
        return takenAmount;
    }
    
    /// <summary>
    /// 容量上限に達しているかどうかを確認。
    /// </summary>
    public bool IsFull() => CurrentLoad == capacity;
    
    private void UpdateUI()
    {
        if (storageAmountBar == null) return;

        // ストレージの容量に応じてUIを更新
        storageAmountBar.fillAmount = (float)CurrentLoad / capacity;
    }

    private void UpdateResourceIcon()
    {
        // リソースタイプがNoneの場合はアイコンを非表示にする
        resourceIconImage.enabled = StoredResourceType != ResourceType.None;
        
        // アイコンを更新
        resourceIconImage.sprite = resourceIconSo.GetIcon(StoredResourceType);
    }
}
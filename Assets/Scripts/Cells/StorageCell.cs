using System;
using UnityEngine;
using UnityEngine.UI;

public sealed class StorageCell : ConnectableCellBase, IContainable
{
    [Header("ストレージセルの設定")]
    [SerializeField] [InspectorReadOnly] private int currentLoad;
    [SerializeField] private int capacity;

    [Header("UI設定")]
    [SerializeField] private Image storageAmountBar;
    [SerializeField] private Image allocatedAmountBar;
    [SerializeField] private Image resourceIconImage;
    [SerializeField] private ResourceSO resourceSo;

    public int StorageAmount
    {
        get => currentLoad;
        set => currentLoad = value;
    }

    private int _allocatedAmount;
    private int _reservedAmount;

    private ResourceType _storedResourceType = ResourceType.None;

    public ResourceType StoredResourceType
    {
        get => _storedResourceType;
        private set
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
        allocatedAmountBar.fillAmount = (float)_allocatedAmount / capacity;
        UpdateUI();
        UpdateResourceIcon();
    }

    public int AllocateStorage(int amount, ResourceType resourceType)
    {
        // 初めてのリソース追加
        if (_storedResourceType == ResourceType.None)
        {
            _storedResourceType = resourceType;
        }

        // 設定済みのリソースタイプと異なる場合、追加しない
        if (_storedResourceType != resourceType) return 0;

        // 既に容量限界に達している場合は0を返す
        // 入れようとしている値が空き容量を越えている場合は空き容量を返す
        // そうでない場合は指定された量を予約する
        var available = capacity - CurrentLoad - _allocatedAmount;
        var allocated = Mathf.Min(available, amount);
        _allocatedAmount += allocated;
        allocatedAmountBar.fillAmount = (float)_allocatedAmount / capacity;
        return allocated;
    }

    public void StoreResource(int amount)
    {
        if (amount > _allocatedAmount) return;
        
        // 現在量に追加し、予約量を減らす。
        CurrentLoad += amount;
        _allocatedAmount -= amount;

        allocatedAmountBar.fillAmount = (float)_allocatedAmount / capacity;
        UpdateResourceIcon();
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
        var maxReservable = CurrentLoad - _reservedAmount;
        var reservable = Mathf.Min(amount, Mathf.Max(0, maxReservable));
        _reservedAmount += reservable;
        return reservable;
    }

    /// <summary>
    /// ストレージからリソースを取り出します。取り出せる量は現在の容量に依存する
    /// </summary>
    /// <param name="amount">取り出す要求値</param>
    public void TakeResource(int amount)
    {
        if (amount > _reservedAmount) return;
        
        // 現在の容量から取り出す
        CurrentLoad -= amount;
        _reservedAmount -= amount;
        if (CurrentLoad == 0) StoredResourceType = ResourceType.None;
    }

    public bool IsFull() => CurrentLoad + _allocatedAmount == capacity;

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
        resourceIconImage.sprite = resourceSo.GetIcon(StoredResourceType);
    }
}
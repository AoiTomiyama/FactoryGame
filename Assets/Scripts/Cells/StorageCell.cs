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
    }

    /// <summary>
    /// ストレージからリソースを取り出します。取り出せる量は現在の容量に依存する
    /// </summary>
    /// <param name="amount">取り出す要求値</param>
    /// <param name="resourceType">取り出すリソースの種類</param>
    /// <returns>取り出しに成功した量</returns>
    public int TakeResource(int amount, out ResourceType resourceType)
    {
        resourceType = StoredResourceType;

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
using System.Collections.Generic;
using UnityEngine;

public sealed class StorageCell : ConnectableCellBase, IContainable, IUIRenderable
{
    [Header("ストレージセルの設定")]
    [SerializeField] private int capacity;

    private int _currentLoad;
    private int _allocatedAmount;
    private int _reservedAmount;

    private readonly Dictionary<Label, UIStatusRowBase> _renderedUI = new();
    private Dictionary<Label, UIElementDataBase> _uiElementDataBases;
    public bool IsUIActive { get; set; }
    private ResourceType StoredResourceType { get; set; } = ResourceType.None;

    private enum Label
    {
        CellName,
        Location,
        Amount,
        Allocated,
        Reserved
    }

    protected override void Start()
    {
        base.Start();
        _uiElementDataBases = new()
        {
            { Label.CellName, new TextElementData("Name", "Storage") },
            { Label.Location, new TextElementData("Location", $"({XIndex}, {ZIndex})") },
            { Label.Amount, new StorageElementData("A", capacity, _currentLoad, StoredResourceType) },
            { Label.Allocated, new GaugeElementData("B", capacity, _allocatedAmount) },
            { Label.Reserved, new GaugeElementData("C", capacity, _reservedAmount) }
        };
    }

    public void UpdateUI()
    {
        if (!IsUIActive) return;

        foreach (var (label, data) in _uiElementDataBases)
        {
            switch (data)
            {
                case StorageElementData storageData:
                    storageData.ResourceType = StoredResourceType;
                    storageData.Current = _currentLoad;
                    break;
                case GaugeElementData gaugeData:
                    gaugeData.Current = label switch
                    {
                        Label.Allocated => _allocatedAmount,
                        Label.Reserved => _reservedAmount,
                        _ => 0
                    };
                    break;
            }

            if (_renderedUI.TryGetValue(label, out var uiElement))
            {
                uiElement.RenderUIByData(data);
            }
            else
            {
                _renderedUI[label] = CellStatusView.Instance.CreateStatusRow(data);
            }
        }
    }

    public void ResetUI() => _renderedUI.Clear();

    public int AllocateStorage(Vector3Int dir, int amount, ResourceType resourceType)
    {
        Debug.Log("AllocateStorage");
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
        var available = capacity - _currentLoad - _allocatedAmount;
        var allocated = Mathf.Min(available, amount);
        _allocatedAmount += allocated;
        if (allocated > 0) UpdateUI();
        return allocated;
    }

    public void StoreResource(Vector3Int dir, int amount)
    {
        if (amount > _allocatedAmount) return;

        // 現在量に追加し、予約量を減らす。
        _currentLoad += amount;
        _allocatedAmount -= amount;
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
        var maxReservable = _currentLoad - _reservedAmount;
        var reservable = Mathf.Min(amount, Mathf.Max(0, maxReservable));
        _reservedAmount += reservable;
        UpdateUI();
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
        _currentLoad -= amount;
        _reservedAmount -= amount;
        UpdateUI();
        if (_currentLoad == 0) StoredResourceType = ResourceType.None;
    }
}
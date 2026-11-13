using UnityEngine;
using System;

public sealed class StorageCellAdapter : ConnectableCellBaseAdapter, IContainableApplication, IExportableApplication, IDataProvidableApplication
{
    [Header("ストレージセルの設定")]
    [SerializeField] private int capacity;
    [SerializeField] private StorageProviderDefinition dataProvider;

    public int Capacity => EffectiveStat(capacity, EquipmentStatDomain.Capacity);
    public override CellTypeDomain ProgressionType => CellTypeDomain.Storage;
    protected override void ValidateBaseStats() => ValidateStat(capacity, EquipmentStatDomain.Capacity);
    protected override void OnStatsChanged() => UpdateUI();

    public int CurrentLoad { get; private set; }
    public int AllocatedAmount { get; private set; }
    public event Action StatusChanged;
    public ResourceTypeDomain StoredResourceType { get; private set; } = ResourceTypeDomain.None;
    public IUIDataProviderApplication GetDataProvider() => CellStatusProviderAdapter.Create(this, dataProvider);
    public SaveGameApplication.Slot[] CaptureInventory() => CurrentLoad == 0 ? Array.Empty<SaveGameApplication.Slot>() :
        new[] { SaveGameApplication.SlotValue(0,StoredResourceType,CurrentLoad) };
    public void ValidateSavedInventory(SaveGameApplication.Slot[] values, SharedStatEffectsApplication effects)
    {
        var limit = EffectiveStatsRulesDomain.Calculate(capacity,effects.GetModifier(ProgressionType,EquipmentStatDomain.Capacity));
        if (values.Length > 1 || values.Length == 1 && (values[0].port != 0 || values[0].amount > limit))
            throw new InvalidOperationException("Saved storage exceeds capacity or has invalid slots.");
    }
    public void RestoreInventory(SaveGameApplication.Slot[] values)
    {
        if (CurrentLoad != 0 || AllocatedAmount != 0) throw new InvalidOperationException("Restore requires fresh storage.");
        if (values.Length == 0) return;
        StoredResourceType = SaveKeysApplication.Resource(values[0].resource); CurrentLoad = values[0].amount;
    }

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
        StatusChanged?.Invoke();
    }

    public bool AllocateStorage(int dir, int amount, ResourceTypeDomain resourceType)
    {
        if (!HasPort(dir, CellPortUseDomain.Input)) return false;
        if (!ResourceStorageRulesDomain.TryReserve(Capacity, Stock, amount, resourceType, out var next)) return false;
        ApplyStock(next);
        return true;
    }

    public void StoreResource(int dir, int amount)
    {
        if (ResourceStorageRulesDomain.TryCommit(Capacity, Stock, amount, out var next)) ApplyStock(next);
    }

    public void CancelStorage(int dir, int amount, ResourceTypeDomain resourceType)
    {
        if (ResourceStorageRulesDomain.TryCancel(Stock, amount, resourceType, out var next)) ApplyStock(next);
    }

    public Vector3 GetPosition(int portId) => PortPosition(portId);

    public bool TryExport(int portId, int requestedAmount, out int amount, out ResourceTypeDomain type)
    {
        amount = 0; type = ResourceTypeDomain.None;
        if (!HasPort(portId, CellPortUseDomain.Output)) return false;
        if (!ResourceStorageRulesDomain.TryExport(Stock, requestedAmount, out var next, out amount, out type)) return false;
        ApplyStock(next);
        return true;
    }
}

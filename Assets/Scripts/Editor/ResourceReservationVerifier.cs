using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 容量の境界と交差セル経由の予約を、シーンを変更せずに検証する。
/// バッチ実行: -executeMethod ResourceReservationVerifier.Run
/// </summary>
public static class ResourceReservationVerifier
{
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

    [MenuItem("Tools/FactoryGame/Verify Resource Reservations")]
    public static void Run()
    {
        VerifyQuantityRules();
        VerifyTransferOperation();
        VerifyReservationTickets();
        VerifyStorage();
        VerifyCrafter();
        VerifyCrossing();
        VerifyConveyor();
        Debug.Log("Resource reservation checks passed: pure quantity rules, transfer operation, reservation tickets, storage, crafter, crossing, conveyor.");
    }

    // この検証は数値と資源種別だけを使い、Unity のセルやシーンを生成しない。
    private static void VerifyQuantityRules()
    {
        var empty = new ResourceStorageRules.Stock(ResourceType.None, 0, 0);
        foreach (var capacity in new[] { int.MinValue, -1, 0 })
            Require(!ResourceStorageRules.TryReserve(capacity, empty, 1, ResourceType.Stone, out var rejected) &&
                    SameStock(rejected, empty), "Rules: nonpositive capacity does not reserve or change type");
        var stock = new ResourceStorageRules.Stock(ResourceType.Stone, 1, 2);
        foreach (var amount in new[] { int.MinValue, -1, 0 })
        {
            Require(!ResourceStorageRules.TryReserve(5, stock, amount, ResourceType.Stone, out var next) &&
                    SameStock(next, stock), "Rules: invalid reservation keeps all values");
            Require(!ResourceStorageRules.TryCommit(5, stock, amount, out next) && SameStock(next, stock),
                "Rules: invalid commit keeps all values");
            Require(!ResourceStorageRules.TryCancel(stock, amount, ResourceType.Stone, out next) &&
                    SameStock(next, stock), "Rules: invalid cancellation keeps all values");
            Require(!ResourceStorageRules.TryExport(stock, amount, out next, out var exported, out _) &&
                    exported == 0 && SameStock(next, stock), "Rules: invalid export keeps all values");
        }
        Require(ResourceStorageRules.GetAvailableCapacity(5, stock) == 2 &&
                ResourceStorageRules.GetReservableAmount(5, stock, ResourceType.Wood) == 0 &&
                ResourceStorageRules.GetReservableAmount(5, stock, ResourceType.None) == 0,
            "Rules: capacity includes current and reserved stock, and type must match");
        Require(!ResourceStorageRules.TryReserve(5, stock, 3, ResourceType.Stone, out var failed) &&
                SameStock(failed, stock), "Rules: a partial reservation is never accepted");
        Require(!ResourceStorageRules.TryCommit(5, stock, 3, out failed) && SameStock(failed, stock) &&
                !ResourceStorageRules.TryCommit(1, stock, 1, out failed) && SameStock(failed, stock),
            "Rules: unreserved quantities and capacity overflow cannot be committed");
        Require(!ResourceStorageRules.TryCancel(stock, 2, ResourceType.Wood, out failed) &&
                SameStock(failed, stock), "Rules: another type cannot cancel a reservation");

        Require(ResourceStorageRules.TryReserve(5, empty, 3, ResourceType.Stone, out stock),
            "Rules: first reservation takes three slots");
        Require(ResourceStorageRules.TryReserve(5, stock, 2, ResourceType.Stone, out stock) &&
                !ResourceStorageRules.TryReserve(5, stock, 1, ResourceType.Stone, out failed) &&
                SameStock(failed, stock), "Rules: simultaneous reservations fill capacity exactly");
        Require(ResourceStorageRules.TryCommit(5, stock, 3, out stock) && stock.Amount == 3 && stock.Reserved == 2 &&
                ResourceStorageRules.GetAvailableCapacity(5, stock) == 0,
            "Rules: committing one reservation preserves the other reservation");
        Require(ResourceStorageRules.TryExport(stock, int.MaxValue, out stock, out var quantity, out var type) &&
                quantity == 3 && type == ResourceType.Stone && stock.Type == ResourceType.Stone && stock.Reserved == 2,
            "Rules: exporting all current stock retains the reserved type");
        Require(ResourceStorageRules.GetReservableAmount(5, stock, ResourceType.Wood) == 0 &&
                ResourceStorageRules.TryCancel(stock, 2, ResourceType.Stone, out stock) &&
                SameStock(stock, empty), "Rules: the final cancellation releases the empty stock type");

        Require(ResourceStorageRules.TryReserve(int.MaxValue, empty, int.MaxValue, ResourceType.Wood, out stock) &&
                ResourceStorageRules.GetAvailableCapacity(int.MaxValue, stock) == 0 &&
                !ResourceStorageRules.TryReserve(int.MaxValue, stock, 1, ResourceType.Wood, out failed) &&
                ResourceStorageRules.TryCommit(int.MaxValue, stock, int.MaxValue, out stock) &&
                stock.Amount == int.MaxValue && stock.Reserved == 0,
            "Rules: the largest valid capacity cannot overflow");
        Require(ResourceStorageRules.TryExport(stock, int.MaxValue, out stock, out quantity, out type) &&
                quantity == int.MaxValue && SameStock(stock, empty),
            "Rules: exporting the largest amount clears stock exactly");
        foreach (var invalid in new[]
                 {
                     new ResourceStorageRules.Stock(ResourceType.None, 1, 0),
                     new ResourceStorageRules.Stock(ResourceType.Stone, -1, 0),
                     new ResourceStorageRules.Stock(ResourceType.Stone, 0, -1),
                     new ResourceStorageRules.Stock(ResourceType.Stone, int.MaxValue, int.MaxValue)
                 })
            Require(ResourceStorageRules.GetAvailableCapacity(int.MaxValue, invalid) == 0 &&
                    !ResourceStorageRules.TryReserve(int.MaxValue, invalid, 1, ResourceType.Stone, out failed) &&
                    SameStock(failed, invalid), "Rules: invalid or overfull stock cannot reserve capacity");
    }

    private static bool SameStock(ResourceStorageRules.Stock first, ResourceStorageRules.Stock second)
        => first.Type == second.Type && first.Amount == second.Amount && first.Reserved == second.Reserved;

    private static void VerifyReservationTickets()
    {
        var storage = CreateCell<StorageCell>();
        var crafter = CreateCell<CrafterCell>();
        var conveyor = CreateCell<ConveyorCell>();
        var crossing = CreateCell<CrossingCell>();
        try
        {
            SetPrivateField(storage, "capacity", 5);
            ResourceReservation second = null;
            Require(ResourceReservation.TryCreate(storage, Vector3Int.right, 2, ResourceType.Stone,
                    out var first) &&
                    ResourceReservation.TryCreate(storage, Vector3Int.right, 2, ResourceType.Stone,
                    out second) && first.Id != second.Id,
                "Ticket: equal reservations have different IDs");
            Require(first.TryCancel() && !first.TryCancel() && !first.TryCommit() &&
                    storage.AllocatedAmount == 2,
                "Ticket: cancellation releases only its own amount once");
            Require(second.TryCommit() && !second.TryCommit() && !second.TryCancel() &&
                    storage.AllocatedAmount == 0 && storage.CurrentLoad == 2,
                "Ticket: commit moves the reserved amount once");

            SetPrivateField(crafter, "ingredientCapacity", 5);
            var inputs = GetPrivateField<Dictionary<Vector3Int, CrafterCell.ResourceInputData>>(
                crafter, "_resourceInputs");
            inputs[Vector3Int.right] = new CrafterCell.ResourceInputData();
            Require(ResourceReservation.TryCreate(crafter, Vector3Int.right, 3, ResourceType.Wood,
                    out var ingredient) && ingredient.TryCancel() && !ingredient.TryCancel() &&
                    inputs[Vector3Int.right].Allocated == 0 &&
                    inputs[Vector3Int.right].Type == ResourceType.None,
                "Ticket: crafter cancellation restores the input state");

            Require(ResourceReservation.TryCreate(conveyor, Vector3Int.right, 2, ResourceType.Stone,
                    out var conveyorInput) && conveyorInput.TryCancel() &&
                    !conveyorInput.TryCancel() &&
                    ResourceReservation.TryCreate(conveyor, Vector3Int.right, 1, ResourceType.Wood,
                        out var replacement) && replacement.TryCancel(),
                "Ticket: conveyor input can be canceled and reserved again");

            var adjacent = GetPrivateField<Dictionary<Vector3Int, IContainable>>(
                crossing, "_adjacentContainers");
            adjacent[Vector3Int.right] = storage;
            Require(ResourceReservation.TryCreate(crossing, Vector3Int.right, 2, ResourceType.Stone,
                    out var delegated) && storage.AllocatedAmount == 2 &&
                    delegated.TryCancel() && !delegated.TryCancel() &&
                    storage.AllocatedAmount == 0 && storage.CurrentLoad == 2,
                "Ticket: crossing cancels its downstream reservation once");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(crossing.gameObject);
            UnityEngine.Object.DestroyImmediate(conveyor.gameObject);
            UnityEngine.Object.DestroyImmediate(crafter.gameObject);
            UnityEngine.Object.DestroyImmediate(storage.gameObject);
        }
    }

    private static void VerifyTransferOperation()
    {
        var source = CreateCell<StorageCell>();
        var target = CreateCell<StorageCell>();
        try
        {
            var completed = new ResourceTransferOperation(source, target, 12, ResourceType.Stone, 4);
            Require(completed.Source == source && completed.Target == target &&
                    completed.ResourceId == 12 && completed.Type == ResourceType.Stone && completed.Amount == 4,
                "Transfer: one record keeps source, target, ID, type and amount");
            Require(completed.TryMarkReserved() && !completed.TryMarkReserved() &&
                    completed.TryMarkAnimating() && completed.TryComplete() &&
                    !completed.TryComplete() && !completed.TryCancel(),
                "Transfer: completion happens once");

            var retried = new ResourceTransferOperation(source, target, 0, ResourceType.Wood, 2);
            Require(retried.TryMarkReserved() && retried.TryAttachId(-13) &&
                    !retried.TryAttachId(14) && retried.TryMarkAnimating() &&
                    retried.TryWaitForNewTarget() && retried.ResourceId == -13 && retried.Target == null &&
                    retried.TrySetTarget(target) && retried.TryMarkReserved() &&
                    retried.TryMarkAnimating() && retried.TryCancel() &&
                    !retried.TryCancel() && !retried.TryComplete(),
                "Transfer: reconnect keeps ID and cancellation happens once");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(source.gameObject);
            UnityEngine.Object.DestroyImmediate(target.gameObject);
        }
    }

    private static void VerifyStorage()
    {
        var storage = CreateCell<StorageCell>();
        try
        {
            SetPrivateField(storage, "capacity", 5);

            Require(!storage.AllocateStorage(Vector3Int.right, 6, ResourceType.Stone), "Storage: reject insufficient capacity");
            Require(storage.StoredResourceType == ResourceType.None, "Storage: rejection leaves type unchanged");
            Require(storage.AllocateStorage(Vector3Int.right, 4, ResourceType.Stone), "Storage: reserve four");
            Require(!storage.AllocateStorage(Vector3Int.right, 2, ResourceType.Stone), "Storage: reject partial reservation");
            Require(!storage.AllocateStorage(Vector3Int.right, 0, ResourceType.Stone), "Storage: reject zero amount");
            Require(!storage.AllocateStorage(Vector3Int.right, 1, ResourceType.None), "Storage: reject missing type");
            Require(storage.AllocatedAmount == 4 && storage.CurrentLoad == 0, "Storage: failed requests leave state intact");

            storage.StoreResource(Vector3Int.right, 5);
            Require(storage.AllocatedAmount == 4 && storage.CurrentLoad == 0, "Storage: reject unreserved amount");
            storage.StoreResource(Vector3Int.right, 4);
            Require(storage.AllocatedAmount == 0 && storage.CurrentLoad == 4, "Storage: commit exact reservation");

            Require(storage.AllocateStorage(Vector3Int.right, 1, ResourceType.Stone), "Storage: reserve remaining space");
            storage.CancelStorage(Vector3Int.right, 1, ResourceType.Stone);
            Require(storage.AllocatedAmount == 0 && storage.CurrentLoad == 4,
                "Storage: cancellation preserves committed stock");
            Require(storage.AllocateStorage(Vector3Int.right, 1, ResourceType.Stone),
                "Storage: canceled capacity can be reserved again");
            Require(storage.TryExport(Vector3.zero, 4, out var exported, out _) && exported == 4,
                "Storage: export existing contents");
            Require(storage.StoredResourceType == ResourceType.Stone, "Storage: retain type while delivery is reserved");
            Require(!storage.AllocateStorage(Vector3Int.right, 1, ResourceType.Wood), "Storage: reject another type in flight");
            storage.StoreResource(Vector3Int.right, 1);
            Require(storage.CurrentLoad == 1 && storage.AllocatedAmount == 0, "Storage: complete in-flight delivery");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(storage.gameObject);
        }
    }

    private static void VerifyCrafter()
    {
        var crafter = CreateCell<CrafterCell>();
        try
        {
            SetPrivateField(crafter, "ingredientCapacity", 5);
            var inputs = GetPrivateField<Dictionary<Vector3Int, CrafterCell.ResourceInputData>>(crafter, "_resourceInputs");
            inputs[Vector3Int.right] = new CrafterCell.ResourceInputData();

            Require(!crafter.AllocateStorage(Vector3Int.right, 6, ResourceType.Stone),
                "Crafter: reject insufficient capacity");
            Require(inputs[Vector3Int.right].Type == ResourceType.None,
                "Crafter: rejection leaves type unchanged");
            Require(crafter.AllocateStorage(Vector3Int.right, 4, ResourceType.Stone), "Crafter: reserve four");
            Require(!crafter.AllocateStorage(Vector3Int.right, 2, ResourceType.Stone), "Crafter: reject partial reservation");
            Require(!crafter.AllocateStorage(Vector3Int.left, 1, ResourceType.Stone), "Crafter: reject absent input");
            Require(!crafter.AllocateStorage(Vector3Int.right, 1, ResourceType.Wood), "Crafter: reject another type");
            Require(inputs[Vector3Int.right].Allocated == 4, "Crafter: failed requests leave reservation intact");

            crafter.StoreResource(Vector3Int.right, 5);
            Require(inputs[Vector3Int.right].Amount == 0 && inputs[Vector3Int.right].Allocated == 4,
                "Crafter: reject unreserved amount");
            crafter.StoreResource(Vector3Int.right, 4);
            Require(inputs[Vector3Int.right].Amount == 4 && inputs[Vector3Int.right].Allocated == 0,
                "Crafter: commit exact reservation");
            Require(crafter.AllocateStorage(Vector3Int.right, 1, ResourceType.Stone), "Crafter: reserve final space");
            crafter.CancelStorage(Vector3Int.right, 1, ResourceType.Stone);
            Require(inputs[Vector3Int.right].Allocated == 0 && inputs[Vector3Int.right].Amount == 4,
                "Crafter: cancellation preserves committed ingredients");
            Require(crafter.AllocateStorage(Vector3Int.right, 1, ResourceType.Stone),
                "Crafter: canceled capacity can be reserved again");
            crafter.StoreResource(Vector3Int.right, 1);
            Require(inputs[Vector3Int.right].Amount == 5 && inputs[Vector3Int.right].Allocated == 0,
                "Crafter: never exceed capacity");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(crafter.gameObject);
        }
    }

    private static void VerifyConveyor()
    {
        var conveyor = CreateCell<ConveyorCell>();
        try
        {
            Require(!conveyor.AllocateStorage(Vector3Int.right, 0, ResourceType.Stone),
                "Conveyor: reject zero amount");
            Require(!conveyor.AllocateStorage(Vector3Int.right, 1, ResourceType.None),
                "Conveyor: reject missing type");
            Require(conveyor.AllocateStorage(Vector3Int.right, 5, ResourceType.Stone),
                "Conveyor: accept one full batch");
            Require(!conveyor.AllocateStorage(Vector3Int.right, 1, ResourceType.Stone),
                "Conveyor: reject a second batch");
            conveyor.CancelStorage(Vector3Int.right, 5, ResourceType.Stone);
            Require(conveyor.AllocateStorage(Vector3Int.right, 1, ResourceType.Wood),
                "Conveyor: cancellation frees the input slot");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(conveyor.gameObject);
        }
    }

    private static void VerifyCrossing()
    {
        var storage = CreateCell<StorageCell>();
        var crossing = CreateCell<CrossingCell>();
        try
        {
            SetPrivateField(storage, "capacity", 5);
            var adjacent = GetPrivateField<Dictionary<Vector3Int, IContainable>>(
                crossing, "_adjacentContainers");
            adjacent[Vector3Int.right] = storage;

            Require(crossing.AllocateStorage(Vector3Int.right, 4, ResourceType.Stone),
                "Crossing: forward full reservation");
            Require(!crossing.AllocateStorage(Vector3Int.right, 2, ResourceType.Stone),
                "Crossing: forward partial rejection");
            Require(!crossing.AllocateStorage(Vector3Int.left, 1, ResourceType.Stone),
                "Crossing: reject absent output");
            Require(storage.AllocatedAmount == 4, "Crossing: target reservation remains exact");
            crossing.CancelStorage(Vector3Int.right, 4, ResourceType.Stone);
            Require(storage.AllocatedAmount == 0 && storage.StoredResourceType == ResourceType.None,
                "Crossing: cancellation reaches target and releases type");
            Require(crossing.AllocateStorage(Vector3Int.right, 5, ResourceType.Wood),
                "Crossing: canceled path accepts a different resource");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(crossing.gameObject);
            UnityEngine.Object.DestroyImmediate(storage.gameObject);
        }
    }

    private static T CreateCell<T>() where T : CellBase
    {
        var root = new GameObject($"ReservationCheck:{typeof(T).Name}");
        new GameObject("Model").transform.SetParent(root.transform);
        return root.AddComponent<T>();
    }

    private static void SetPrivateField(object target, string name, object value)
    {
        var field = target.GetType().GetField(name, PrivateInstance);
        if (field == null) throw new InvalidOperationException($"Missing field: {target.GetType().Name}.{name}");
        field.SetValue(target, value);
    }

    private static T GetPrivateField<T>(object target, string name)
    {
        var field = target.GetType().GetField(name, PrivateInstance);
        if (field == null) throw new InvalidOperationException($"Missing field: {target.GetType().Name}.{name}");
        return (T)field.GetValue(target);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException($"Resource reservation check failed: {message}");
    }
}

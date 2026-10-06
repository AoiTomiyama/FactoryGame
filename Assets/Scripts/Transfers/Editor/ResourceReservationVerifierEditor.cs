using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 容量の境界と交差セル経由の予約を、シーンを変更せずに検証する。
/// バッチ実行: -executeMethod ResourceReservationVerifierEditor.Run
/// </summary>
public static class ResourceReservationVerifierEditor
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
        var empty = new ResourceStorageRulesDomain.Stock(ResourceTypeDomain.None, 0, 0);
        foreach (var capacity in new[] { int.MinValue, -1, 0 })
            Require(!ResourceStorageRulesDomain.TryReserve(capacity, empty, 1, ResourceTypeDomain.Stone, out var rejected) &&
                    SameStock(rejected, empty), "Rules: nonpositive capacity does not reserve or change type");
        var stock = new ResourceStorageRulesDomain.Stock(ResourceTypeDomain.Stone, 1, 2);
        foreach (var amount in new[] { int.MinValue, -1, 0 })
        {
            Require(!ResourceStorageRulesDomain.TryReserve(5, stock, amount, ResourceTypeDomain.Stone, out var next) &&
                    SameStock(next, stock), "Rules: invalid reservation keeps all values");
            Require(!ResourceStorageRulesDomain.TryCommit(5, stock, amount, out next) && SameStock(next, stock),
                "Rules: invalid commit keeps all values");
            Require(!ResourceStorageRulesDomain.TryCancel(stock, amount, ResourceTypeDomain.Stone, out next) &&
                    SameStock(next, stock), "Rules: invalid cancellation keeps all values");
            Require(!ResourceStorageRulesDomain.TryExport(stock, amount, out next, out var exported, out _) &&
                    exported == 0 && SameStock(next, stock), "Rules: invalid export keeps all values");
        }
        Require(ResourceStorageRulesDomain.GetAvailableCapacity(5, stock) == 2 &&
                ResourceStorageRulesDomain.GetReservableAmount(5, stock, ResourceTypeDomain.Wood) == 0 &&
                ResourceStorageRulesDomain.GetReservableAmount(5, stock, ResourceTypeDomain.None) == 0,
            "Rules: capacity includes current and reserved stock, and type must match");
        Require(!ResourceStorageRulesDomain.TryReserve(5, stock, 3, ResourceTypeDomain.Stone, out var failed) &&
                SameStock(failed, stock), "Rules: a partial reservation is never accepted");
        Require(!ResourceStorageRulesDomain.TryCommit(5, stock, 3, out failed) && SameStock(failed, stock) &&
                !ResourceStorageRulesDomain.TryCommit(1, stock, 1, out failed) && SameStock(failed, stock),
            "Rules: unreserved quantities and capacity overflow cannot be committed");
        Require(!ResourceStorageRulesDomain.TryCancel(stock, 2, ResourceTypeDomain.Wood, out failed) &&
                SameStock(failed, stock), "Rules: another type cannot cancel a reservation");

        Require(ResourceStorageRulesDomain.TryReserve(5, empty, 3, ResourceTypeDomain.Stone, out stock),
            "Rules: first reservation takes three slots");
        Require(ResourceStorageRulesDomain.TryReserve(5, stock, 2, ResourceTypeDomain.Stone, out stock) &&
                !ResourceStorageRulesDomain.TryReserve(5, stock, 1, ResourceTypeDomain.Stone, out failed) &&
                SameStock(failed, stock), "Rules: simultaneous reservations fill capacity exactly");
        Require(ResourceStorageRulesDomain.TryCommit(5, stock, 3, out stock) && stock.Amount == 3 && stock.Reserved == 2 &&
                ResourceStorageRulesDomain.GetAvailableCapacity(5, stock) == 0,
            "Rules: committing one reservation preserves the other reservation");
        Require(ResourceStorageRulesDomain.TryExport(stock, int.MaxValue, out stock, out var quantity, out var type) &&
                quantity == 3 && type == ResourceTypeDomain.Stone && stock.Type == ResourceTypeDomain.Stone && stock.Reserved == 2,
            "Rules: exporting all current stock retains the reserved type");
        Require(ResourceStorageRulesDomain.GetReservableAmount(5, stock, ResourceTypeDomain.Wood) == 0 &&
                ResourceStorageRulesDomain.TryCancel(stock, 2, ResourceTypeDomain.Stone, out stock) &&
                SameStock(stock, empty), "Rules: the final cancellation releases the empty stock type");

        Require(ResourceStorageRulesDomain.TryReserve(int.MaxValue, empty, int.MaxValue, ResourceTypeDomain.Wood, out stock) &&
                ResourceStorageRulesDomain.GetAvailableCapacity(int.MaxValue, stock) == 0 &&
                !ResourceStorageRulesDomain.TryReserve(int.MaxValue, stock, 1, ResourceTypeDomain.Wood, out failed) &&
                ResourceStorageRulesDomain.TryCommit(int.MaxValue, stock, int.MaxValue, out stock) &&
                stock.Amount == int.MaxValue && stock.Reserved == 0,
            "Rules: the largest valid capacity cannot overflow");
        Require(ResourceStorageRulesDomain.TryExport(stock, int.MaxValue, out stock, out quantity, out type) &&
                quantity == int.MaxValue && SameStock(stock, empty),
            "Rules: exporting the largest amount clears stock exactly");
        foreach (var invalid in new[]
                 {
                     new ResourceStorageRulesDomain.Stock(ResourceTypeDomain.None, 1, 0),
                     new ResourceStorageRulesDomain.Stock(ResourceTypeDomain.Stone, -1, 0),
                     new ResourceStorageRulesDomain.Stock(ResourceTypeDomain.Stone, 0, -1),
                     new ResourceStorageRulesDomain.Stock(ResourceTypeDomain.Stone, int.MaxValue, int.MaxValue)
                 })
            Require(ResourceStorageRulesDomain.GetAvailableCapacity(int.MaxValue, invalid) == 0 &&
                    !ResourceStorageRulesDomain.TryReserve(int.MaxValue, invalid, 1, ResourceTypeDomain.Stone, out failed) &&
                    SameStock(failed, invalid), "Rules: invalid or overfull stock cannot reserve capacity");
    }

    private static bool SameStock(ResourceStorageRulesDomain.Stock first, ResourceStorageRulesDomain.Stock second)
        => first.Type == second.Type && first.Amount == second.Amount && first.Reserved == second.Reserved;

    private static void VerifyReservationTickets()
    {
        var storage = CreateCell<StorageCellAdapter>();
        var crafter = CreateCell<CrafterCellAdapter>();
        var conveyor = CreateCell<ConveyorCellAdapter>();
        var crossing = CreateCell<CrossingCellAdapter>();
        try
        {
            SetPrivateField(storage, "capacity", 5);
            ResourceReservationApplication second = null;
            Require(ResourceReservationApplication.TryCreate(storage, Vector3Int.right, 2, ResourceTypeDomain.Stone,
                    out var first) &&
                    ResourceReservationApplication.TryCreate(storage, Vector3Int.right, 2, ResourceTypeDomain.Stone,
                    out second) && first.Id != second.Id,
                "Ticket: equal reservations have different IDs");
            Require(first.TryCancel() && !first.TryCancel() && !first.TryCommit() &&
                    storage.AllocatedAmount == 2,
                "Ticket: cancellation releases only its own amount once");
            Require(second.TryCommit() && !second.TryCommit() && !second.TryCancel() &&
                    storage.AllocatedAmount == 0 && storage.CurrentLoad == 2,
                "Ticket: commit moves the reserved amount once");

            SetPrivateField(crafter, "ingredientCapacity", 5);
            var inputs = GetPrivateField<Dictionary<Vector3Int, CrafterCellAdapter.ResourceInputData>>(
                crafter, "_resourceInputs");
            inputs[Vector3Int.right] = new CrafterCellAdapter.ResourceInputData();
            Require(ResourceReservationApplication.TryCreate(crafter, Vector3Int.right, 3, ResourceTypeDomain.Wood,
                    out var ingredient) && ingredient.TryCancel() && !ingredient.TryCancel() &&
                    inputs[Vector3Int.right].Allocated == 0 &&
                    inputs[Vector3Int.right].Type == ResourceTypeDomain.None,
                "Ticket: crafter cancellation restores the input state");

            Require(ResourceReservationApplication.TryCreate(conveyor, Vector3Int.right, 2, ResourceTypeDomain.Stone,
                    out var conveyorInput) && conveyorInput.TryCancel() &&
                    !conveyorInput.TryCancel() &&
                    ResourceReservationApplication.TryCreate(conveyor, Vector3Int.right, 1, ResourceTypeDomain.Wood,
                        out var replacement) && replacement.TryCancel(),
                "Ticket: conveyor input can be canceled and reserved again");

            var adjacent = GetPrivateField<Dictionary<Vector3Int, IContainableApplication>>(
                crossing, "_adjacentContainers");
            adjacent[Vector3Int.right] = storage;
            Require(ResourceReservationApplication.TryCreate(crossing, Vector3Int.right, 2, ResourceTypeDomain.Stone,
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
        var source = CreateCell<StorageCellAdapter>();
        var target = CreateCell<StorageCellAdapter>();
        try
        {
            var completed = new ResourceTransferOperationApplication(source, target, 12, ResourceTypeDomain.Stone, 4);
            Require(completed.Source == source && completed.Target == target &&
                    completed.ResourceId == 12 && completed.Type == ResourceTypeDomain.Stone && completed.Amount == 4,
                "Transfer: one record keeps source, target, ID, type and amount");
            Require(completed.TryMarkReserved() && !completed.TryMarkReserved() &&
                    completed.TryMarkAnimating() && completed.TryComplete() &&
                    !completed.TryComplete() && !completed.TryCancel(),
                "Transfer: completion happens once");

            var retried = new ResourceTransferOperationApplication(source, target, 0, ResourceTypeDomain.Wood, 2);
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
        var storage = CreateCell<StorageCellAdapter>();
        try
        {
            SetPrivateField(storage, "capacity", 5);

            Require(!storage.AllocateStorage(Vector3Int.right, 6, ResourceTypeDomain.Stone), "Storage: reject insufficient capacity");
            Require(storage.StoredResourceType == ResourceTypeDomain.None, "Storage: rejection leaves type unchanged");
            Require(storage.AllocateStorage(Vector3Int.right, 4, ResourceTypeDomain.Stone), "Storage: reserve four");
            Require(!storage.AllocateStorage(Vector3Int.right, 2, ResourceTypeDomain.Stone), "Storage: reject partial reservation");
            Require(!storage.AllocateStorage(Vector3Int.right, 0, ResourceTypeDomain.Stone), "Storage: reject zero amount");
            Require(!storage.AllocateStorage(Vector3Int.right, 1, ResourceTypeDomain.None), "Storage: reject missing type");
            Require(storage.AllocatedAmount == 4 && storage.CurrentLoad == 0, "Storage: failed requests leave state intact");

            storage.StoreResource(Vector3Int.right, 5);
            Require(storage.AllocatedAmount == 4 && storage.CurrentLoad == 0, "Storage: reject unreserved amount");
            storage.StoreResource(Vector3Int.right, 4);
            Require(storage.AllocatedAmount == 0 && storage.CurrentLoad == 4, "Storage: commit exact reservation");

            Require(storage.AllocateStorage(Vector3Int.right, 1, ResourceTypeDomain.Stone), "Storage: reserve remaining space");
            storage.CancelStorage(Vector3Int.right, 1, ResourceTypeDomain.Stone);
            Require(storage.AllocatedAmount == 0 && storage.CurrentLoad == 4,
                "Storage: cancellation preserves committed stock");
            Require(storage.AllocateStorage(Vector3Int.right, 1, ResourceTypeDomain.Stone),
                "Storage: canceled capacity can be reserved again");
            Require(storage.TryExport(Vector3.zero, 4, out var exported, out _) && exported == 4,
                "Storage: export existing contents");
            Require(storage.StoredResourceType == ResourceTypeDomain.Stone, "Storage: retain type while delivery is reserved");
            Require(!storage.AllocateStorage(Vector3Int.right, 1, ResourceTypeDomain.Wood), "Storage: reject another type in flight");
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
        var crafter = CreateCell<CrafterCellAdapter>();
        try
        {
            SetPrivateField(crafter, "ingredientCapacity", 5);
            var inputs = GetPrivateField<Dictionary<Vector3Int, CrafterCellAdapter.ResourceInputData>>(crafter, "_resourceInputs");
            inputs[Vector3Int.right] = new CrafterCellAdapter.ResourceInputData();

            Require(!crafter.AllocateStorage(Vector3Int.right, 6, ResourceTypeDomain.Stone),
                "Crafter: reject insufficient capacity");
            Require(inputs[Vector3Int.right].Type == ResourceTypeDomain.None,
                "Crafter: rejection leaves type unchanged");
            Require(crafter.AllocateStorage(Vector3Int.right, 4, ResourceTypeDomain.Stone), "Crafter: reserve four");
            Require(!crafter.AllocateStorage(Vector3Int.right, 2, ResourceTypeDomain.Stone), "Crafter: reject partial reservation");
            Require(!crafter.AllocateStorage(Vector3Int.left, 1, ResourceTypeDomain.Stone), "Crafter: reject absent input");
            Require(!crafter.AllocateStorage(Vector3Int.right, 1, ResourceTypeDomain.Wood), "Crafter: reject another type");
            Require(inputs[Vector3Int.right].Allocated == 4, "Crafter: failed requests leave reservation intact");

            crafter.StoreResource(Vector3Int.right, 5);
            Require(inputs[Vector3Int.right].Amount == 0 && inputs[Vector3Int.right].Allocated == 4,
                "Crafter: reject unreserved amount");
            crafter.StoreResource(Vector3Int.right, 4);
            Require(inputs[Vector3Int.right].Amount == 4 && inputs[Vector3Int.right].Allocated == 0,
                "Crafter: commit exact reservation");
            Require(crafter.AllocateStorage(Vector3Int.right, 1, ResourceTypeDomain.Stone), "Crafter: reserve final space");
            crafter.CancelStorage(Vector3Int.right, 1, ResourceTypeDomain.Stone);
            Require(inputs[Vector3Int.right].Allocated == 0 && inputs[Vector3Int.right].Amount == 4,
                "Crafter: cancellation preserves committed ingredients");
            Require(crafter.AllocateStorage(Vector3Int.right, 1, ResourceTypeDomain.Stone),
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
        var conveyor = CreateCell<ConveyorCellAdapter>();
        try
        {
            Require(!conveyor.AllocateStorage(Vector3Int.right, 0, ResourceTypeDomain.Stone),
                "Conveyor: reject zero amount");
            Require(!conveyor.AllocateStorage(Vector3Int.right, 1, ResourceTypeDomain.None),
                "Conveyor: reject missing type");
            Require(conveyor.AllocateStorage(Vector3Int.right, 5, ResourceTypeDomain.Stone),
                "Conveyor: accept one full batch");
            Require(!conveyor.AllocateStorage(Vector3Int.right, 1, ResourceTypeDomain.Stone),
                "Conveyor: reject a second batch");
            conveyor.CancelStorage(Vector3Int.right, 5, ResourceTypeDomain.Stone);
            Require(conveyor.AllocateStorage(Vector3Int.right, 1, ResourceTypeDomain.Wood),
                "Conveyor: cancellation frees the input slot");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(conveyor.gameObject);
        }
    }

    private static void VerifyCrossing()
    {
        var storage = CreateCell<StorageCellAdapter>();
        var crossing = CreateCell<CrossingCellAdapter>();
        try
        {
            SetPrivateField(storage, "capacity", 5);
            var adjacent = GetPrivateField<Dictionary<Vector3Int, IContainableApplication>>(
                crossing, "_adjacentContainers");
            adjacent[Vector3Int.right] = storage;

            Require(crossing.AllocateStorage(Vector3Int.right, 4, ResourceTypeDomain.Stone),
                "Crossing: forward full reservation");
            Require(!crossing.AllocateStorage(Vector3Int.right, 2, ResourceTypeDomain.Stone),
                "Crossing: forward partial rejection");
            Require(!crossing.AllocateStorage(Vector3Int.left, 1, ResourceTypeDomain.Stone),
                "Crossing: reject absent output");
            Require(storage.AllocatedAmount == 4, "Crossing: target reservation remains exact");
            crossing.CancelStorage(Vector3Int.right, 4, ResourceTypeDomain.Stone);
            Require(storage.AllocatedAmount == 0 && storage.StoredResourceType == ResourceTypeDomain.None,
                "Crossing: cancellation reaches target and releases type");
            Require(crossing.AllocateStorage(Vector3Int.right, 5, ResourceTypeDomain.Wood),
                "Crossing: canceled path accepts a different resource");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(crossing.gameObject);
            UnityEngine.Object.DestroyImmediate(storage.gameObject);
        }
    }

    private static T CreateCell<T>() where T : CellBaseAdapter
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

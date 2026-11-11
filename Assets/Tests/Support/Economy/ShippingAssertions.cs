#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>新規出荷資産と吸収の境界を確認し、既存の換金・搬送・初期配置検証へ進む。</summary>
public static class ShippingAssertions
{
    private static int _checks;
    public static void VerifyAssetsAndAbsorption()
    {
        _checks = 0;
        var cell = AssetDatabase.LoadAssetAtPath<ShippingCellAdapter>("Assets/Prefabs/Field/General/F_ShippingCell.prefab");
        var placeholder = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Placeholder/General/P_ShippingCell.prefab");
        Require(cell != null && cell.Footprint.Area == 1 && cell.Ports.Count == 4 && cell.Ports.All(p => p.Use == CellPortUseDomain.Input), "one tile, four input ports");
        Require(PrefabUtility.GetPrefabAssetType(cell) == PrefabAssetType.Variant &&
            PrefabUtility.GetPrefabAssetType(placeholder) == PrefabAssetType.Variant, "saved variants");
        Require(cell.GetComponent<EmptyCellAdapter>() == null && cell.transform.GetChild(0).Find("ShippingCellModel") != null &&
            placeholder.transform.GetChild(0).Find("ShippingCellModel") != null, "shared independent model and shipping adapter");
        var db = AssetDatabase.LoadAssetAtPath<CellDatabaseDefinition>("Assets/Scripts/ScriptableObject/GeneralCellDB.asset");
        Require(db.TryGetCellInfo(CellTypeDomain.Shipping, out var info) && info.FieldCellPrefab == cell && info.PlaceholderCellPrefab == placeholder, "catalog references");
        var layout = AssetDatabase.LoadAssetAtPath<SingularityStartDefinition>("Assets/Scripts/ScriptableObject/SingularityStart.asset");
        layout.Validate(14); layout.Validate(100);
        Require(layout.ShippingPrefab == cell && layout.CreateCatalog().TryGetPlacementAmount(CellTypeDomain.Shipping, 1, out var price) && price == 30, "initial shipping and price");
        var absorption = new AbsorptionIntakeApplication(new[] { 0, 1 }, new DeliveryProgressApplication(layout.CreateDeliveryCatalog())) { Accepting = true };
        var count = 0;
        absorption.Absorbed += (type, amount) => { count += amount; absorption.Commit(0, amount); };
        Require(absorption.TryReserve(0, 3, ResourceTypeDomain.Iron) && !absorption.TryReserve(0, 1, ResourceTypeDomain.Wood), "absorption accepts defined unsellable resource and reserves once");
        Require(!absorption.Commit(0, 2) && !absorption.Cancel(0, 3, ResourceTypeDomain.Wood), "mismatch retains absorption reservation");
        Require(absorption.Commit(0, 3) && !absorption.Commit(0, 3) && count == 3, "absorption notification and reentry are one-shot");
        foreach (var amount in new[] { int.MinValue, -1, 0 }) Require(!absorption.TryReserve(1, amount, ResourceTypeDomain.Wood), "invalid absorption amount");
        Require(!absorption.TryReserve(1, 1, ResourceTypeDomain.None) && !absorption.TryReserve(1, 1, (ResourceTypeDomain)999), "invalid absorption resource");
        Require(absorption.TryReserve(1, 2, ResourceTypeDomain.Wood), "absorption cancellation reserve");
        absorption.CancelAll(); Require(!absorption.Commit(1, 2) && count == 3, "cancel all never absorbs");
        absorption.Absorbed += (_, _) => throw new InvalidOperationException("expected receipt observer failure");
        var notified = false; absorption.Absorbed += (_, _) => notified = true;
        Require(absorption.TryReserve(1, 1, ResourceTypeDomain.Wood) && absorption.Commit(1, 1), "receipt commits before observer notifications");
        Require(absorption.LastNotificationException != null && notified && !absorption.Commit(1, 1), "observer failure does not duplicate absorption or skip later observers");

        Debug.Log($"Shipping EDIT checks passed: {_checks} assertions; assets and absorption boundaries.");

    }
    private static void Require(bool condition, string message)
    { if (!condition) throw new InvalidOperationException("Shipping check failed: " + message); _checks++; }

}

#endif

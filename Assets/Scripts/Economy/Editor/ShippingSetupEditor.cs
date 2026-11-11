using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

/// <summary>既存のVariant作成経路で出荷資産を作り、定義へ接続する。Sceneは保存しない。</summary>
public static class ShippingSetupEditor
{
    public const string FieldPath = "Assets/Prefabs/Field/General/F_ShippingCell.prefab";
    public const string PlaceholderPath = "Assets/Prefabs/Placeholder/General/P_ShippingCell.prefab";
    private const string ModelPath = "Assets/Prefabs/Models/ShippingCellModel.prefab";
    public static void Run()
    {
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
        if (model == null)
        {
            var root = new GameObject("ShippingCellModel");
            try
            {
                var dark = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Cell/ConveyorMat.mat");
                var shell = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Cell/StorageMat.mat");
                Part(root.transform, "Intake", new Vector3(0, .56f, 0), new Vector3(.7f, .1f, .7f), dark);
                // 四方向の開口を残した受入口。保管庫の箱と輪郭を分ける。
                for (var i = 0; i < 4; i++)
                {
                    var rotation = Quaternion.Euler(0, i * 90, 0);
                    Part(root.transform, "Frame" + i, rotation * new Vector3(.36f, .66f, .36f), new Vector3(.14f, .32f, .14f), shell);
                }
                model = PrefabUtility.SaveAsPrefabAsset(root, ModelPath);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }
        CreateVariant(model, "Assets/Prefabs/Field/General/F_EmptyCell.prefab", FieldPath, "F_");
        CreateVariant(model, "Assets/Prefabs/Placeholder/General/P_EmptyCell.prefab", PlaceholderPath, "P_");
        var contents = PrefabUtility.LoadPrefabContents(FieldPath);
        try
        {
            var empty = contents.GetComponent<EmptyCellAdapter>();
            if (empty != null) UnityEngine.Object.DestroyImmediate(empty);
            var cell = contents.GetComponent<ShippingCellAdapter>() ?? contents.AddComponent<ShippingCellAdapter>();
            var serialized = new SerializedObject(cell);
            var ports = serialized.FindProperty("ports");
            for (var i = 0; i < ports.arraySize; i++) ports.GetArrayElementAtIndex(i).FindPropertyRelative("Use").intValue = (int)CellPortUseDomain.Input;
            serialized.ApplyModifiedPropertiesWithoutUndo(); cell.ValidatePorts();
            PrefabUtility.SaveAsPrefabAsset(contents, FieldPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(contents); }
        var field = AssetDatabase.LoadAssetAtPath<ShippingCellAdapter>(FieldPath);
        var placeholder = AssetDatabase.LoadAssetAtPath<GameObject>(PlaceholderPath);
        var database = AssetDatabase.LoadAssetAtPath<CellDatabaseDefinition>("Assets/Scripts/ScriptableObject/GeneralCellDB.asset");
        var infos = database.GetAllCellInfos().Where(i => i.CellTypeDomain != CellTypeDomain.Shipping).ToList();
        infos.Add(new CellInfoDefinition { CellName = "出荷", CellTypeDomain = CellTypeDomain.Shipping, FieldCellPrefab = field, PlaceholderCellPrefab = placeholder });
        database.SetCellInfos(infos.ToArray()); EditorUtility.SetDirty(database);
        var economy = new SerializedObject(AssetDatabase.LoadAssetAtPath<CurrencyEconomyDefinition>("Assets/Scripts/ScriptableObject/CurrencyEconomy.asset"));
        var prices = economy.FindProperty("placementPrices");
        var index = Enumerable.Range(0, prices.arraySize).Where(i => prices.GetArrayElementAtIndex(i).FindPropertyRelative("cell").intValue == (int)CellTypeDomain.Shipping).DefaultIfEmpty(-1).First();
        if (index < 0) { index = prices.arraySize; prices.arraySize++; }
        var price = prices.GetArrayElementAtIndex(index); price.FindPropertyRelative("cell").intValue = (int)CellTypeDomain.Shipping;
        price.FindPropertyRelative("price").longValue = 30; economy.ApplyModifiedPropertiesWithoutUndo();
        var layout = new SerializedObject(AssetDatabase.LoadAssetAtPath<SingularityStartDefinition>("Assets/Scripts/ScriptableObject/SingularityStart.asset"));
        layout.FindProperty("shipping").objectReferenceValue = field; layout.ApplyModifiedPropertiesWithoutUndo();
        AssetDatabase.SaveAssets(); Debug.Log("Shipping assets saved: model, field/placeholder variants, catalog, price and initial layout.");
    }
    private static void CreateVariant(GameObject model, string basePath, string target, string prefix)
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(target) != null) return;
        typeof(GenerateVariantWindowEditor).GetMethod("CreateNewVariant", BindingFlags.Static | BindingFlags.NonPublic)
            .Invoke(null, new object[] { model, AssetDatabase.LoadAssetAtPath<GameObject>(basePath), System.IO.Path.GetDirectoryName(target).Replace('\\', '/'), prefix });
        if (AssetDatabase.LoadAssetAtPath<GameObject>(target) == null) throw new InvalidOperationException("Shipping variant was not created: " + target);
    }
    private static void Part(Transform parent, string name, Vector3 position, Vector3 scale, Material material)
    {
        var part = GameObject.CreatePrimitive(PrimitiveType.Cube); UnityEngine.Object.DestroyImmediate(part.GetComponent<Collider>());
        part.name = name; part.transform.SetParent(parent, false); part.transform.localPosition = position;
        part.transform.localScale = scale; part.GetComponent<Renderer>().sharedMaterial = material;
    }
}

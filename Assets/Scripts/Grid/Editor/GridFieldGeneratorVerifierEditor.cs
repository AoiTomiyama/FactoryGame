using System;
using System.IO;
using System.Reflection;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>Editor のフィールド生成とプレイヤービルドを別々に検証する。</summary>
public static class GridFieldGeneratorVerifierEditor
{
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

    /// <summary>保存フラグを信じたコールド起動と、定義更新で古いキャッシュが残る経路を再現する。</summary>
    public static void RunCellDefinitions()
    {
        var saved = AssetDatabase.LoadAssetAtPath<CellDatabaseDefinition>("Assets/Scripts/ScriptableObject/GeneralCellDB.asset");
        var expected = (CellInfoDefinition[])typeof(CellDatabaseDefinition).GetField("cellPairingInfos", PrivateInstance).GetValue(saved);
        Require(expected.Length > 2, "saved cell definitions exist");
        var database = UnityEngine.Object.Instantiate(saved);
        try
        {
            // OnValidateを呼ばず、旧保存値true＋空の実行時辞書を直接作る。
            var lookup = (Dictionary<CellTypeDomain, CellInfoDefinition>)typeof(CellDatabaseDefinition)
                .GetField("_infoLookup", PrivateInstance).GetValue(database);
            lookup.Clear();
            SetField(database, "isInitialized", true);
            Invoke(database, "OnEnable");
            Require(database.GetAllCellInfos().Count == expected.Length, "cold enable rebuilds despite legacy true flag");
            foreach (var info in expected)
                Require(database.TryGetCellInfo(info.CellTypeDomain, out var actual) && actual.Equals(info), "saved names/types/prefab references survive");

            var updated = expected[0];
            updated.CellName = "Updated definition";
            database.SetCellInfos(new[] { updated });
            Require(database.GetAllCellInfos().Count == 1 && database.TryGetCellInfo(updated.CellTypeDomain, out var changed)
                && changed.CellName == updated.CellName, "replacement updates entries");
            Require(!database.TryGetCellInfo(expected[1].CellTypeDomain, out _), "replacement removes stale entries");
            database.SetCellInfos(new[] { updated, expected[1] });
            Require(database.TryGetCellInfo(expected[1].CellTypeDomain, out _), "replacement adds entries without manual validation");
            var snapshot = database.GetAllCellInfos();
            snapshot.Clear();
            Require(database.GetAllCellInfos().Count == 2, "returned list cannot mutate lookup");

            SetField(database, "cellPairingInfos", new[] { expected[2] });
            Invoke(database, "OnValidate");
            Require(database.GetAllCellInfos().Single().Equals(expected[2]), "inspector changes invalidate lookup");
            lookup.Clear();
            SetField(database, "isInitialized", true);
            Invoke(database, "OnEnable");
            Require(database.GetAllCellInfos().Single().Equals(expected[2]), "reenable rebuilds lookup");

            var duplicate = expected[0];
            duplicate.CellName = "Last duplicate wins";
            database.SetCellInfos(new[] { expected[0], duplicate });
            Require(database.GetAllCellInfos().Single().CellName == duplicate.CellName, "duplicate keeps last entry");
            database.SetCellInfos(Array.Empty<CellInfoDefinition>());
            Require(database.GetAllCellInfos().Count == 0 && !database.TryGetCellInfo(expected[0].CellTypeDomain, out _), "empty definition removes entries");
            SetField(database, "cellPairingInfos", null);
            Invoke(database, "OnEnable");
            Require(database.GetAllCellInfos().Count == 0, "null definition is empty");
            var fresh = ScriptableObject.CreateInstance<CellDatabaseDefinition>();
            try { Require(fresh.GetAllCellInfos().Count == 0, "new unconfigured definition is empty"); }
            finally { UnityEngine.Object.DestroyImmediate(fresh); }
            Require(!EditorJsonUtility.ToJson(database).Contains("isInitialized"), "runtime flag is not serialized");
            Debug.Log("Cell definition checks passed: legacy cold enable, references, update/add/remove, snapshot, inspector, reenable, duplicate, empty/null.");
        }
        finally { UnityEngine.Object.DestroyImmediate(database); }
    }

    private static void Invoke(object target, string method) => target.GetType().GetMethod(method, PrivateInstance).Invoke(target, null);
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("Cell definition check failed: " + message);
    }

    [MenuItem("Tools/FactoryGame/Verify Grid Generation")]
    public static void RunGeneration()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Prefabs/Field/General/F_EmptyCell.prefab");
        if (prefab == null) throw new InvalidOperationException("Empty cell prefab is missing.");

        var generatorObject = new GameObject("GridGenerationCheck:Generator");
        var anchor = new GameObject("GridGenerationCheck:Anchor").transform;
        try
        {
            var generator = generatorObject.AddComponent<GridFieldGeneratorAdapter>();
            SetField(generator, "emptyCellPrefab", prefab);
            SetField(generator, "gridSize", 2);
            var propField = typeof(GridFieldGeneratorAdapter).GetField("propPrefabs", PrivateInstance);
            if (propField == null) throw new InvalidOperationException("Prop prefab field is missing.");
            propField.SetValue(generator, Array.CreateInstance(propField.FieldType.GetElementType(), 0));

            generator.GenerateGrid(anchor);
            if (anchor.childCount != 2) throw new InvalidOperationException("Expected two grid rows.");
            for (var x = 0; x < 2; x++)
            {
                var row = anchor.GetChild(x);
                if (row.childCount != 2) throw new InvalidOperationException("Expected two cells per row.");
                for (var z = 0; z < 2; z++)
                {
                    var cell = row.GetChild(z);
                    if (cell.name != $"Tile_{x}_{z}" || PrefabUtility.GetCorrespondingObjectFromSource(cell) == null)
                        throw new InvalidOperationException("Generated cell lost its name or prefab link.");
                }
            }

            generator.GenerateGridLine(anchor);
            var lines = anchor.Find("GridLines")?.GetComponent<LineRenderer>();
            if (lines == null || lines.positionCount != 18)
                throw new InvalidOperationException("Grid lines were not generated.");

            GridFieldGeneratorAdapter.ClearGrid(anchor);
            if (anchor.childCount != 0) throw new InvalidOperationException("Grid clear left children.");
            Debug.Log("Grid generation checks passed: prefab cells, grid lines, clear.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(anchor.gameObject);
            UnityEngine.Object.DestroyImmediate(generatorObject);
        }
    }

    public static void RunPlayerBuild()
        => BuildPlayer(BuildOptions.None);

    // CLIの実プレイヤー検証だけをDevelopmentビルドへ含め、通常ビルドの動作は変えない。
    public static void RunCellDefinitionBuild()
    {
        RunCellDefinitions();
        RunGeneration();
        UIStatusVerifierEditor.VerifyAssets();
        BuildPlayer(BuildOptions.Development);
    }

    private static void BuildPlayer(BuildOptions options)
    {
        var output = Path.Combine(Path.GetTempPath(), "FactoryGameTask3Build", "FactoryGame.exe");
        Directory.CreateDirectory(Path.GetDirectoryName(output));
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = new[] { "Assets/Scenes/MainScene.unity" },
            locationPathName = output,
            target = BuildTarget.StandaloneWindows64,
            options = options
        });
        if (report.summary.result != BuildResult.Succeeded)
            throw new InvalidOperationException($"Player build failed: {report.summary.result}");
        Debug.Log("Grid generator player build passed.");
    }

    private static void SetField(object target, string name, object value)
    {
        var field = target.GetType().GetField(name, PrivateInstance);
        if (field == null) throw new InvalidOperationException($"Missing field: {name}");
        field.SetValue(target, value);
    }
}

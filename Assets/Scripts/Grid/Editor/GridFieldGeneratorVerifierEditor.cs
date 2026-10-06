using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>Editor のフィールド生成とプレイヤービルドを別々に検証する。</summary>
public static class GridFieldGeneratorVerifierEditor
{
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

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
    {
        var output = Path.Combine(Path.GetTempPath(), "FactoryGameTask3Build", "FactoryGame.exe");
        Directory.CreateDirectory(Path.GetDirectoryName(output));
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = new[] { "Assets/Scenes/MainScene.unity" },
            locationPathName = output,
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.None
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

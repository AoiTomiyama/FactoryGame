using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(GridFieldGenerator))]
public class GridGeneratorEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        var generator = (GridFieldGenerator)target;

        if (GUILayout.Button("Generate Grid")) GenerateGrid(generator);

        if (GUILayout.Button("Clear Grid")) ClearGrid(generator);
    }

    /// <summary>
    /// グリット情報のクリア
    /// </summary>
    /// <param name="generator"></param>
    private static void ClearGrid(GridFieldGenerator generator)
    {
        if (generator.Grid == null) return;
        for (int i = generator.transform.childCount - 1; i >= 0 ; i--)
        {
            var target = generator.transform.GetChild(i).gameObject;
            DestroyImmediate(target);
        }

        generator.Grid = null;
    }

    /// <summary>
    /// シーン上にグリッドを生成
    /// </summary>
    /// <param name="generator"></param>
    private void GenerateGrid(GridFieldGenerator generator)
    {
        if (generator.cellPrefab == null)
        {
            Debug.LogError("Tile Prefab is not assigned.");
            return;
        }

        if (generator.Grid != null)
        {
            ClearGrid(generator);
        }

        generator.Grid = new GameObject[generator.gridSize, generator.gridSize];

        for (int x = 0; x < generator.gridSize; x++)
        {
            var separator = new GameObject($"Separator_{x}");
            separator.transform.SetParent(generator.transform);
            for (int z = 0; z < generator.gridSize; z++)
            {
                var cellScale = generator.cellPrefab.transform.localScale;
                var pos = new Vector3(x * cellScale.x, 0,
                                      z * cellScale.z);
                
                var tile = (GameObject)PrefabUtility.InstantiatePrefab(generator.cellPrefab, separator.transform);
                generator.Grid[x, z] = tile;
                tile.transform.position = pos;
                tile.name = $"Tile_{x}_{z}";
            }
        }
    }
}
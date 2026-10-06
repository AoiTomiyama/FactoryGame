using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(GridFieldGeneratorAdapter))]
public class GridGeneratorEditor : Editor
{
    private Transform _anchorTransform;

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        var fieldGenerator = (GridFieldGeneratorAdapter)target;

        if (GUILayout.Button("Generate Grid"))
        {
            Generate(fieldGenerator);
        }

        if (GUILayout.Button("Clear Grid"))
        {
            Clear();
        }

        if (GUILayout.Button("Generate Seed Value"))
        {
            fieldGenerator.GenerateRandomSeedValue();
        }
    }

    private void Clear()
    {
        if (_anchorTransform == null)
        {
            // 割り当てがnullかつ、Databaseがある場合は破棄
            var database = FindAnyObjectByType<GridFieldDatabaseAdapter>();
            if (database != null)
            {
                _anchorTransform = database.transform;
            }
        }

        GridFieldGeneratorAdapter.ClearGrid(_anchorTransform);
        DestroyImmediate(_anchorTransform.gameObject);
    }

    private void Generate(GridFieldGeneratorAdapter fieldGenerator)
    {
        if (_anchorTransform == null)
        {
            // Databaseを元に検索して、なければ新規に作成
            var database = FindAnyObjectByType<GridFieldDatabaseAdapter>();
            if (database != null)
            {
                _anchorTransform = database.transform;
            }
            else
            {
                const string anchorName = "===== [Field] =====";

                _anchorTransform = new GameObject(anchorName).transform;

                _anchorTransform.SetParent(fieldGenerator.transform.parent);
                _anchorTransform.AddComponent<GridFieldDatabaseAdapter>();
            }
        }

        fieldGenerator.GenerateGrid(_anchorTransform);
        fieldGenerator.GenerateGridLine(_anchorTransform);
    }
}
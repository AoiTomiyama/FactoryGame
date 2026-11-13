using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[CustomEditor(typeof(GridFieldGeneratorAdapter))]
public class GridGeneratorEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        var generator = (GridFieldGeneratorAdapter)target;
        var field = generator.TargetField;
        if (field == null) EditorGUILayout.HelpBox("生成先のGridを割り当ててください。", MessageType.Info);
        using (new EditorGUI.DisabledScope(field == null || Application.isPlaying))
        {
            if (GUILayout.Button("Generate Grid"))
            {
                generator.GenerateGrid(field.transform);
                generator.GenerateGridLine(field.transform);
                EditorSceneManager.MarkSceneDirty(field.gameObject.scene);
            }
            if (GUILayout.Button("Clear Grid"))
            {
                // 起動側が保持する参照を維持し、生成物だけを消す。
                GridFieldGeneratorAdapter.ClearGrid(field.transform);
                EditorSceneManager.MarkSceneDirty(field.gameObject.scene);
            }
        }
        if (GUILayout.Button("Generate Seed Value"))
        {
            generator.GenerateRandomSeedValue();
            EditorUtility.SetDirty(generator);
        }
    }
}

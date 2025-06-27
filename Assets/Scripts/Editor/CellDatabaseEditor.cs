using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(CellDatabaseSO))]
public class CellDatabaseEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        var cellDatabase = (CellDatabaseSO)target;

        if (GUILayout.Button("Validate Cell Info"))
        {
            cellDatabase.ValidateAndBuildLookup();
        }
        if (GUILayout.Button("Auto Assign"))
        {
            cellDatabase.AutoAssignData();
        }
    }
}

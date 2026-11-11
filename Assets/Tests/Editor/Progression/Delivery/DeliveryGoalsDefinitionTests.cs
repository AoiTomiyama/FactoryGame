using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

[TestFixture, Category("Delivery")]
public sealed class DeliveryGoalsDefinitionTests
{
    [Test, Description("目標の編集・Undo／Redo・保存と再読込を確認し、原本は変更しない")]
    public void EditedDefinitionSupportsUndoRedoAndSavedReload()
    {
        const string source = "Assets/Scripts/ScriptableObject/DeliveryGoals.asset";
        var definition = AssetDatabase.LoadAssetAtPath<DeliveryGoalsDefinition>(source);
        Assert.That(definition, Is.Not.Null); var before = File.ReadAllBytes(source);
        var path = AssetDatabase.GenerateUniqueAssetPath("Assets/DeliveryGoalsVerification.asset");
        var editable = Object.Instantiate(definition); AssetDatabase.CreateAsset(editable, path);
        try
        {
            var serialized = new SerializedObject(editable); var stage = serialized.FindProperty("stages").GetArrayElementAtIndex(0);
            var original = editable.CreateCatalog().Stages[0];
            Undo.RecordObject(editable, "Delivery goal verification"); stage.FindPropertyRelative("label").stringValue = "編集確認";
            stage.FindPropertyRelative("requirements").GetArrayElementAtIndex(0).FindPropertyRelative("cumulativeAmount").longValue = 123;
            serialized.ApplyModifiedProperties(); Undo.FlushUndoRecordObjects();
            Assert.That(editable.CreateCatalog().Stages[0].Label, Is.EqualTo("編集確認"));
            Assert.That(editable.CreateCatalog().Stages[0].Requirements[0].Amount, Is.EqualTo(123));
            Undo.PerformUndo(); Assert.That(editable.CreateCatalog().Stages[0].Label, Is.EqualTo(original.Label));
            Assert.That(editable.CreateCatalog().Stages[0].Requirements[0].Amount, Is.EqualTo(original.Requirements[0].Amount));
            Undo.PerformRedo(); Assert.That(editable.CreateCatalog().Stages[0].Label, Is.EqualTo("編集確認"));
            AssetDatabase.SaveAssetIfDirty(editable); AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var reread = AssetDatabase.LoadAssetAtPath<DeliveryGoalsDefinition>(path).CreateCatalog();
            Assert.That(reread.Stages[0].Label, Is.EqualTo("編集確認")); Assert.That(reread.Stages[0].Requirements[0].Amount, Is.EqualTo(123));
        }
        finally { Undo.ClearUndo(editable); AssetDatabase.DeleteAsset(path); }
        Assert.That(File.ReadAllBytes(source).SequenceEqual(before), Is.True);
    }
}

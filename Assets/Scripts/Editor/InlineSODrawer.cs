using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(InlineSOAttribute))]
public class InlineSODrawer : PropertyDrawer
{
    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        if (property.objectReferenceValue == null) return EditorGUIUtility.singleLineHeight;

        var so = new SerializedObject(property.objectReferenceValue);
        var height = EditorGUIUtility.singleLineHeight;

        so.Update();
        var p = so.GetIterator();
        if (p.NextVisible(true)) // skip "m_Script"
        {
            while (p.NextVisible(false))
            {
                height += EditorGUI.GetPropertyHeight(p, true) + EditorGUIUtility.standardVerticalSpacing;
            }
        }

        return height;
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.ObjectField(new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight),
            property, label);

        if (property.objectReferenceValue == null) return;

        var so = new SerializedObject(property.objectReferenceValue);
        so.Update();

        var p = so.GetIterator();
        var y = position.y + EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;

        if (p.NextVisible(true)) // skip "m_Script"
        {
            while (p.NextVisible(false))
            {
                var h = EditorGUI.GetPropertyHeight(p, true);
                var r = new Rect(position.x, y, position.width, h);
                EditorGUI.PropertyField(r, p, true);
                y += h + EditorGUIUtility.standardVerticalSpacing;
            }
        }

        so.ApplyModifiedProperties();
    }
}
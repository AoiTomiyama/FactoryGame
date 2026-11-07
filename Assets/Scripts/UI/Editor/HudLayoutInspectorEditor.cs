using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>既存の部品へ、操作した共通項目だけを一括反映する編集入口。</summary>
[CustomEditor(typeof(HudLayoutAdapter))]
public sealed class HudLayoutInspectorEditor : Editor
{
    private OperationBarAdapter _operation;
    private bool _targets;

    public override void OnInspectorGUI()
    {
        var hud = (HudLayoutAdapter)target;
        if (_operation == null) _operation = SceneOperation(hud);
        EditorGUILayout.LabelField("HUDの共通設定", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("変更した項目を固定UI・共有行Prefab・操作帯へ一括反映します。行Prefabの変更は、それを使う他のHUDにも反映されます。", MessageType.Info);
        using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
        {
            Edit("共通フォント", hud.TextRow != null ? hud.TextRow.font : null,
                value => (Font)EditorGUILayout.ObjectField("共通フォント", value, typeof(Font), false),
                value => ApplyFont(hud, _operation, value));
            var size = hud.TextRow != null ? hud.TextRow.fontSize : 16;
            Edit("基本文字サイズ", size, value => EditorGUILayout.IntField("基本文字サイズ", value),
                value => ApplyBaseSize(hud, _operation, value));
            Edit("文字色", hud.TextRow != null ? hud.TextRow.color : Color.white,
                value => EditorGUILayout.ColorField("文字色", value), value => ApplyTextColor(hud, _operation, value));
            var panel = Panels(hud, _operation).FirstOrDefault();
            Edit("パネル背景色", panel != null ? panel.color : Color.black,
                value => EditorGUILayout.ColorField("パネル背景色", value), value => ApplyPanelColor(hud, _operation, value));
            var button = hud.Upgrade != null ? hud.Upgrade.targetGraphic : null;
            Edit("ボタン通常色", button != null ? button.color : Color.white,
                value => EditorGUILayout.ColorField("ボタン通常色", value), value => ApplyButtonColor(hud, _operation, value));
            Edit("選択色", hud.SelectedTabColor, value => EditorGUILayout.ColorField("選択色", value),
                value => ApplySelectedColor(hud, _operation, value));
            EditorGUILayout.HelpBox("基本文字サイズは追加項目の本文サイズです。変更分を全Textへ加え、見出し・本文・補足の差を保ちます。細かな色・文字・配置は各部品の標準Inspectorで編集できます。", MessageType.None);
            _operation = (OperationBarAdapter)EditorGUILayout.ObjectField("対象の操作帯", _operation, typeof(OperationBarAdapter), true);
        }
        Destination(hud);
        if (_operation != null) Destination(_operation);
        else EditorGUILayout.HelpBox("操作帯は未指定です。Prefab Modeでは操作帯Prefabを指定すると一括調整に含まれます。", MessageType.Info);
        _targets = EditorGUILayout.Foldout(_targets, "共有行Prefab・個別編集の参照", true);
        if (_targets)
        {
            foreach (var row in RowRoots(hud)) EditorGUILayout.ObjectField("共有行Prefab", row, typeof(GameObject), false);
            EditorGUILayout.ObjectField("固定UI", hud.gameObject, typeof(GameObject), true);
            EditorGUILayout.HelpBox("SceneはOverrideとしてSceneを保存、Prefab Modeは開いているPrefabを保存、共有行とProjectから指定した操作帯はProjectを保存します。Apply/Revertは標準のOverridesを使います。", MessageType.None);
        }
    }

    private static void Edit<T>(string name, T value, Func<T, T> draw, Action<T> apply)
    {
        EditorGUI.BeginChangeCheck();
        var next = draw(value);
        if (EditorGUI.EndChangeCheck()) { Undo.SetCurrentGroupName("HUD：" + name); apply(next); }
    }

    internal static T Reference<T>(Object owner, string field) where T : Object
    {
        if (owner == null) return null;
        using var data = new SerializedObject(owner);
        return data.FindProperty(field)?.objectReferenceValue as T;
    }

    internal static OperationBarAdapter SceneOperation(HudLayoutAdapter hud)
        => EditorUtility.IsPersistent(hud) || PrefabStageUtility.GetPrefabStage(hud.gameObject) != null ? null
            : hud.gameObject.scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<OperationBarAdapter>(true)).FirstOrDefault();

    internal static GameObject[] RowRoots(HudLayoutAdapter hud)
    {
        var view = hud.GetComponentInChildren<CellStatusViewAdapter>(true);
        return new Component[] { hud.TextRow, hud.RecipeHeading, hud.SkillRow, hud.ResourceRow,
            Reference<CellSelectButtonUIAdapter>(hud.PlacementBuilder, "buttonPrefab"), Reference<HudStatusRowAdapter>(view, "hudRowPrefab") }
            .Where(value => value != null).Select(value => value.gameObject).Distinct().ToArray();
    }

    internal static GameObject[] Roots(HudLayoutAdapter hud, OperationBarAdapter operation)
        => RowRoots(hud).Append(hud.gameObject).Concat(operation != null ? new[] { operation.gameObject } : Array.Empty<GameObject>()).Distinct().ToArray();
    internal static Text[] Texts(HudLayoutAdapter hud, OperationBarAdapter operation)
        => Roots(hud, operation).SelectMany(root => root.GetComponentsInChildren<Text>(true)).Distinct().ToArray();
    internal static Button[] Buttons(HudLayoutAdapter hud, OperationBarAdapter operation)
        => Roots(hud, operation).SelectMany(root => root.GetComponentsInChildren<Button>(true)).Distinct().ToArray();
    internal static Image[] Panels(HudLayoutAdapter hud, OperationBarAdapter operation)
    {
        var status = hud.GetComponentInChildren<CellStatusViewAdapter>(true);
        // 用途が保存参照で特定できるパネルだけを扱い、アイコン・ゲージ・マスクを除く。
        return new Component[] { hud.Summary, hud.Body, status != null ? status.StatusScroll : null, operation != null ? operation.Bar : null }
            .Where(component => component != null).Select(component => component.GetComponent<Image>()).Where(image => image != null).Distinct().ToArray();
    }

    private static void Write(Object target, string path, Action<SerializedProperty> change)
    {
        if (target == null) return;
        using var data = new SerializedObject(target);
        data.Update(); change(data.FindProperty(path)); data.ApplyModifiedProperties();
    }
    private static void Together(Action edit)
    {
        Undo.IncrementCurrentGroup();
        var group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("HUDの共通設定を変更");
        try { edit(); Undo.FlushUndoRecordObjects(); }
        finally { Undo.CollapseUndoOperations(group); }
    }
    internal static void ApplyFont(HudLayoutAdapter hud, OperationBarAdapter operation, Font font) => Together(() =>
    {
        foreach (var text in Texts(hud, operation)) Write(text, "m_FontData.m_Font", p => p.objectReferenceValue = font);
    });
    internal static void ApplyBaseSize(HudLayoutAdapter hud, OperationBarAdapter operation, int size) => Together(() =>
    {
        var texts = Texts(hud, operation);
        if (texts.Length == 0 || hud.TextRow == null) return;
        // 最小文字を1以上に保てる範囲で同じ差分を使い、個別のサイズ差を維持する。
        var delta = Mathf.Max(size - hud.TextRow.fontSize, 1 - texts.Min(text => text.fontSize));
        foreach (var text in texts) { var next = text.fontSize + delta; Write(text, "m_FontData.m_FontSize", p => p.intValue = next); }
    });
    internal static void ApplyTextColor(HudLayoutAdapter hud, OperationBarAdapter operation, Color color) => Together(() =>
    {
        foreach (var text in Texts(hud, operation)) Write(text, "m_Color", p => p.colorValue = color);
    });
    internal static void ApplyPanelColor(HudLayoutAdapter hud, OperationBarAdapter operation, Color color) => Together(() =>
    {
        foreach (var panel in Panels(hud, operation)) Write(panel, "m_Color", p => p.colorValue = color);
    });
    internal static void ApplyButtonColor(HudLayoutAdapter hud, OperationBarAdapter operation, Color color) => Together(() =>
    {
        foreach (var button in Buttons(hud, operation)) Write(button.targetGraphic, "m_Color", p => p.colorValue = color);
        foreach (var cell in Roots(hud, operation).SelectMany(root => root.GetComponentsInChildren<CellSelectButtonUIAdapter>(true)).Distinct())
            Write(Reference<Image>(cell, "backgroundImage"), "m_Color", p => p.colorValue = color);
        Write(hud, "IdleTabColor", p => p.colorValue = color);
        Write(hud.PlacementBuilder, "defaultBackgroundColor", p => p.colorValue = color);
        Write(Reference<SubMenuUIBuilderAdapter>(hud.PlacementBuilder, "subMenuUIBuilder"), "subMenuColor", p => p.colorValue = color);
        PreviewTabs(hud);
    });
    internal static void ApplySelectedColor(HudLayoutAdapter hud, OperationBarAdapter operation, Color color) => Together(() =>
    {
        Write(hud, "SelectedTabColor", p => p.colorValue = color);
        Write(hud.PlacementBuilder, "openedSubMenuColor", p => p.colorValue = color);
        foreach (var button in Buttons(hud, operation)) Write(button, "m_Colors.m_SelectedColor", p => p.colorValue = color);
        PreviewTabs(hud);
    });
    private static void PreviewTabs(HudLayoutAdapter hud)
    {
        for (var i = 0; i < hud.Tabs.Length; i++)
        {
            var color = i == 0 ? hud.SelectedTabColor : hud.IdleTabColor;
            if (hud.Tabs[i] != null) Write(hud.Tabs[i].targetGraphic, "m_Color", p => p.colorValue = color);
        }
    }
    private static void Destination(Component value)
    {
        var stage = PrefabStageUtility.GetPrefabStage(value.gameObject);
        var destination = stage != null ? "Prefab Mode：" + stage.assetPath
            : EditorUtility.IsPersistent(value) ? "共有アセット：" + AssetDatabase.GetAssetPath(value)
            : "Scene Override：" + value.gameObject.scene.path;
        EditorGUILayout.LabelField("変更先", destination, EditorStyles.wordWrappedLabel);
    }
}

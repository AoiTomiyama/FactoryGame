using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>既存の保存先を直接編集し、描画だけでは値を書き込まないHUD編集入口。</summary>
[CustomEditor(typeof(HudLayoutAdapter))]
public sealed class HudLayoutInspectorEditor : Editor
{
    private bool _fixed = true, _rows, _bar, _references;
    private OperationBarAdapter _operation;

    public override void OnInspectorGUI()
    {
        var hud = (HudLayoutAdapter)target;
        using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
        {
            EditorGUILayout.HelpBox("変更した項目だけを反映します。複数の値が異なる項目は「—」で表示します。部品の個別編集は各参照から開けます。", MessageType.Info);
            _fixed = EditorGUILayout.Foldout(_fixed, "固定UI：選択中のHUD", true);
            if (_fixed)
            {
                Destination(hud);
                DrawStatic(hud);
            }
            _rows = EditorGUILayout.Foldout(_rows, "生成行：参照先の共有Prefab", true);
            if (_rows)
            {
                EditorGUILayout.HelpBox("ここでの変更は参照先アセットへ保存され、同じ行Prefabを使うHUDへ反映されます。SceneのOverrideにはなりません。行の内容は再生中に設定されます。", MessageType.Info);
                foreach (var row in RowRoots(hud)) DrawPart(row, true);
                if (hud.PlacementBuilder != null)
                {
                    Destination(hud.PlacementBuilder);
                    DrawProperties(hud.PlacementBuilder, "配置ボタンの表示色（このHUDのBuilder）", "defaultBackgroundColor", "openedSubMenuColor");
                    DrawProperties(Reference<SubMenuUIBuilderAdapter>(hud.PlacementBuilder, "subMenuUIBuilder"),
                        "採取サブメニューの表示色", "subMenuColor");
                }
            }
            _bar = EditorGUILayout.Foldout(_bar, "操作帯：別Prefab", true);
            if (_bar)
            {
                // Prefab Modeでは他のSceneやPrefab資産を自動選択しない。
                if (_operation == null && !EditorUtility.IsPersistent(hud) && PrefabStageUtility.GetPrefabStage(hud.gameObject) == null)
                    _operation = hud.gameObject.scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<OperationBarAdapter>(true)).FirstOrDefault();
                _operation = (OperationBarAdapter)EditorGUILayout.ObjectField("編集する操作帯", _operation, typeof(OperationBarAdapter), true);
                EditorGUILayout.HelpBox("Sceneでは同じSceneの操作帯を表示します。Prefab Modeでは操作帯Prefabを指定してください。変更先は下に表示します。", MessageType.Info);
                if (_operation != null) { Destination(_operation); DrawPart(_operation.gameObject, false); }
            }
        }
        _references = EditorGUILayout.Foldout(_references, "既存の参照・個別設定", true);
        if (_references) DrawDefaultInspector();
    }

    internal static T Reference<T>(Object owner, string field) where T : Object
    {
        if (owner == null) return null;
        using var data = new SerializedObject(owner);
        return data.FindProperty(field)?.objectReferenceValue as T;
    }

    internal static GameObject[] RowRoots(HudLayoutAdapter hud)
    {
        var view = hud.GetComponentInChildren<CellStatusViewAdapter>(true);
        return new Component[] { hud.TextRow, hud.RecipeHeading, hud.SkillRow, hud.ResourceRow,
            Reference<CellSelectButtonUIAdapter>(hud.PlacementBuilder, "buttonPrefab"), Reference<HudStatusRowAdapter>(view, "hudRowPrefab") }
            .Where(value => value != null).Select(value => value.gameObject).Distinct().ToArray();
    }

    internal static Text[][] TextRoles(HudLayoutAdapter hud)
    {
        var all = hud.GetComponentsInChildren<Text>(true);
        var headings = new[] { hud.EquipmentHeading, hud.Close != null ? hud.Close.GetComponentInChildren<Text>(true) : null }
            .Where(value => value != null).ToArray();
        var notes = new[] { hud.Feedback, hud.EquipmentFeedback }.Where(value => value != null).ToArray();
        return new[] { headings, all.Except(headings).Except(notes).ToArray(), notes };
    }

    private static void DrawStatic(HudLayoutAdapter hud)
    {
        var roles = TextRoles(hud);
        DrawText(roles[0], "見出し"); DrawText(roles[1], "本文・操作"); DrawText(roles[2], "補足・結果");
        var selected = hud.SelectedTabColor; var idle = hud.IdleTabColor;
        DrawProperties(hud, "タブの表示色", "SelectedTabColor", "IdleTabColor");
        PreviewTabPalette(hud, selected, idle);
        EditorGUILayout.HelpBox("タブ色の変更は配置ページを選択した状態でプレビューします。配置ボタンの表示色は生成行欄のBuilder設定を使います。", MessageType.None);
        DrawGeometry(hud.gameObject);
        DrawImagesAndButtons(hud.gameObject);
    }

    internal static void PreviewTabPalette(HudLayoutAdapter hud, Color previousSelected, Color previousIdle)
    {
        if (hud.SelectedTabColor == previousSelected && hud.IdleTabColor == previousIdle) return;
        for (var i = 0; i < hud.Tabs.Length; i++)
        {
            if (hud.Tabs[i] == null) continue;
            using var data = new SerializedObject(hud.Tabs[i].GetComponent<Image>());
            data.FindProperty("m_Color").colorValue = i == 0 ? hud.SelectedTabColor : hud.IdleTabColor;
            data.ApplyModifiedProperties();
        }
    }

    private static void DrawText(Text[] texts, string label)
    {
        if (texts.Length == 0) return;
        EditorGUILayout.LabelField(label, EditorStyles.boldLabel);
        DrawProperties(texts.Cast<Object>().ToArray(), "共通項目", "m_FontData.m_Font", "m_FontData.m_FontSize", "m_Color");
        foreach (var text in texts) EditorGUILayout.ObjectField(text.name, text, typeof(Text), true);
    }

    private static void DrawPart(GameObject root, bool shared)
    {
        EditorGUILayout.Space();
        EditorGUILayout.ObjectField(shared ? "共有行Prefab" : "操作帯", root, typeof(GameObject), true);
        Destination(root);
        if (!Foldout(root, "表示と配置を編集")) return;
        // 行Prefab内にも見出し・本文・補足が混在するため、サイズを一律に揃えない。
        foreach (var text in root.GetComponentsInChildren<Text>(true))
        {
            EditorGUILayout.ObjectField("文字の編集先", text, typeof(Text), true);
            DrawProperties(text, "文字：" + text.name, "m_FontData.m_Font", "m_FontData.m_FontSize", "m_Color");
        }
        DrawImagesAndButtons(root);
        DrawGeometry(root);
    }

    private static void DrawImagesAndButtons(GameObject root)
    {
        if (!Foldout(root, "背景・ボタンの色")) return;
        foreach (var image in root.GetComponentsInChildren<Image>(true))
        {
            EditorGUILayout.ObjectField(image.GetComponent<Button>() != null ? "ボタン背景" : "背景・画像", image, typeof(Image), true);
            DrawProperties(image, image.name, "m_Color");
        }
        foreach (var button in root.GetComponentsInChildren<Button>(true))
            DrawProperties(button, "ボタン状態色：" + button.name, "m_Colors");
    }

    private static void DrawGeometry(GameObject root)
    {
        if (!Foldout(root, "余白・間隔・配置")) return;
        EditorGUILayout.HelpBox("自動整列する部品はLayoutGroupの余白・間隔を編集します。常時表示の項目位置、文字に合わせる行の高さ、ゲージ幅は表示内容に応じて自動更新します。", MessageType.None);
        foreach (var group in root.GetComponentsInChildren<LayoutGroup>(true))
        {
            EditorGUILayout.ObjectField("整列する範囲", group, typeof(LayoutGroup), true);
            if (group is GridLayoutGroup) DrawProperties(group, group.name, "m_Padding", "m_Spacing", "m_CellSize");
            else DrawProperties(group, group.name, "m_Padding", "m_Spacing");
        }
        foreach (var rect in EditableRects(root))
        {
            EditorGUILayout.ObjectField("配置する部品", rect, typeof(RectTransform), true);
            DrawProperties(rect, rect.name, "m_AnchoredPosition", "m_SizeDelta");
        }
    }

    internal static RectTransform[] EditableRects(GameObject root)
    {
        var hud = root.GetComponent<HudLayoutAdapter>();
        var gauge = Reference<Image>(root.GetComponent<HudStatusRowAdapter>(), "_gauge");
        // 自動配置で上書きされる座標は一括編集から外し、固定枠の保存座標だけを示す。
        return root.GetComponentsInChildren<RectTransform>(true).Where(rect =>
            (rect.parent == null || rect.parent.GetComponent<LayoutGroup>() == null) &&
            (hud == null || hud.Summary == null || rect == hud.Summary || !rect.IsChildOf(hud.Summary)) &&
            (gauge == null || rect != gauge.rectTransform) &&
            rect.GetComponent<ContentSizeFitter>() == null && rect.GetComponent<LayoutElement>() == null).ToArray();
    }

    private static bool Foldout(Object root, string label)
    {
        var key = "UIS07.Inspector." + root.GetInstanceID() + "." + label;
        var expanded = EditorGUILayout.Foldout(SessionState.GetBool(key, false), label, true);
        SessionState.SetBool(key, expanded);
        return expanded;
    }

    private static void Destination(Object value)
    {
        var component = value as Component;
        var go = component != null ? component.gameObject : value as GameObject;
        var stage = go != null ? PrefabStageUtility.GetPrefabStage(go) : null;
        var path = AssetDatabase.GetAssetPath(value);
        var message = stage != null ? "変更先：Prefab Mode " + stage.assetPath + "（Prefabを保存）"
            : EditorUtility.IsPersistent(value) ? "変更先：共有アセット " + path + "（Projectを保存）"
            : "変更先：Scene " + (go != null ? go.scene.path : "") + "（Sceneを保存）";
        if (stage == null && PrefabUtility.IsPartOfPrefabInstance(value))
            message += "\nPrefab Overrideとして保持します。Apply/Revertは既存InspectorのOverridesで操作してください。";
        EditorGUILayout.HelpBox(message, MessageType.None);
    }

    private static void DrawProperties(Object value, string label, params string[] paths)
        => DrawProperties(value != null ? new[] { value } : Array.Empty<Object>(), label, paths);

    private static void DrawProperties(Object[] values, string label, params string[] paths)
    {
        if (values.Length == 0) return;
        using var data = new SerializedObject(values);
        data.Update();
        EditorGUILayout.LabelField(label);
        EditorGUI.indentLevel++;
        foreach (var path in paths)
        {
            var property = data.FindProperty(path);
            if (property != null) EditorGUILayout.PropertyField(property, Label(path), true);
        }
        EditorGUI.indentLevel--;
        // SerializedPropertyは変更した値だけをUndo・Prefab Overrideへ記録する。
        data.ApplyModifiedProperties();
    }

    private static GUIContent Label(string path) => new(path switch
    {
        "m_FontData.m_Font" => "フォント", "m_FontData.m_FontSize" => "文字サイズ", "m_Color" => "色",
        "m_Colors" => "通常・強調・押下・選択・無効", "m_Padding" => "内側の余白", "m_Spacing" => "間隔",
        "m_CellSize" => "セル寸法", "m_AnchoredPosition" => "位置", "m_SizeDelta" => "寸法の差分",
        "SelectedTabColor" => "選択中", "IdleTabColor" => "未選択", "defaultBackgroundColor" => "通常",
        "openedSubMenuColor" => "サブメニュー展開中", "subMenuColor" => "サブメニュー背景", _ => path
    });
}

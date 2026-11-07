using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>Inspectorの保存先と編集保持を、既存HUDの回帰検証につなぐ。</summary>
public sealed class HudLayoutEditingVerifierEditor : EditorWindow
{
    private const string Key = "UIS07.Verify";
    private const string Temporary = "Assets/__HudEditingVerification";
    private Editor _inspector;
    private Vector2 _scroll;
    private int _draws;
    private string[] _paths, _contents;
    private static void Require(bool value, string message)
    { if (!value) throw new InvalidOperationException("HUD editing: " + message); }

    public static void RunRendered()
    {
        try
        {
            Require(Application.isBatchMode, "verification uses a dedicated batch project");
            EditorSceneManager.OpenScene("Assets/Scenes/MainScene.unity");
            if (AssetDatabase.IsValidFolder(Temporary)) AssetDatabase.DeleteAsset(Temporary);
            var window = CreateInstance<HudLayoutEditingVerifierEditor>();
            window.titleContent = new GUIContent("HUD editing verification");
            window._paths = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs/UI" })
                .Select(AssetDatabase.GUIDToAssetPath).Append("Assets/Scenes/MainScene.unity").ToArray();
            window._contents = window._paths.Select(File.ReadAllText).ToArray();
            var hud = Object.FindAnyObjectByType<HudLayoutAdapter>(FindObjectsInactive.Include);
            foreach (var root in HudLayoutInspectorEditor.RowRoots(hud).Append(hud.gameObject).Append(Bar().gameObject))
                foreach (var label in new[] { "表示と配置を編集", "背景・ボタンの色", "余白・間隔・配置" })
                    SessionState.SetBool("UIS07.Inspector." + root.GetInstanceID() + "." + label, true);
            window._inspector = Editor.CreateEditor(hud);
            foreach (var field in new[] { "_rows", "_bar", "_references" })
                typeof(HudLayoutInspectorEditor).GetField(field, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                    .SetValue(window._inspector, true);
            window.Show();
            window.Repaint();
        }
        catch (Exception exception) { Debug.LogException(exception); EditorApplication.Exit(1); }
    }

    private void OnGUI()
    {
        if (_inspector == null) return;
        _scroll = EditorGUILayout.BeginScrollView(_scroll);
        _inspector.OnInspectorGUI();
        EditorGUILayout.EndScrollView();
        if (Event.current.type != EventType.Repaint || ++_draws != 2) { Repaint(); return; }
        EditorApplication.delayCall += Continue;
    }

    private void Continue()
    {
        try
        {
            Require(_paths.Select(File.ReadAllText).SequenceEqual(_contents), "Inspector repaints preserve saved files");
            Require(!UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty, "Inspector repaint keeps scene clean");
            Require(_paths.Where(p => p.EndsWith(".prefab")).All(p => !EditorUtility.IsDirty(AssetDatabase.LoadAssetAtPath<GameObject>(p))),
                "Inspector repaint keeps shared prefabs clean");
            DestroyImmediate(_inspector); _inspector = null; Close();
            VerifyEditMode();
            SessionState.SetBool(Key, true);
            EconomyUIVerifierEditor.RunPreparedRendered();
        }
        catch (Exception exception) { Debug.LogException(exception); EditorApplication.Exit(1); }
    }

    private static void Set(Object[] values, string path, Action<SerializedProperty> change)
    {
        using var data = new SerializedObject(values);
        data.Update(); change(data.FindProperty(path)); data.ApplyModifiedProperties();
        Undo.FlushUndoRecordObjects();
    }
    private static void Set(Object value, string path, Action<SerializedProperty> change) => Set(new[] { value }, path, change);
    private static HudLayoutAdapter Hud() => Object.FindAnyObjectByType<HudLayoutAdapter>(FindObjectsInactive.Include);
    private static OperationBarAdapter Bar() => Object.FindAnyObjectByType<OperationBarAdapter>(FindObjectsInactive.Include);

    private static void VerifyEditMode()
    {
        AssetDatabase.CreateFolder("Assets", "__HudEditingVerification");
        var hud = Hud();
        var originalRows = HudLayoutInspectorEditor.RowRoots(hud);
        // 共有Prefabの試験は参照先を複製し、本来のアセットへ波及しないことも照合する。
        foreach (var root in originalRows)
            Require(AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(root), Temporary + "/" + root.name + ".prefab"), "copy referenced row " + root.name);
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        EditorSceneManager.SaveScene(scene, Temporary + "/Scene.unity");
        Replace(hud, "TextRow"); Replace(hud, "RecipeHeading"); Replace(hud, "SkillRow"); Replace(hud, "ResourceRow");
        Replace(hud.PlacementBuilder, "buttonPrefab");
        Replace(hud.GetComponentInChildren<CellStatusViewAdapter>(true), "hudRowPrefab");

        var roles = HudLayoutInspectorEditor.TextRoles(hud);
        Require(!HudLayoutInspectorEditor.EditableRects(hud.gameObject).Intersect(hud.WalletItems.Select(text => text.rectTransform)).Any(),
            "automatically positioned summary items are excluded from fixed coordinates");
        var headingSizes = roles[0].Select(text => text.fontSize).ToArray();
        var noteSizes = roles[2].Select(text => text.fontSize).ToArray();
        Set(roles[1].Cast<Object>().ToArray(), "m_FontData.m_FontSize", p => p.intValue = 17);
        Require(roles[1].All(text => text.fontSize == 17), "group body size applies in Edit Mode");
        Require(headingSizes.SequenceEqual(roles[0].Select(text => text.fontSize)) && noteSizes.SequenceEqual(roles[2].Select(text => text.fontSize)),
            "body edit preserves heading and note hierarchy");
        Undo.PerformUndo(); Require(roles[1].All(text => text.fontSize == 16), "group Undo");
        Undo.PerformRedo(); Require(roles[1].All(text => text.fontSize == 17), "group Redo");
        Require(PrefabUtility.GetPropertyModifications(hud.gameObject).Any(p => p.propertyPath == "m_FontData.m_FontSize"), "scene font override recorded");
        Set(hud.EquipmentText, "m_Color", p => p.colorValue = Color.cyan);
        Set(Bar().ViewButton.GetComponentInChildren<Text>(), "m_FontData.m_FontSize", p => p.intValue = 17);
        var previousSelected = hud.SelectedTabColor; var previousIdle = hud.IdleTabColor;
        Set(hud, "SelectedTabColor", p => p.colorValue = new Color(.3f, .5f, .6f));
        HudLayoutInspectorEditor.PreviewTabPalette(hud, previousSelected, previousIdle);
        Require(hud.Tabs[0].GetComponent<Image>().color == hud.SelectedTabColor, "tab color previews without play");
        var grid = hud.PlacementBuilder.GetComponent<GridLayoutGroup>();
        var oldSpacing = grid.spacing;
        Set(grid, "m_Spacing", p => p.vector2Value = oldSpacing + Vector2.one);
        Require(grid.spacing == oldSpacing + Vector2.one, "spacing edit");
        Undo.PerformUndo(); Require(grid.spacing == oldSpacing, "spacing Undo");
        Undo.PerformRedo(); Require(grid.spacing == oldSpacing + Vector2.one, "spacing Redo");
        // フォント参照の編集もSerializedProperty経由で戻せることを確認する。
        var font = hud.EquipmentText.font;
        Set(hud.EquipmentText, "m_FontData.m_Font", p => p.objectReferenceValue = null);
        Require(hud.EquipmentText.font == null, "font reference edit");
        Undo.PerformUndo(); Require(hud.EquipmentText.font == font && font.HasCharacter('換'), "font Undo and Japanese glyph");

        foreach (var row in HudLayoutInspectorEditor.RowRoots(hud))
        {
            var texts = row.GetComponentsInChildren<Text>(true);
            Set(texts.Cast<Object>().ToArray(), "m_FontData.m_FontSize", p => p.intValue = row == hud.RecipeHeading.gameObject ? 19 : 17);
            AssetDatabase.SaveAssetIfDirty(row);
            AssetDatabase.ImportAsset(AssetDatabase.GetAssetPath(row), ImportAssetOptions.ForceUpdate);
            Require(row.GetComponentsInChildren<Text>(true).All(t => t.fontSize == (row == hud.RecipeHeading.gameObject ? 19 : 17)), "row save and reload " + row.name);
        }
        EditorSceneManager.SaveScene(scene);
        EditorSceneManager.OpenScene(Temporary + "/Scene.unity");
        hud = Hud();
        Require(hud.EquipmentText.fontSize == 17 && hud.EquipmentText.color == Color.cyan, "scene save and reload keeps individual edits");
        Require(Bar().ViewButton.GetComponentInChildren<Text>().fontSize == 17, "separate operation instance save and reload");
        Require(hud.PlacementBuilder.GetComponent<GridLayoutGroup>().spacing == oldSpacing + Vector2.one, "spacing save and reload");

        var prefabPath = Temporary + "/Hud.prefab";
        var contents = PrefabUtility.LoadPrefabContents("Assets/Prefabs/UI/FactoryHud.prefab");
        PrefabUtility.SaveAsPrefabAsset(contents, prefabPath); PrefabUtility.UnloadPrefabContents(contents);
        var stage = PrefabStageUtility.OpenPrefab(prefabPath);
        var stageHud = stage.prefabContentsRoot.GetComponent<HudLayoutAdapter>();
        Set(stageHud.EquipmentHeading, "m_FontData.m_FontSize", p => p.intValue = 20);
        Require(stageHud.EquipmentHeading.fontSize == 20, "Prefab Mode editing");
        Undo.PerformUndo(); Require(stageHud.EquipmentHeading.fontSize == 18, "Prefab Mode Undo");
        Undo.PerformRedo(); Require(stageHud.EquipmentHeading.fontSize == 20, "Prefab Mode Redo");
        PrefabUtility.SaveAsPrefabAsset(stage.prefabContentsRoot, prefabPath);
        StageUtility.GoToMainStage();
        Require(AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath).GetComponent<HudLayoutAdapter>().EquipmentHeading.fontSize == 20,
            "Prefab Mode save and reload");
        Require(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/FactoryHud.prefab").GetComponent<HudLayoutAdapter>().EquipmentHeading.fontSize == 18,
            "Prefab Mode edit leaves source HUD unchanged");
        Require(originalRows.All(row => row.GetComponentsInChildren<Text>(true).All(t => t.fontSize == (row.name == "HudRecipeHeading" ? 18 : 16))),
            "unrelated shared row assets preserved");
        SessionState.SetString(Key + ".Spacing", JsonUtility.ToJson(new Spacing { value = oldSpacing }));
        Debug.Log("HUD editing Edit Mode checks passed: repaint, grouped and individual edits, hierarchy, Undo/Redo, scene Overrides, shared row isolation, Prefab Mode, save/reload.");
    }

    [Serializable] private struct Spacing { public Vector2 value; }
    private static void Replace(Object owner, string field)
    {
        using var data = new SerializedObject(owner);
        var property = data.FindProperty(field);
        var component = (Component)property.objectReferenceValue;
        var copy = AssetDatabase.LoadAssetAtPath<GameObject>(Temporary + "/" + component.gameObject.name + ".prefab");
        property.objectReferenceValue = copy.GetComponent(component.GetType()); data.ApplyModifiedProperties();
    }

    public static void VerifyRuntime(EconomyPanelAdapter panel)
    {
        if (!SessionState.GetBool(Key, false)) return;
        SessionState.EraseBool(Key);
        var hud = Hud(); var bar = Bar();
        Require(hud.EquipmentText.fontSize == 17 && hud.EquipmentText.color == Color.cyan, "static scene edits retained after play");
        Require(bar.ViewButton.GetComponentInChildren<Text>().fontSize == 17, "operation edit retained after play");
        Require(hud.Tabs[0].GetComponent<Image>().color == hud.SelectedTabColor && hud.SelectedTabColor == new Color(.3f, .5f, .6f), "runtime tab palette retained");
        panel.SetSummaryItems("編集保持");
        Require(panel.SummaryView.Items[0].fontSize == 17, "generated summary uses edited prefab");
        panel.SetSummaryItems("再表示"); Require(panel.SummaryView.Items[0].fontSize == 17, "summary refresh retains style");
        Require(panel.PlacementButtons.GetComponentsInChildren<CellSelectButtonUIAdapter>(true).All(b => b.GetComponentInChildren<Text>(true).fontSize == 17), "generated placement style");
        Require(hud.Pages[2].GetComponentsInChildren<Text>(true).All(t => t.fontSize == 17), "generated skill style");
        panel.RecipeView.Render(hud.Recipes, hud.Resources);
        Require(panel.RecipeContent.GetComponentsInChildren<RecipeResourceRowAdapter>(true).All(r => r.GetComponentInChildren<Text>(true).fontSize == 17), "recipe list rebuild keeps edited style");
        Require(panel.RecipeContent.GetComponentsInChildren<Text>(true).Any(t => t.fontSize == 19), "recipe heading hierarchy retained");
        var view = hud.GetComponentInChildren<CellStatusViewAdapter>(true);
        view.UpdateUIStatusWindow(GridFieldDatabaseAdapter.Instance.GetCell(50, 50));
        var statusRows = view.StatusContent.GetComponentsInChildren<HudStatusRowAdapter>();
        Require(statusRows.Length > 0 && statusRows.SelectMany(row => row.GetComponentsInChildren<Text>()).All(t => t.fontSize == 17),
            "pooled status rows retain edited prefab style");
        view.SetStatusWindowActive(false);
        Debug.Log("HUD editing Play Mode checks passed: fixed UI, separate operation bar, tab color, generated placement/summary/skill/recipe/status rows and list refresh.");
        // 続く既存回帰と最終画像は既定の見た目で比較する。
        foreach (var text in hud.GetComponentsInChildren<Text>(true))
        {
            if (text.fontSize == 17) text.fontSize = 16;
            if (text.fontSize == 19 && text != hud.Close.GetComponentInChildren<Text>(true)) text.fontSize = 18;
        }
        hud.EquipmentText.color = Color.white;
        bar.ViewButton.GetComponentInChildren<Text>().fontSize = 16;
        hud.SelectedTabColor = new Color(.24f, .43f, .53f); panel.ShowPage(0);
        hud.PlacementBuilder.GetComponent<GridLayoutGroup>().spacing = JsonUtility.FromJson<Spacing>(SessionState.GetString(Key + ".Spacing", "")).value;
        foreach (var row in HudLayoutInspectorEditor.RowRoots(hud))
            foreach (var text in row.GetComponentsInChildren<Text>(true)) text.fontSize = row == hud.RecipeHeading.gameObject ? 18 : 16;
        panel.SetSummaryItems(); panel.RecipeView.Render(hud.Recipes, hud.Resources);
    }
}

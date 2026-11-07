using System;
using System.IO;
using System.Linq;
using Cysharp.Threading.Tasks;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>共通項目の一括編集と保存先を、既存HUD回帰へ接続して検証する。</summary>
public sealed class HudLayoutEditingVerifierEditor : EditorWindow
{
    private const string Key = "UIS07.Verify";
    private const string Temporary = "Assets/__HudEditingVerification";
    private Editor _inspector;
    private Vector2 _scroll;
    private int _draws;
    private string[] _paths, _contents;
    private static readonly Color TextColor = new(.9f, .95f, 1), PanelColor = new(.08f, .1f, .14f),
        ButtonColor = new(.2f, .3f, .4f), SelectedColor = new(.3f, .5f, .6f);
    private static void Require(bool value, string message)
    { if (!value) throw new InvalidOperationException("HUD editing: " + message); }
    private static HudLayoutAdapter Hud() => Object.FindAnyObjectByType<HudLayoutAdapter>(FindObjectsInactive.Include);
    private static OperationBarAdapter Bar() => Object.FindAnyObjectByType<OperationBarAdapter>(FindObjectsInactive.Include);

    public static void RunRendered()
    {
        try
        {
            Require(Application.isBatchMode, "verification uses a dedicated batch project");
            EditorSceneManager.OpenScene("Assets/Scenes/MainScene.unity");
            if (AssetDatabase.IsValidFolder(Temporary)) AssetDatabase.DeleteAsset(Temporary);
            var window = CreateInstance<HudLayoutEditingVerifierEditor>();
            window.titleContent = new GUIContent("HUD common settings verification");
            window._paths = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs/UI" })
                .Select(AssetDatabase.GUIDToAssetPath).Append("Assets/Scenes/MainScene.unity").ToArray();
            window._contents = window._paths.Select(File.ReadAllText).ToArray();
            window._inspector = Editor.CreateEditor(Hud());
            window.Show(); window.Repaint();
        }
        catch (Exception exception) { Fail(exception); }
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
            Require(_paths.Select(File.ReadAllText).SequenceEqual(_contents), "initial Inspector repaints preserve saved files");
            Require(!UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty, "initial Inspector keeps scene clean");
            Require(_paths.Where(p => p.EndsWith(".prefab")).All(p => !EditorUtility.IsDirty(AssetDatabase.LoadAssetAtPath<GameObject>(p))), "initial Inspector keeps prefabs clean");
            DestroyImmediate(_inspector); _inspector = null; Close();
            VerifyEditMode(); SessionState.SetBool(Key, true);
            EconomyUIVerifierEditor.RunPreparedRendered();
        }
        catch (Exception exception) { Fail(exception); }
    }
    private static void Fail(Exception exception) { Debug.LogException(exception); EditorApplication.Exit(1); }
    private static void Replace(Object owner, string field)
    {
        using var data = new SerializedObject(owner);
        var property = data.FindProperty(field); var component = (Component)property.objectReferenceValue;
        var copy = AssetDatabase.LoadAssetAtPath<GameObject>(Temporary + "/" + component.gameObject.name + ".prefab");
        property.objectReferenceValue = copy.GetComponent(component.GetType()); data.ApplyModifiedProperties();
    }
    private static void ReplaceRows(HudLayoutAdapter hud)
    {
        Replace(hud, "TextRow"); Replace(hud, "RecipeHeading"); Replace(hud, "SkillRow"); Replace(hud, "ResourceRow");
        Replace(hud.PlacementBuilder, "buttonPrefab"); Replace(hud.GetComponentInChildren<CellStatusViewAdapter>(true), "hudRowPrefab");
    }
    private static void VerifyEditMode()
    {
        AssetDatabase.CreateFolder("Assets", "__HudEditingVerification");
        var hud = Hud(); var bar = Bar();
        var originalRows = HudLayoutInspectorEditor.RowRoots(hud);
        var originalFiles = originalRows.Select(AssetDatabase.GetAssetPath).Append("Assets/Prefabs/UI/FactoryHud.prefab").ToArray();
        var originalContents = originalFiles.Select(File.ReadAllText).ToArray();
        foreach (var root in originalRows)
            Require(AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(root), Temporary + "/" + root.name + ".prefab"), "copy row " + root.name);
        Require(AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(hud.Font), Temporary + "/Font.fontsettings"), "copy font");
        var font = AssetDatabase.LoadAssetAtPath<Font>(Temporary + "/Font.fontsettings");
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        EditorSceneManager.SaveScene(scene, Temporary + "/Scene.unity"); ReplaceRows(hud);
        var texts = HudLayoutInspectorEditor.Texts(hud, bar);
        var sizes = texts.Select(t => t.fontSize).ToArray();
        var imageColors = HudLayoutInspectorEditor.Roots(hud, bar).SelectMany(r => r.GetComponentsInChildren<Image>(true)).Select(i => i.color).ToArray();
        var geometry = HudLayoutInspectorEditor.Roots(hud, bar).SelectMany(r => r.GetComponentsInChildren<RectTransform>(true))
            .Select(r => JsonUtility.ToJson(new Geometry { position = r.anchoredPosition, size = r.sizeDelta })).ToArray();
        HudLayoutInspectorEditor.ApplyBaseSize(hud, bar, 17);
        Require(texts.Select(t => t.fontSize).SequenceEqual(sizes.Select(s => s + 1)), "one size edit preserves every heading/body/note and individual difference");
        Undo.PerformUndo(); Require(texts.Select(t => t.fontSize).SequenceEqual(sizes), "one Undo restores all static/row/operation sizes");
        Undo.PerformRedo(); Require(texts.Select(t => t.fontSize).SequenceEqual(sizes.Select(s => s + 1)), "one Redo restores all sizes");
        Require(PrefabUtility.GetPropertyModifications(hud.gameObject).Any(p => p.propertyPath == "m_FontData.m_FontSize"), "Scene Override recorded");
        HudLayoutInspectorEditor.ApplyFont(hud, bar, font);
        Require(texts.All(t => t.font == font) && font.HasCharacter('換'), "common Japanese font across all roots");
        Undo.PerformUndo(); Require(texts.All(t => t.font != font), "one font Undo across all roots");
        Undo.PerformRedo(); Require(texts.All(t => t.font == font), "one font Redo across all roots");
        HudLayoutInspectorEditor.ApplyTextColor(hud, bar, TextColor);
        Require(texts.All(t => t.color == TextColor), "common text color across all roots");
        Require(imageColors.SequenceEqual(HudLayoutInspectorEditor.Roots(hud, bar).SelectMany(r => r.GetComponentsInChildren<Image>(true)).Select(i => i.color)), "text edits preserve image colors");
        var protectedImages = HudLayoutInspectorEditor.RowRoots(hud).SelectMany(r => r.GetComponentsInChildren<Image>(true))
            .Where(i => i.GetComponent<Button>() == null).ToArray();
        var protectedColors = protectedImages.Select(i => i.color).ToArray();
        var previousPanels = HudLayoutInspectorEditor.Panels(hud, bar).Select(i => i.color).ToArray();
        HudLayoutInspectorEditor.ApplyPanelColor(hud, bar, PanelColor);
        Require(HudLayoutInspectorEditor.Panels(hud, bar).Length == 4 && HudLayoutInspectorEditor.Panels(hud, bar).All(i => i.color == PanelColor), "four explicit panels including operation bar");
        Require(protectedColors.SequenceEqual(protectedImages.Select(i => i.color)), "icons and gauge excluded from panel color");
        Undo.PerformUndo(); Require(previousPanels.SequenceEqual(HudLayoutInspectorEditor.Panels(hud, bar).Select(i => i.color)), "one panel Undo");
        Undo.PerformRedo();
        HudLayoutInspectorEditor.ApplyButtonColor(hud, bar, ButtonColor);
        Require(HudLayoutInspectorEditor.Buttons(hud, bar).Where(b => b != hud.Tabs[0]).All(b => b.targetGraphic.color == ButtonColor), "common normal color for static/row/operation buttons");
        Require(HudLayoutInspectorEditor.Reference<SubMenuUIBuilderAdapter>(hud.PlacementBuilder, "subMenuUIBuilder") != null, "submenu color owner resolved");
        HudLayoutInspectorEditor.ApplySelectedColor(hud, bar, SelectedColor);
        Require(hud.SelectedTabColor == SelectedColor && hud.Tabs[0].targetGraphic.color == SelectedColor &&
            HudLayoutInspectorEditor.Buttons(hud, bar).All(b => b.colors.selectedColor == SelectedColor), "common selected tab and button tint");
        Undo.PerformUndo(); Require(hud.SelectedTabColor != SelectedColor && HudLayoutInspectorEditor.Buttons(hud, bar).All(b => b.colors.selectedColor != SelectedColor), "one selection Undo includes all targets");
        Undo.PerformRedo();
        Require(geometry.SequenceEqual(HudLayoutInspectorEditor.Roots(hud, bar).SelectMany(r => r.GetComponentsInChildren<RectTransform>(true))
            .Select(r => JsonUtility.ToJson(new Geometry { position = r.anchoredPosition, size = r.sizeDelta }))), "common edits preserve saved geometry");
        foreach (var row in HudLayoutInspectorEditor.RowRoots(hud))
        { AssetDatabase.SaveAssetIfDirty(row); AssetDatabase.ImportAsset(AssetDatabase.GetAssetPath(row), ImportAssetOptions.ForceUpdate); }
        EditorSceneManager.SaveScene(scene); EditorSceneManager.OpenScene(Temporary + "/Scene.unity");
        hud = Hud(); bar = Bar();
        Require(HudLayoutInspectorEditor.Texts(hud, bar).All(t => t.font == font && t.color == TextColor), "common font and text color survive save/reload");
        Require(hud.EquipmentHeading.fontSize == 19 && hud.Feedback.fontSize == 15 && hud.TextRow.fontSize == 17 && bar.ViewButton.GetComponentInChildren<Text>().fontSize == 17, "all size roles survive save/reload");
        Require(HudLayoutInspectorEditor.Panels(hud, bar).All(i => i.color == PanelColor) && bar.ViewButton.targetGraphic.color == ButtonColor && hud.SelectedTabColor == SelectedColor, "all palette fields survive save/reload");

        var prefabPath = Temporary + "/Hud.prefab";
        var contents = PrefabUtility.LoadPrefabContents("Assets/Prefabs/UI/FactoryHud.prefab");
        PrefabUtility.SaveAsPrefabAsset(contents, prefabPath); PrefabUtility.UnloadPrefabContents(contents);
        var stage = PrefabStageUtility.OpenPrefab(prefabPath);
        var stageHud = stage.prefabContentsRoot.GetComponent<HudLayoutAdapter>(); ReplaceRows(stageHud);
        Require(HudLayoutInspectorEditor.SceneOperation(stageHud) == null, "Prefab Mode does not auto-target another scene");
        HudLayoutInspectorEditor.ApplyTextColor(stageHud, null, Color.green);
        Require(stageHud.EquipmentHeading.color == Color.green && stageHud.TextRow.color == Color.green, "Prefab Mode common edit includes shared rows");
        Undo.PerformUndo(); Require(stageHud.EquipmentHeading.color == Color.white && stageHud.TextRow.color == TextColor, "Prefab Mode one Undo restores local and shared values");
        Undo.PerformRedo(); Require(stageHud.EquipmentHeading.color == Color.green, "Prefab Mode Redo");
        PrefabUtility.SaveAsPrefabAsset(stage.prefabContentsRoot, prefabPath);
        StageUtility.GoToMainStage();
        Require(AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath).GetComponent<HudLayoutAdapter>().EquipmentHeading.color == Color.green, "Prefab Mode saved and reloaded");
        HudLayoutInspectorEditor.ApplyTextColor(Hud(), Bar(), TextColor);
        Require(originalFiles.Select(File.ReadAllText).SequenceEqual(originalContents), "original shared assets remain unchanged");
        Debug.Log("HUD common Edit Mode checks passed: six bulk actions, size differences, icons/gauge exclusion, no geometry edits, atomic Undo/Redo, Overrides, Prefab Mode and save/reload.");
    }
    [Serializable] private struct Geometry { public Vector2 position, size; }

    public static async UniTask VerifyRuntime(EconomyPanelAdapter panel)
    {
        if (!SessionState.GetBool(Key, false)) return;
        SessionState.EraseBool(Key);
        var hud = Hud(); var bar = Bar();
        var font = AssetDatabase.LoadAssetAtPath<Font>(Temporary + "/Font.fontsettings");
        Require(hud.EquipmentHeading.fontSize == 19 && hud.Feedback.fontSize == 15 && hud.EquipmentText.fontSize == 17 &&
            bar.ViewButton.GetComponentInChildren<Text>().fontSize == 17, "fixed and separate operation size hierarchy after play");
        Require(HudLayoutInspectorEditor.Texts(hud, bar).All(t => t.font == font && t.color == TextColor), "all text font and color after play");
        Require(HudLayoutInspectorEditor.Panels(hud, bar).All(i => i.color == PanelColor) && bar.ViewButton.targetGraphic.color == ButtonColor &&
            hud.Tabs[0].targetGraphic.color == SelectedColor, "palette after play");
        panel.SetSummaryItems("編集保持"); panel.SetSummaryItems("再表示");
        Require(panel.SummaryView.Items[0].fontSize == 17 && panel.SummaryView.Items[0].color == TextColor, "summary creation and reuse");
        var placement = panel.PlacementButtons.GetComponentsInChildren<CellSelectButtonUIAdapter>(true);
        Require(placement.Length > 0 && placement.All(b => b.GetComponentInChildren<Text>(true).fontSize == 17 && b.GetComponentInChildren<Text>(true).font == font), "placement generation");
        Require(placement.All(b => b.GetComponentInChildren<Button>(true).targetGraphic.color == ButtonColor &&
            HudLayoutInspectorEditor.Reference<Image>(b, "backgroundImage").color == ButtonColor), "builder and submenu keep normal palette");
        Require(hud.Pages[2].GetComponentsInChildren<Text>(true).All(t => t.fontSize == 17), "generated skill text");
        panel.RecipeView.Render(hud.Recipes, hud.Resources);
        Require(panel.RecipeContent.GetComponentsInChildren<RecipeResourceRowAdapter>(true).All(r => r.GetComponentInChildren<Text>(true).fontSize == 17 && r.GetComponentInChildren<Text>(true).color == TextColor) &&
            panel.RecipeContent.GetComponentsInChildren<Text>(true).Any(t => t.fontSize == 19), "recipe rebuild keeps font/color/hierarchy");
        var view = hud.GetComponentInChildren<CellStatusViewAdapter>(true);
        view.UpdateUIStatusWindow(GridFieldDatabaseAdapter.Instance.GetCell(50, 50));
        var rows = view.StatusContent.GetComponentsInChildren<HudStatusRowAdapter>();
        Require(rows.Length > 0 && rows.SelectMany(row => row.GetComponentsInChildren<Text>()).All(t => t.fontSize == 17 && t.font == font && t.color == TextColor), "pooled state row style");
        Debug.Log("HUD common Play Mode checks passed: fixed/operation palette, six generated row types, list rebuild and pool reuse.");
        await EconomyUIVerifierEditor.VerifyCommonLayout(panel);
        view.SetStatusWindowActive(false);
        RestoreDefaults(panel, hud, bar, view);
    }

    private static void RestoreDefaults(EconomyPanelAdapter panel, HudLayoutAdapter hud, OperationBarAdapter bar, CellStatusViewAdapter view)
    {
        var sourceHud = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/FactoryHud.prefab").GetComponent<HudLayoutAdapter>();
        RestoreVisuals(sourceHud.gameObject, hud.gameObject);
        RestoreVisuals(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/OperationBar.prefab"), bar.gameObject);
        foreach (var row in HudLayoutInspectorEditor.RowRoots(hud))
            RestoreVisuals(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/" + row.name + ".prefab"), row);
        foreach (var text in panel.SummaryView.Items) RestoreText(sourceHud.TextRow, text);
        foreach (var button in panel.PlacementButtons.GetComponentsInChildren<CellSelectButtonUIAdapter>(true))
            RestoreVisuals(HudLayoutInspectorEditor.Reference<CellSelectButtonUIAdapter>(sourceHud.PlacementBuilder, "buttonPrefab").gameObject, button.gameObject);
        foreach (var button in hud.Pages[2].GetComponentsInChildren<Button>(true)) RestoreVisuals(sourceHud.SkillRow.gameObject, button.gameObject);
        foreach (var row in view.StatusContent.GetComponentsInChildren<HudStatusRowAdapter>(true))
            RestoreVisuals(HudLayoutInspectorEditor.Reference<HudStatusRowAdapter>(sourceHud.GetComponentInChildren<CellStatusViewAdapter>(true), "hudRowPrefab").gameObject, row.gameObject);
        hud.SelectedTabColor = sourceHud.SelectedTabColor; hud.IdleTabColor = sourceHud.IdleTabColor;
        CopyField(sourceHud.PlacementBuilder, hud.PlacementBuilder, "defaultBackgroundColor"); CopyField(sourceHud.PlacementBuilder, hud.PlacementBuilder, "openedSubMenuColor");
        CopyField(HudLayoutInspectorEditor.Reference<SubMenuUIBuilderAdapter>(sourceHud.PlacementBuilder, "subMenuUIBuilder"),
            HudLayoutInspectorEditor.Reference<SubMenuUIBuilderAdapter>(hud.PlacementBuilder, "subMenuUIBuilder"), "subMenuColor");
        // UIS-06が直後に検査する代表編集を保ち、他の値を既定へ戻して回帰と画像を比較する。
        hud.Close.GetComponentInChildren<Text>(true).fontSize = 19; bar.RotateButton.targetGraphic.color = new Color(.3f, .4f, .5f);
        panel.SetSummaryItems(); panel.RecipeView.Render(hud.Recipes, hud.Resources); panel.ShowPage(0);
        // 代表試験の折返しによる一時的な上余白を戻してから、既存回帰の基準座標を採る。
        hud.UpdateLayout();
        Require(Mathf.Abs(((RectTransform)hud.Close.transform).anchoredPosition.y -
            ((RectTransform)sourceHud.Close.transform).anchoredPosition.y) < .01f, "edited-size layout restores authored header position");
    }
    private static void CopyField(Object source, Object target, string path)
    {
        using var original = new SerializedObject(source); using var edited = new SerializedObject(target);
        edited.FindProperty(path).colorValue = original.FindProperty(path).colorValue; edited.ApplyModifiedPropertiesWithoutUndo();
    }
    private static void RestoreText(Text source, Text target)
    { target.font = source.font; target.fontSize = source.fontSize; target.color = source.color; }
    private static void RestoreVisuals(GameObject source, GameObject target)
    {
        foreach (var text in source.GetComponentsInChildren<Text>(true))
        { var item = FindTarget(text.transform, source.transform, target.transform)?.GetComponent<Text>(); if (item != null) RestoreText(text, item); }
        foreach (var image in source.GetComponentsInChildren<Image>(true))
        { var item = FindTarget(image.transform, source.transform, target.transform)?.GetComponent<Image>(); if (item != null) item.color = image.color; }
        foreach (var button in source.GetComponentsInChildren<Button>(true))
        { var item = FindTarget(button.transform, source.transform, target.transform)?.GetComponent<Button>(); if (item != null) item.colors = button.colors; }
    }
    private static Transform FindTarget(Transform child, Transform source, Transform target)
    { var path = AnimationUtility.CalculateTransformPath(child, source); return path.Length == 0 ? target : target.Find(path); }
}

#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>保存済みHUDの参照と編集保持を既存のUI回帰へ追加する。</summary>
public static class HudPrefabAssertions
{
    private static void Require(bool value, string message)
    { if (!value) throw new InvalidOperationException("HUD prefab: " + message); }
    public static void Prepare()
    {
        var hud = TestScenes.Find<HudLayoutAdapter>(FindObjectsInactive.Include);
        var bar = TestScenes.Find<OperationBarAdapter>(FindObjectsInactive.Include);
        Require(hud != null && bar != null, "scene contains both saved HUD roots; hud=" + (hud != null) + "; bar=" + (bar != null) +
            "; roots=" + string.Join(",", TestScenes.CurrentScene.GetRootGameObjects().Select(root => root.name)) +
            "; HUD asset=" + (AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/FactoryHud.prefab").GetComponent<HudLayoutAdapter>() != null) +
            "; HUD script=" + (AssetDatabase.LoadAssetAtPath<MonoScript>("Assets/Scripts/UI/Adapter/HudLayoutAdapter.cs").GetClass()?.FullName ?? "unresolved"));
        if (!Application.isPlaying)
        {
            Require(PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(hud) == "Assets/Prefabs/UI/FactoryHud.prefab", "scene uses HUD prefab");
            Require(PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(bar) == "Assets/Prefabs/UI/OperationBar.prefab", "scene uses operation prefab");
        }
        Require(TestScenes.All<SelectButtonBuilderAdapter>(FindObjectsInactive.Include).Length == 1, "one placement builder");
        Require(TestScenes.All<CellStatusViewAdapter>(FindObjectsInactive.Include).Length == 1, "one cell status view");
        foreach (var root in TestScenes.CurrentScene.GetRootGameObjects())
            foreach (var component in root.GetComponentsInChildren<Component>(true))
            {
                Require(component != null, "no missing script in scene");
                VerifyReferences(component);
            }
        foreach (var path in new[] { "FactoryHud", "OperationBar", "SelectButton", "Recipe", "HudTextRow", "HudRecipeHeading", "HudSkillRow", "HudStatusRow" })
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/" + path + ".prefab");
            Require(prefab != null, "saved prefab " + path);
            foreach (var component in prefab.GetComponentsInChildren<Component>(true))
            { Require(component != null, "prefab has no missing script " + path); VerifyReferences(component); }
        }
        Require(hud.TextRow.font != null && hud.TextRow.font.HasCharacter('換'), "saved font supports Japanese in Editor");
        // 保存済み参照に対する編集を再生前に加え、起動・再設定で保持されることを確認する。
        ((RectTransform)hud.Close.transform).anchoredPosition += new Vector2(3, 0);
        hud.Close.GetComponentInChildren<Text>().fontSize = 19;
        var palette = new SerializedObject(hud);
        palette.FindProperty("SelectedTabColor").colorValue = new Color(.3f, .5f, .6f);
        palette.FindProperty("IdleTabColor").colorValue = new Color(.12f, .22f, .32f);
        palette.ApplyModifiedProperties();
        ((RectTransform)bar.RotateButton.transform).anchoredPosition += new Vector2(2, 0);
        foreach (var edited in new Component[] { (RectTransform)hud.Close.transform, hud.Close.GetComponentInChildren<Text>(),
            hud, (RectTransform)bar.RotateButton.transform })
        {
            EditorUtility.SetDirty(edited);
            if (PrefabUtility.IsPartOfPrefabInstance(edited)) PrefabUtility.RecordPrefabInstancePropertyModifications(edited);
        }
        if (!Application.isPlaying) UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(TestScenes.CurrentScene);
        if (Application.isPlaying)
        {
            typeof(HudLayoutAdapter).GetField("_headerPosition", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).SetValue(hud, ((RectTransform)hud.Close.transform).anchoredPosition);
            hud.ApplyColors();
            hud.PlacementBuilder.Configure(hud.Font);
        }
        _prepared = true;
        Debug.Log("HUD prefab serialized references and Editor font checks passed.");
    }
    private static bool _prepared;
    private static void VerifyReferences(Component component)
    {
        var data = new SerializedObject(component); var property = data.GetIterator();
        while (property.NextVisible(true))
            if (property.propertyType == SerializedPropertyType.ObjectReference)
                Require(property.objectReferenceValue != null || property.objectReferenceInstanceIDValue == 0,
                    "resolved reference " + component.name + "." + property.propertyPath);
    }
    public static void VerifyEditModeColors()
    {
        var hud = TestScenes.Find<HudLayoutAdapter>(FindObjectsInactive.Include);
        var bar = TestScenes.Find<OperationBarAdapter>(FindObjectsInactive.Include);
        Require(!EditorApplication.isPlaying, "palette preview runs before play");
        Require(hud.Tabs[0].colors.normalColor == hud.SelectedTabColor && hud.Tabs[1].colors.normalColor == hud.IdleTabColor &&
            bar.RotateButton.colors.normalColor == hud.IdleTabColor && bar.ViewButton.colors.selectedColor == hud.SelectedTabColor,
            "standard Inspector colors preview in Edit Mode across tabs and operation buttons");
        Require(hud.Body.GetComponent<Image>().color == new Color(.07f, .09f, .12f, .96f) &&
            bar.Bar.GetComponent<Image>().color == new Color(.07f, .09f, .12f, .96f), "Edit Mode preview preserves black backgrounds");
        Debug.Log("HUD Edit Mode palette preview and black background checks passed.");
    }
    public static void VerifyRuntime(EconomyPanelAdapter panel)
    {
        Require(_prepared, "Editor edits prepared");
        var hud = panel.GetComponentInChildren<HudLayoutAdapter>();
        var bar = TestScenes.Find<OperationBarAdapter>();
        var header = (RectTransform)hud.Close.transform;
        Require(Mathf.Abs(header.anchoredPosition.x - 371) < .01f, "edited fixed frame position retained");
        Require(hud.Close.GetComponentInChildren<Text>().fontSize == 19, "edited font size retained");
        Require(hud.SelectedTabColor == new Color(.3f, .5f, .6f) && hud.IdleTabColor == new Color(.12f, .22f, .32f), "edited shared palette retained");
        var savedHud = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/FactoryHud.prefab").GetComponent<HudLayoutAdapter>();
        var savedBar = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/OperationBar.prefab").GetComponent<OperationBarAdapter>();
        Require(hud.Summary.GetComponent<Image>().color == savedHud.Summary.GetComponent<Image>().color &&
            hud.Body.GetComponent<Image>().color == savedHud.Body.GetComponent<Image>().color &&
            hud.GetComponentInChildren<CellStatusViewAdapter>(true).StatusScroll.GetComponent<Image>().color ==
                savedHud.GetComponentInChildren<CellStatusViewAdapter>(true).StatusScroll.GetComponent<Image>().color &&
            bar.Bar.GetComponent<Image>().color == savedBar.Bar.GetComponent<Image>().color, "black panel backgrounds retain saved colors");
        foreach (var button in hud.GetComponentsInChildren<Button>(true).Concat(bar.GetComponentsInChildren<Button>(true)))
        {
            Require(button.targetGraphic.color == Color.white, "no double color multiplication");
            var locked = button.name.StartsWith("SkillNode_") && panel.GetComponent<GridFieldGeneratorAdapter>().Skills.CanPurchase(button.name.Substring("SkillNode_".Length)) == SkillPurchaseResultApplication.PrerequisiteMissing;
            Require(button.colors.normalColor == (locked ? new Color(.18f, .18f, .18f) : button == hud.Tabs[0] ? hud.SelectedTabColor : hud.IdleTabColor) &&
                button.colors.selectedColor == (locked ? new Color(.18f, .18f, .18f) : hud.SelectedTabColor), "buttons share palette with locked-skill gray exception");
        }
        foreach (var cell in hud.GetComponentsInChildren<CellSelectButtonUIAdapter>(true))
            Require(cell.GetComponent<Image>().color == hud.IdleTabColor, "placement and submenu outer backgrounds share normal color");
        panel.ShowPage(1);
        Require(hud.Tabs[1].colors.normalColor == hud.SelectedTabColor && hud.Tabs[0].colors.normalColor == hud.IdleTabColor, "page selection shares palette");
        panel.RecipeView.Render(hud.Recipes, hud.Resources);
        Require(hud.ResourceRow.GetComponentInChildren<Image>(true).color == Color.white, "resource icon color preserved on list refresh");
        panel.ShowPage(0);
        var rotate = (RectTransform)bar.RotateButton.transform;
        Require(Mathf.Abs(rotate.offsetMin.x - 14) < .01f, "edited operation position retained");
        var fixedObjects = new[] { hud.Close.gameObject, hud.Navigation.gameObject, hud.Body.gameObject,
            hud.Tabs[0].gameObject, hud.Tabs[1].gameObject, hud.Tabs[2].gameObject, bar.RotateButton.gameObject, bar.ViewButton.gameObject };
        var ids = fixedObjects.Select(o => o.GetInstanceID()).ToArray();
        var generator = panel.GetComponent<GridFieldGeneratorAdapter>();
        panel.Configure(generator.Wallet, generator.Skills,
            TestScenes.Find<CellStatusViewAdapter>(), TestScenes.Find<CellPlacerAdapter>(), generator.Deliveries);
        Require(ids.SequenceEqual(fixedObjects.Select(o => o.GetInstanceID())), "reconfigure preserves static instances");
        Require(TestScenes.All<HudLayoutAdapter>(FindObjectsInactive.Include).Length == 1,
            "no replacement HUD or hidden restore tree");
        // この試験用の編集だけを戻し、既存の配置条件と最終画像を検証する。
        header.anchoredPosition -= new Vector2(3, 0);
        typeof(HudLayoutAdapter).GetField("_headerPosition", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            .SetValue(hud, header.anchoredPosition);
        hud.Close.GetComponentInChildren<Text>().fontSize = 26;
        hud.SelectedTabColor = new Color(.24f, .43f, .53f); hud.IdleTabColor = new Color(.14f, .21f, .27f);
        hud.ApplyColors(); hud.PlacementBuilder.Configure(hud.Font);
        foreach (var cell in hud.GetComponentsInChildren<CellSelectButtonUIAdapter>(true)) cell.SetColor(hud.IdleTabColor);
        bar.Configure(TestScenes.Find<PlayerCursorBehaviourAdapter>(), TestScenes.Find<CameraControllerAdapter>());
        panel.ShowPage(0);
        rotate.anchoredPosition -= new Vector2(2, 0);
        Debug.Log("HUD prefab runtime edit retention and reconfigure checks passed.");
    }
}

#endif

using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>保存済みHUDの参照と編集保持を既存のUI回帰へ追加する。</summary>
public static class HudPrefabVerifierEditor
{
    private static void Require(bool value, string message)
    { if (!value) throw new InvalidOperationException("HUD prefab: " + message); }
    public static void Prepare()
    {
        var hud = UnityEngine.Object.FindAnyObjectByType<HudLayoutAdapter>(FindObjectsInactive.Include);
        var bar = UnityEngine.Object.FindAnyObjectByType<OperationBarAdapter>(FindObjectsInactive.Include);
        Require(hud != null && bar != null, "scene contains both saved HUD roots");
        Require(PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(hud) == "Assets/Prefabs/UI/FactoryHud.prefab", "scene uses HUD prefab");
        Require(PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(bar) == "Assets/Prefabs/UI/OperationBar.prefab", "scene uses operation prefab");
        Require(UnityEngine.Object.FindObjectsByType<SelectButtonBuilderAdapter>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length == 1, "one placement builder");
        Require(UnityEngine.Object.FindObjectsByType<CellStatusViewAdapter>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length == 1, "one cell status view");
        foreach (var root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
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
        hud.SelectedTabColor = new Color(.3f, .5f, .6f);
        hud.IdleTabColor = new Color(.12f, .22f, .32f);
        ((RectTransform)bar.RotateButton.transform).anchoredPosition += new Vector2(2, 0);
        foreach (var edited in new Component[] { (RectTransform)hud.Close.transform, hud.Close.GetComponentInChildren<Text>(),
            hud, (RectTransform)bar.RotateButton.transform })
        {
            EditorUtility.SetDirty(edited);
            PrefabUtility.RecordPrefabInstancePropertyModifications(edited);
        }
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        SessionState.SetBool("UIS06.Edits", true);
        Debug.Log("HUD prefab serialized references and Editor font checks passed.");
    }
    private static void VerifyReferences(Component component)
    {
        var data = new SerializedObject(component); var property = data.GetIterator();
        while (property.NextVisible(true))
            if (property.propertyType == SerializedPropertyType.ObjectReference)
                Require(property.objectReferenceValue != null || property.objectReferenceInstanceIDValue == 0,
                    "resolved reference " + component.name + "." + property.propertyPath);
    }
    public static void VerifyRuntime(EconomyPanelAdapter panel)
    {
        Require(SessionState.GetBool("UIS06.Edits", false), "Editor edits prepared");
        SessionState.EraseBool("UIS06.Edits");
        var hud = panel.GetComponentInChildren<HudLayoutAdapter>();
        var bar = UnityEngine.Object.FindAnyObjectByType<OperationBarAdapter>();
        var header = (RectTransform)hud.Close.transform;
        Require(Mathf.Abs(header.anchoredPosition.x - 103) < .01f, "edited fixed frame position retained");
        Require(hud.Close.GetComponentInChildren<Text>().fontSize == 19, "edited font size retained");
        Require(hud.SelectedTabColor == new Color(.3f, .5f, .6f) && hud.IdleTabColor == new Color(.12f, .22f, .32f), "edited shared palette retained");
        Require(hud.Summary.GetComponent<Image>().color == hud.IdleTabColor && hud.Body.GetComponent<Image>().color == hud.IdleTabColor &&
            hud.GetComponentInChildren<CellStatusViewAdapter>(true).StatusScroll.GetComponent<Image>().color == hud.IdleTabColor &&
            bar.Bar.GetComponent<Image>().color == hud.IdleTabColor, "fixed panels and separate operation bar share normal color");
        foreach (var button in hud.GetComponentsInChildren<Button>(true).Concat(bar.GetComponentsInChildren<Button>(true)))
        {
            Require(button.targetGraphic.color == Color.white, "no double color multiplication");
            Require(button.colors.normalColor == (button == hud.Tabs[0] ? hud.SelectedTabColor : hud.IdleTabColor) &&
                button.colors.selectedColor == hud.SelectedTabColor, "static and generated buttons share both colors");
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
        panel.Configure(generator.Wallet, generator.Skills, generator.Upgrades,
            UnityEngine.Object.FindAnyObjectByType<CellStatusViewAdapter>(), UnityEngine.Object.FindAnyObjectByType<CellPlacerAdapter>());
        Require(ids.SequenceEqual(fixedObjects.Select(o => o.GetInstanceID())), "reconfigure preserves static instances");
        Require(UnityEngine.Object.FindObjectsByType<HudLayoutAdapter>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length == 1,
            "no replacement HUD or hidden restore tree");
        // この試験用の編集だけを戻し、既存の配置条件と最終画像を検証する。
        header.anchoredPosition -= new Vector2(3, 0);
        typeof(HudLayoutAdapter).GetField("_headerPosition", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            .SetValue(hud, header.anchoredPosition);
        hud.Close.GetComponentInChildren<Text>().fontSize = 18;
        hud.SelectedTabColor = new Color(.24f, .43f, .53f); hud.IdleTabColor = new Color(.14f, .21f, .27f);
        hud.ApplyColors(); hud.PlacementBuilder.Configure(hud.Font);
        foreach (var cell in hud.GetComponentsInChildren<CellSelectButtonUIAdapter>(true)) cell.SetColor(hud.IdleTabColor);
        bar.Configure(UnityEngine.Object.FindAnyObjectByType<PlayerCursorBehaviourAdapter>(), UnityEngine.Object.FindAnyObjectByType<CameraControllerAdapter>());
        panel.ShowPage(0);
        rotate.anchoredPosition -= new Vector2(2, 0);
        Debug.Log("HUD prefab runtime edit retention and reconfigure checks passed.");
    }
}

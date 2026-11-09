using System;
using System.Linq;
using System.Reflection;
using Cysharp.Threading.Tasks;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[InitializeOnLoad]
public static class EconomyUIVerifierEditor
{
    private const string Key = "FactoryGame.EconomyUIVerification";
    private static int _checks;
    static EconomyUIVerifierEditor() => EditorApplication.playModeStateChanged += state =>
    {
        if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Key, false)) return;
        SessionState.EraseBool(Key);
        Verify().Forget();
    };
    public static void Run()
    {
        // 保存済みのSceneとHUDアセットを検証対象にする。
        AssetDatabase.ImportAsset("Assets/Prefabs/UI", ImportAssetOptions.ImportRecursive | ImportAssetOptions.ForceUpdate);
        AssetDatabase.ImportAsset("Assets/Scenes/MainScene.unity", ImportAssetOptions.ForceUpdate);
        EditorSceneManager.OpenScene("Assets/Scenes/MainScene.unity");
        HudPrefabVerifierEditor.Prepare();
        EditorApplication.delayCall += StartPreparedPlayMode;
    }
    private static void StartPreparedPlayMode()
    {
        try { HudPrefabVerifierEditor.VerifyEditModeColors(); }
        catch (Exception exception) { Debug.LogException(exception); EditorApplication.Exit(1); return; }
        EditorUtility.audioMasterMute = true;
        Require(EditorUtility.audioMasterMute, "mute before play");
        SessionState.SetBool(Key, true);
        EditorApplication.isPlaying = true;
    }
    public static void RunRendered()
    {
        SessionState.SetBool(Key + ".Render", true);
        Run();
    }
    public static void RunLegacyUI()
    {
        EditorUtility.audioMasterMute = true;
        UIStatusVerifierEditor.Run();
    }
    public static void RunPlacement()
    {
        EditorUtility.audioMasterMute = true;
        EquipmentPlacementVerifierEditor.Run();
    }
    private static async UniTaskVoid Verify()
    {
        try
        {
            await UniTask.DelayFrame(3);
            var generator = UnityEngine.Object.FindAnyObjectByType<GridFieldGeneratorAdapter>();
            var wallet = generator.Wallet;
            var panel = generator.GetComponent<EconomyPanelAdapter>();
            var view = UnityEngine.Object.FindAnyObjectByType<CellStatusViewAdapter>();
            await WaitForSlide(panel);
            HudPrefabVerifierEditor.VerifyRuntime(panel);
            var grid = GridFieldDatabaseAdapter.Instance;
            Require(panel.IsSubscribed && panel.BalanceText.Contains("残高 100"), "initial shared balance");
            Require(!panel.UpgradeInteractable, "no selected equipment");
            wallet.TryReserveExpense(90, out var expense);
            Require(panel.BalanceText.Contains("支出予約 90") && panel.BalanceText.Contains("使用可能 10"), "expense and available balance");
            expense.TryCancel();
            Require(panel.BalanceText.Contains("支出予約 0"), "cancel refresh");
            var singularity = (SingularityCellAdapter)grid.GetCell(50, 50);
            view.UpdateUIStatusWindow(singularity);
            Require(singularity.GetDataProvider().ReadData().Count == 4, "custom singularity provider");
            Require(singularity.AllocateStorage(Vector3Int.left, 7, ResourceTypeDomain.Wood), "income reserve");
            Require(panel.BalanceText.Contains("換金待ち 7"), "pending income");
            singularity.CancelStorage(Vector3Int.left, 7, ResourceTypeDomain.Wood);
            Require(!panel.FeedbackText.Contains("+7"), "cancel is not conversion");
            singularity.AllocateStorage(Vector3Int.left, 7, ResourceTypeDomain.Wood);
            singularity.StoreResource(Vector3Int.left, 7);
            Require(panel.FeedbackText == "換金 +7" && panel.BalanceText.Contains("残高 107"), "conversion notification");
            var snapshot = singularity.GetDataProvider().ReadData();
            Require(((TextElementDataApplication)snapshot[LabelEnumApplication.Amount]).Text == "107", "custom reader current balance");
            var storage = Install(grid, "Storage", 20, 20);
            view.UpdateUIStatusWindow(storage);
            Require(panel.UpgradeInteractable && panel.EquipmentText.Contains("強化費 15") && panel.EquipmentText.Contains("Lv.1/3"), "upgrade quote");
            Require(Subscribers(singularity, "StatusChanged") == 0, "selection releases singularity UI subscriptions");
            Require(panel.PurchaseUpgrade() == EquipmentUpgradeResultApplication.Success && storage.EquipmentLevel == 2 &&
                panel.EquipmentText.Contains("強化費 30") && panel.EquipmentText.Contains("Lv.2/3"), "upgrade updates level and next cost");
            Require(panel.PurchaseUpgrade() == EquipmentUpgradeResultApplication.Success && !panel.UpgradeInteractable &&
                panel.EquipmentText.Contains("最大レベル"), "maximum disables action");
            var conveyor = Install(grid, "Conveyor", 21, 20);
            view.UpdateUIStatusWindow(conveyor);
            Require(ReferenceEquals(view.SelectedCell, conveyor) && panel.UpgradeInteractable && panel.EquipmentText.Contains("搬送量"), "equipment without legacy provider is selectable");
            Require(Subscribers(storage, "StatusChanged") == 0, "switch releases storage subscriptions");
            conveyor.gameObject.SetActive(false);
            await UniTask.DelayFrame(2);
            Require(ReferenceEquals(view.SelectedCell, conveyor) && !panel.UpgradeInteractable, "disabled equipment keeps selection and rejects purchase");
            conveyor.gameObject.SetActive(true);
            await UniTask.DelayFrame(2);
            Require(panel.UpgradeInteractable, "reactivated equipment refreshes purchase state");
            var root = generator.Skills.Catalog.Nodes.Single(n => n.Prerequisite == null);
            var branch = generator.Skills.Catalog.Nodes.First(n => n.Prerequisite == root.Uid);
            var beforeSkill = wallet.Current.Balance;
            panel.SkillButton(branch.Uid).onClick.Invoke();
            Require(panel.SelectedSkill == branch.Uid && !panel.UpgradeInteractable && panel.EquipmentView.Feedback.Contains(root.Label), "locked node opens prerequisite detail");
            Require(view.SelectedCell == null && view.StatusContent.GetComponentsInChildren<HudStatusRowAdapter>().Length == 0 && wallet.Current.Balance == beforeSkill, "skill selection releases equipment without purchase");
            panel.SkillButton(root.Uid).onClick.Invoke();
            Require(!generator.Skills.IsOwned(root.Uid) && wallet.Current.Balance == beforeSkill, "node selection never purchases");
            wallet.TryReserveExpense(wallet.Current.AvailableBalance, out expense);
            Require(!panel.UpgradeInteractable && panel.EquipmentView.Feedback.Contains("残高不足") && panel.SkillButton(root.Uid).interactable, "unaffordable node remains readable and apply disabled");
            expense.TryCancel();
            wallet.TryReserveIncome(1000, out var income); income.TryCommit();
            Require(panel.UpgradeInteractable, "balance change enables selected skill action");
            panel.EquipmentView.UpgradeButton.onClick.Invoke();
            Require(generator.Skills.IsOwned(root.Uid) && !panel.UpgradeInteractable && panel.SkillButton(root.Uid).transform.Find("Applied").gameObject.activeSelf, "detail action applies root and marks owned node");
            var paidBalance = wallet.Current.Balance;
            panel.EquipmentView.UpgradeButton.onClick.Invoke();
            Require(wallet.Current.Balance == paidBalance, "repeat apply never pays twice");
            panel.SkillButton(branch.Uid).onClick.Invoke();
            Require(panel.UpgradeInteractable && !generator.Skills.IsOwned(branch.Uid), "unlocked branch waits for detail action");
            panel.EquipmentView.UpgradeButton.onClick.Invoke();
            Require(generator.Skills.IsOwned(branch.Uid) && panel.EquipmentView.Heading.text == branch.Label, "branch applies from right details");
            view.CloseButton.onClick.Invoke();
            Require(panel.SelectedSkill == null, "right close clears skill selection");
            view.UpdateUIStatusWindow(storage);
            view.SetStatusWindowActive(false);
            Require(Subscribers(storage, "StatusChanged") == 0 && !panel.UpgradeInteractable, "hide selection releases subscriptions");
            view.UpdateUIStatusWindow(storage);
            panel.SetExpanded(false);
            Require(panel.IsSubscribed && Subscribers(wallet, "Changed") == 2, "collapsed left page keeps persistent balance subscription");
            var hiddenText = panel.BalanceText;
            wallet.TryReserveIncome(1, out income); income.TryCommit();
            Require(panel.BalanceText != hiddenText, "closed page still updates persistent balance");
            panel.SetExpanded(true);
            Require(panel.IsSubscribed && panel.EquipmentText.Contains("最大レベル") && panel.BalanceText.Contains($"残高 {wallet.Current.Balance}"), "reopen reads latest selected state");
            panel.enabled = false;
            Require(!panel.IsSubscribed, "disabled panel unsubscribes");
            panel.enabled = true;
            view.UpdateUIStatusWindow(conveyor);
            UnityEngine.Object.Destroy(conveyor.gameObject);
            await UniTask.DelayFrame(2);
            Require(view.SelectedCell == null && !panel.UpgradeInteractable, "deleted equipment closes selection");
            await UniTask.Yield();
            Canvas.ForceUpdateCanvases();
            var canvas = panel.GetComponentInChildren<Canvas>();
            var buttons = canvas.GetComponentsInChildren<Button>(true);
            Require(buttons.Length >= generator.Skills.Catalog.Nodes.Count + 5, "placement, navigation, skill and upgrade buttons exist");
            Require(canvas.GetComponentsInChildren<Text>(true).First().font.HasCharacter('換'), "Japanese glyph available");
            var raycaster = UnityEngine.Object.FindAnyObjectByType<UIRaycasterAdapter>();
            var header = buttons.First(b => b.GetComponentInChildren<Text>(true).text.Contains("閉じる"));
            var point = RectTransformUtility.WorldToScreenPoint(null, header.transform.position + new Vector3(20, -15));
            Require(raycaster.IsPointerOverUI(point), "integrated canvas blocks field input");
            var placer = UnityEngine.Object.FindAnyObjectByType<CellPlacerAdapter>();
            var before = wallet.Current.Balance;
            var storageButton = canvas.GetComponentsInChildren<CellSelectButtonUIAdapter>(true).Single(b => b.name == "StorageButton");
            storageButton.GetComponentInChildren<Button>(true).onClick.Invoke();
            await UniTask.DelayFrame(2);
            Require(panel.PlacementText.Contains("保管庫 単価 15"), "existing placement button updates quote");
            var submenu = canvas.GetComponentsInChildren<CellSelectButtonUIAdapter>(true).Single(b => b.name == "ExtractorWoodSubMenuButton");
            submenu.GetComponentInChildren<Button>(true).onClick.Invoke();
            var choice = canvas.GetComponentsInChildren<CellSelectButtonUIAdapter>(true).Single(b => b.name == "ExtractorStoneChoice");
            Require(choice.gameObject.activeInHierarchy, "existing submenu opens choices");
            choice.GetComponentInChildren<Button>(true).onClick.Invoke();
            await UniTask.DelayFrame(2);
            Require(panel.PlacementText.Contains("石材採取 単価"), "submenu selects placement type");
            Require(wallet.Current.Balance == before, "selection and menu changes do not pay");
            VerifyInputCancellation(placer, raycaster, point, wallet, grid);
            VerifyOperationBar(placer, raycaster, wallet, grid);
            await VerifySlide(panel, raycaster);
            for (var page = 0; page < 3; page++)
            {
                var subscribers = Subscribers(wallet, "Changed");
                panel.PageButton(page).onClick.Invoke();
                Require(panel.SelectedPage == page && Subscribers(wallet, "Changed") == subscribers, "page switch keeps one subscription");
            }
            // CanvasScalerの高さ基準720に合わせ、指定解像度の論理幅で実UIを検査する。
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.GetComponent<CanvasScaler>().enabled = false;
            foreach (var resolution in new[] { new Vector2(1600, 900), new Vector2(1366, 768) })
            {
                canvas.GetComponent<RectTransform>().sizeDelta = new Vector2(resolution.x * 720 / resolution.y, 720);
                for (var page = 0; page < 3; page++)
                {
                    panel.ShowPage(page);
                    await UniTask.DelayFrame(4);
                    Canvas.ForceUpdateCanvases();
                    VerifyLayout(canvas, resolution, page);
                    if (page == 2)
                    {
                        var tree = panel.SkillButton(root.Uid).transform.parent;
                        Require(tree.GetComponentsInChildren<Image>().Count(image => image.name.StartsWith("Prerequisite_")) == generator.Skills.Catalog.Nodes.Count - 1, "tree connects every prerequisite " + resolution);
                        foreach (var node in generator.Skills.Catalog.Nodes.Where(node => node.Prerequisite != null))
                        {
                            var childRect = (RectTransform)panel.SkillButton(node.Uid).transform;
                            var parentRect = (RectTransform)panel.SkillButton(node.Prerequisite).transform;
                            Require(childRect.anchoredPosition.y < parentRect.anchoredPosition.y - parentRect.rect.height, "tree child below parent " + node.Uid + resolution);
                        }
                        panel.SelectSkill(branch.Uid);
                        await UniTask.DelayFrame(4);
                        Canvas.ForceUpdateCanvases();
                        Require(tree.Find("SelectionFrame").gameObject.activeSelf && panel.EquipmentView.Heading.text == branch.Label, "selection matches right detail " + resolution);
                        Require(Inside(canvas.GetComponent<RectTransform>(), panel.EquipmentView.UpgradeButton.GetComponent<RectTransform>(), view.StatusScroll.viewport), "skill apply fits right viewport " + resolution);
                        view.CloseButton.onClick.Invoke();
                        var scroll = canvas.GetComponentInChildren<ScrollRect>();
                        scroll.verticalNormalizedPosition = 0;
                        await UniTask.DelayFrame(2);
                        Canvas.ForceUpdateCanvases();
                        var last = panel.SkillButton(generator.Skills.Catalog.Nodes.Last().Uid).GetComponent<RectTransform>();
                        var lastBounds = BoundsIn(canvas.GetComponent<RectTransform>(), last);
                        var viewportBounds = BoundsIn(canvas.GetComponent<RectTransform>(), scroll.viewport);
                        Require(lastBounds.yMin >= viewportBounds.yMin - 1 && lastBounds.yMax <= viewportBounds.yMax + 1,
                            $"{resolution}: final skill reachable by scrolling");
                    }
                }
            }
            await VerifySideTabs(panel, view, canvas, wallet, grid);
            if (SessionState.GetBool(Key + ".Render", false))
            {
                SessionState.EraseBool(Key + ".Render");
                await CaptureHud(panel, view, canvas);
                panel.SelectSkill(branch.Uid);
                await CaptureHud(panel, view, canvas, 2, "skill-tree");
            }
            Require(wallet.LastNotificationException == null && generator.Skills.LastNotificationException == null, "notification handlers succeed");
            UnityEngine.Object.Destroy(panel);
            await UniTask.DelayFrame(2);
            Require(canvas != null && !canvas.gameObject.activeSelf && view != null &&
                Subscribers(wallet, "Changed") == 1 && Subscribers(view, "SelectionChanged") == 0,
                "destroying controller releases subscriptions and retains authored HUD inactive");
            Require(view.SelectedCell == null && view.StatusContent.GetComponentsInChildren<HudStatusRowAdapter>().Length == 0,
                "controller destruction releases selection and active rows without legacy restoration");
            var authored = canvas.GetComponent<HudLayoutAdapter>();
            var pageStates = authored.Pages.Select(page => page.gameObject.activeSelf).ToArray();
            var bodyActive = authored.Body.gameObject.activeSelf;
            foreach (var tab in authored.Tabs) tab.onClick.Invoke();
            authored.Close.onClick.Invoke();
            Require(pageStates.SequenceEqual(authored.Pages.Select(page => page.gameObject.activeSelf)) &&
                bodyActive == authored.Body.gameObject.activeSelf, "destroyed controller removes static button listeners");
            Debug.Log($"Economy UI checks passed: {_checks} assertions; MainScene balance, conversion, quotes, buttons, prerequisites and subscription lifetime.");
            EditorApplication.Exit(0);
        }
        catch (Exception exception) { Debug.LogException(exception); EditorApplication.Exit(1); }
    }
    private static async UniTask WaitForSlide(EconomyPanelAdapter panel)
    {
        var hud = panel.GetComponentInChildren<HudLayoutAdapter>(true);
        var deadline = Time.realtimeSinceStartup + 2;
        while (hud.IsSliding && Time.realtimeSinceStartup < deadline) await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate);
        Require(!hud.IsSliding, "side panel slide completes within deadline");
    }
    private static async UniTask VerifySlide(EconomyPanelAdapter panel, UIRaycasterAdapter raycaster)
    {
        var hud = panel.GetComponentInChildren<HudLayoutAdapter>(true);
        await WaitForSlide(panel);
        var body = (RectTransform)hud.Body.transform;
        var open = body.anchoredPosition;
        var page = panel.SelectedPage;
        var group = body.GetComponent<CanvasGroup>();
        var timeScale = Time.timeScale;
        var point = RectTransformUtility.WorldToScreenPoint(null, body.TransformPoint(body.rect.center));
        try
        {
            Time.timeScale = 0;
            EventSystem.current.SetSelectedGameObject(hud.Tabs[page].gameObject);
            var started = Time.unscaledTime;
            panel.SetExpanded(false);
            Require(hud.Tabs.All(tab => tab.colors.normalColor == hud.IdleTabColor) && EventSystem.current.currentSelectedGameObject == null,
                "closing clears active tab color and selection focus");
            Require(body.gameObject.activeSelf && group.blocksRaycasts && !group.interactable, "closing keeps visible frame and blocks purchases");
            do { await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate); }
            while (hud.IsSliding && Time.unscaledTime - started < .08f);
            Require(body.anchoredPosition.x < open.x, "closing slides left instead of disappearing");
            var current = body.anchoredPosition.x;
            var sampledTime = Time.unscaledTime - started;
            panel.SetExpanded(true);
            Require(body.anchoredPosition.x == current, "reversal starts from current position");
            await WaitForSlide(panel);
            Require(Vector2.Distance(body.anchoredPosition, open) < .01f && group.interactable, "opening returns to authored frame while paused");
            panel.SetExpanded(false);
            await WaitForSlide(panel);
            var canvas = (RectTransform)hud.transform;
            Require(canvas.InverseTransformPoint(body.TransformPoint(new Vector3(body.rect.xMax, 0))).x < canvas.rect.xMin,
                "closed frame is entirely outside left edge");
            if (sampledTime < .16f)
                Require((open.x - current) / (open.x - body.anchoredPosition.x) > sampledTime / .2f + .1f,
                    "Ease Out Cubic advances faster than a linear slide before decelerating");
            Require(!body.gameObject.activeSelf && !group.blocksRaycasts && !raycaster.IsPointerOverUI(point), "closed frame releases field input");
            Require(hud.Navigation.gameObject.activeInHierarchy && hud.Tabs[0].IsInteractable(), "tabs remain available outside moving frame");
            panel.ShowPage(page);
            await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate);
            panel.SetExpanded(false);
            panel.enabled = false;
            await UniTask.DelayFrame(2);
            Require(!hud.gameObject.activeSelf, "disabled controller cannot leave an animated frame visible");
            panel.enabled = true;
            await WaitForSlide(panel);
            panel.ShowPage(page);
            await WaitForSlide(panel);
            Require(panel.SelectedPage == page && Vector2.Distance(body.anchoredPosition, open) < .01f, "enable and reopen retain page and saved position");
            Debug.Log("HUD slide checks passed: movement, reversal, paused time, input, persistent tabs and disable/reopen.");
        }
        finally { Time.timeScale = timeScale; }
    }
    private static async UniTask VerifySideTabs(EconomyPanelAdapter panel, CellStatusViewAdapter view,
        Canvas canvas, CurrencyWalletApplication wallet, GridFieldDatabaseAdapter grid)
    {
        var cursor = UnityEngine.Object.FindAnyObjectByType<PlayerCursorBehaviourAdapter>();
        var placer = UnityEngine.Object.FindAnyObjectByType<CellPlacerAdapter>();
        var raycaster = UnityEngine.Object.FindAnyObjectByType<UIRaycasterAdapter>();
        placer.SetSelectedCellType(CellTypeDomain.Crafter);
        cursor.RotatePlacement();
        var direction = cursor.transform.rotation;
        var storage = (StorageCellAdapter)Install(grid, "Storage", 25, 25);
        Require(storage.AllocateStorage(Vector3Int.left, 3, ResourceTypeDomain.Wood), "prepare selected inventory");
        storage.StoreResource(Vector3Int.left, 3);
        view.UpdateUIStatusWindow(storage);
        var balance = wallet.Current.Balance;
        var capacity = storage.Capacity;
        Require(panel.EquipmentText.Contains("強化後: 容量 " + storage.CapacityAtLevel(2)), "preview uses same next-level stat rule");
        for (var page = 0; page < 3; page++)
        {
            panel.SetExpanded(false);
            panel.PageButton(page).onClick.Invoke();
            Require(panel.IsExpanded && panel.SelectedPage == page, "side tab opens target directly");
            panel.PageButton(page).onClick.Invoke();
            Require(!panel.IsExpanded && panel.SelectedPage == page, "same side tab closes and retains page");
            panel.SetExpanded(true);
            Require(view.SelectedCell == storage && placer.SelectedType == CellTypeDomain.Crafter &&
                Quaternion.Angle(direction, cursor.transform.rotation) < 0.01f, "tabs preserve equipment selection and placement direction");
        }
        Require(storage.EquipmentLevel == 1 && storage.CurrentLoad == 3 && storage.Capacity == capacity && wallet.Current.Balance == balance,
            "preview and navigation preserve level inventory and money");
        panel.EquipmentView.UpgradeButton.onClick.Invoke();
        Require(storage.EquipmentLevel == 2 && storage.Capacity == storage.CapacityAtLevel(2) && storage.CurrentLoad == 3,
            "right-hand button upgrades same selected cell and preserves inventory");
        var recipes = AssetDatabase.LoadAssetAtPath<RecipeDatabaseDefinition>("Assets/Scripts/ScriptableObject/Recipes/RecipeDB.asset");
        var resources = AssetDatabase.LoadAssetAtPath<ResourceDefinition>("Assets/Scripts/ScriptableObject/ResourceDB.asset");
        panel.ShowPage(1);
        var originalLabels = panel.RecipeContent.GetComponentsInChildren<Text>(true).Select(t => t.text).ToArray();
        foreach (var recipe in recipes.recipes)
        {
            Require(originalLabels.Any(t => t.Contains(recipe.RecipeName) && t.Contains($"{recipe.CraftSecond} 秒")), "recipe name and craft time from definition");
            foreach (var ingredient in recipe.Ingredients)
                Require(originalLabels.Contains($"材料: {resources.GetResourceByType(ingredient.resourceType).ResourceName} ×{ingredient.requiredAmount}"), "ingredient name and required amount");
            Require(originalLabels.Contains($"成果物: {resources.GetResourceByType(recipe.Result).ResourceName} ×{recipe.ResultAmount}"), "output name and quantity");
        }
        var expanded = ScriptableObject.CreateInstance<RecipeDatabaseDefinition>();
        expanded.recipes = Enumerable.Range(0, 24).Select(i => JsonUtility.FromJson<RecipeDataDefinition>(
            "{\"recipeName\":\"収容確認" + i + "\",\"ingredients\":[{\"resourceType\":2,\"requiredAmount\":3},{\"resourceType\":1,\"requiredAmount\":4},{\"resourceType\":2,\"requiredAmount\":5},{\"resourceType\":1,\"requiredAmount\":6},{\"resourceType\":2,\"requiredAmount\":7},{\"resourceType\":1,\"requiredAmount\":8}],\"craftSecond\":12.5,\"result\":2,\"resultAmount\":9}"
        )).ToArray();
        panel.RecipeView.Render(expanded, resources);
        var extras = Enumerable.Range(0, 12).Select(i => "追加項目 " + i + "：12345").ToArray();
        panel.SetSummaryItems(extras);
        var root = canvas.GetComponent<RectTransform>();
        foreach (var resolution in new[] { new Vector2(1600, 900), new Vector2(1366, 768) })
        {
            root.sizeDelta = new Vector2(resolution.x * 720 / resolution.y, 720);
            panel.ShowPage(1);
            view.UpdateUIStatusWindow(storage);
            await UniTask.DelayFrame(5);
            Canvas.ForceUpdateCanvases();
            VerifyLayout(canvas, resolution, 1);
            var summaryBounds = BoundsIn(root, panel.SummaryRect);
            var leftScroll = panel.RecipeContent.GetComponentInParent<ScrollRect>();
            var leftBounds = BoundsIn(root, leftScroll.GetComponent<RectTransform>());
            var rightBounds = BoundsIn(root, (RectTransform)view.transform);
            Require(summaryBounds.yMin >= leftBounds.yMax + 7 && summaryBounds.yMin >= rightBounds.yMax + 7,
                "expanded summary clears both side panels " + resolution);
            Require(leftBounds.xMax < rightBounds.xMin, "left recipe and right cell information do not overlap " + resolution);
            Require(panel.EquipmentView.Heading.transform.GetSiblingIndex() == 0 &&
                panel.EquipmentView.UpgradeRoot.GetSiblingIndex() == view.StatusContent.childCount - 1,
                "cell heading then inventory then upgrade");
            foreach (var scroll in new[] { leftScroll, view.StatusScroll })
            {
                scroll.verticalNormalizedPosition = 0;
                await UniTask.DelayFrame(3);
                Canvas.ForceUpdateCanvases();
            }
            var lastRecipe = (RectTransform)panel.RecipeContent.GetChild(panel.RecipeContent.childCount - 1);
            Require(Inside(root, lastRecipe, leftScroll.viewport), "last of 24 recipes with six ingredients reachable " + resolution);
            VerifyFixedSummary(panel, root, resolution);
            var fixedPositions = panel.SummaryView.WalletItems.Concat(panel.SummaryView.Items).Select(t => t.rectTransform.anchoredPosition).ToArray();
            ExecuteEvents.ExecuteHierarchy(panel.SummaryRect.gameObject, new PointerEventData(EventSystem.current) { scrollDelta = new Vector2(0, -12) }, ExecuteEvents.scrollHandler);
            await UniTask.DelayFrame(2);
            VerifyFixedSummary(panel, root, resolution);
            Require(fixedPositions.SequenceEqual(panel.SummaryView.WalletItems.Concat(panel.SummaryView.Items).Select(t => t.rectTransform.anchoredPosition)),
                "wheel input does not move persistent summary " + resolution);
            Require(Inside(root, panel.EquipmentView.UpgradeButton.GetComponent<RectTransform>(), view.StatusScroll.viewport), "upgrade button reachable " + resolution);
            foreach (var text in panel.RecipeContent.GetComponentsInChildren<Text>())
                Require(text.preferredHeight <= text.rectTransform.rect.height + 1, "expanded recipe text height " + resolution);
            foreach (var icon in panel.RecipeContent.GetComponentsInChildren<Image>())
                Require(icon.sprite != null && icon.rectTransform.rect.width >= 31 && icon.rectTransform.rect.height >= 31, "resource icon retained " + resolution);
        }
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.GetComponent<CanvasScaler>().enabled = true;
        panel.SummaryView.SetWalletOrder(PersistentSummaryViewAdapter.WalletField.Income, PersistentSummaryViewAdapter.WalletField.Expense,
            PersistentSummaryViewAdapter.WalletField.Available, PersistentSummaryViewAdapter.WalletField.Balance);
        Require(panel.BalanceText.StartsWith("換金待ち") && panel.BalanceText.EndsWith($"残高 {wallet.Current.Balance}"), "currency field order can change");
        panel.SummaryView.SetWalletOrder(PersistentSummaryViewAdapter.WalletField.Balance, PersistentSummaryViewAdapter.WalletField.Available,
            PersistentSummaryViewAdapter.WalletField.Expense, PersistentSummaryViewAdapter.WalletField.Income);
        panel.SetSummaryItems(extras.Reverse().ToArray());
        Require(panel.SummaryView.Items.First().text == extras.Last(), "summary order follows caller order");
        panel.SetSummaryItems();
        panel.RecipeView.Render(recipes, resources);
        UnityEngine.Object.Destroy(expanded);
        panel.ShowPage(1);
        view.UpdateUIStatusWindow(storage);
        await UniTask.DelayFrame(5);
        Canvas.ForceUpdateCanvases();
        view.StatusScroll.verticalNormalizedPosition = 0;
        await UniTask.DelayFrame(2);
        foreach (var target in new[] { panel.PageButton(1).GetComponent<RectTransform>(), panel.SummaryRect,
            panel.EquipmentView.UpgradeButton.GetComponent<RectTransform>() })
        {
            var point = RectTransformUtility.WorldToScreenPoint(null, target.TransformPoint(target.rect.center));
            Require(raycaster.IsPointerOverUI(point), "new HUD controls intercept field input");
            VerifyInputCancellation(placer, raycaster, point, wallet, grid);
        }
        var singularity = (SingularityCellAdapter)grid.GetCell(50, 50);
        view.UpdateUIStatusWindow(singularity);
        Require(!panel.EquipmentView.UpgradeRoot.gameObject.activeSelf && view.SelectedCell == singularity,
            "non-upgrade cell shows status without upgrade controls");
        view.CloseButton.onClick.Invoke();
        Require(view.SelectedCell == null && !panel.UpgradeInteractable, "right close button releases selection");
        var crafter = (CrafterCellAdapter)Install(grid, "Crafter", 26, 25);
        view.UpdateUIStatusWindow(crafter);
        Require(panel.EquipmentView.Heading.text.Contains("加工設備") && panel.EquipmentView.Heading.text.Contains("待機") &&
            panel.EquipmentText.Contains("強化後: 入力容量 " + crafter.IngredientCapacityAtLevel(2)), "crafter name operating state and next capacity");
    }
    private static void VerifyFixedSummary(EconomyPanelAdapter panel, RectTransform root, Vector2 resolution)
    {
        Require(panel.SummaryRect.GetComponentsInChildren<ScrollRect>(true).Length == 0 &&
            panel.SummaryRect.GetComponentsInChildren<RectMask2D>(true).Length == 0, "persistent summary has no scrolling or clipping " + resolution);
        var texts = panel.SummaryView.WalletItems.Concat(panel.SummaryView.Items).Where(t => t.gameObject.activeSelf).ToArray();
        Require(texts.Length == 16, "all four wallet and twelve extra items visible " + resolution);
        foreach (var text in texts)
        {
            Require(Inside(root, text.rectTransform, panel.SummaryRect) && Inside(root, text.rectTransform, root), "every summary item inside visible frame " + resolution);
            Require(text.preferredHeight <= text.rectTransform.rect.height + 1 && text.preferredWidth <= text.rectTransform.rect.width + 1,
                "summary item text fits " + resolution);
        }
        for (var i = 0; i < texts.Length; i++)
            for (var j = i + 1; j < texts.Length; j++)
            {
                var a = BoundsIn(root, texts[i].rectTransform); var b = BoundsIn(root, texts[j].rectTransform);
                Require(Mathf.Min(a.xMax, b.xMax) - Mathf.Max(a.xMin, b.xMin) < 1 || Mathf.Min(a.yMax, b.yMax) - Mathf.Max(a.yMin, b.yMin) < 1,
                    "summary items do not overlap " + resolution);
            }
        var bottom = BoundsIn(root, panel.SummaryRect).yMin;
        Require(bottom > root.rect.yMin + 108, "summary clears bottom operation bar " + resolution);
    }

    private static async UniTask CaptureHud(EconomyPanelAdapter panel, CellStatusViewAdapter view, Canvas canvas, int page = 1, string prefix = "hud")
    {
        panel.SetSummaryItems(Enumerable.Range(0, 12).Select(i => "追加項目 " + i + "：12345").ToArray());
        // GUI操作を使わず、実際のHUDを専用カメラから画像へ書き出す。
        var canvases = new[] { canvas, UnityEngine.Object.FindAnyObjectByType<OperationBarAdapter>().GetComponentInChildren<Canvas>() };
        var objects = canvases.SelectMany(c => c.GetComponentsInChildren<Transform>(true)).Distinct().ToArray();
        var layers = objects.Select(t => t.gameObject.layer).ToArray();
        var cameraObject = new GameObject("HudVerificationCamera", typeof(Camera));
        var camera = cameraObject.GetComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(0.15f, 0.16f, 0.17f);
        camera.cullingMask = 1 << 31; camera.orthographic = true; camera.nearClipPlane = 0.1f; camera.farClipPlane = 100;
        camera.enabled = false;
        for (var i = 0; i < objects.Length; i++) objects[i].gameObject.layer = 31;
        foreach (var c in canvases) { c.renderMode = RenderMode.ScreenSpaceCamera; c.worldCamera = camera; c.planeDistance = 10; }
        foreach (var resolution in new[] { new Vector2Int(1600, 900), new Vector2Int(1366, 768) })
        {
            var target = new RenderTexture(resolution.x, resolution.y, 24);
            camera.targetTexture = target;
            foreach (var c in canvases) c.GetComponent<CanvasScaler>().enabled = true;
            panel.ShowPage(page);
            view.StatusScroll.verticalNormalizedPosition = 1;
            await UniTask.DelayFrame(5);
            Canvas.ForceUpdateCanvases();
            foreach (var mask in canvas.GetComponentsInChildren<RectMask2D>()) mask.PerformClipping();
            camera.Render();
            foreach (var icon in panel.RecipeContent.GetComponentsInChildren<Image>())
                Require(!icon.canvasRenderer.cull, "visible recipe icon rendered " + resolution);
            var previous = RenderTexture.active;
            RenderTexture.active = target;
            var texture = new Texture2D(resolution.x, resolution.y, TextureFormat.RGB24, false);
            texture.ReadPixels(new Rect(0, 0, resolution.x, resolution.y), 0, 0); texture.Apply();
            var path = $"docs/verification/uis05/{prefix}-{resolution.x}x{resolution.y}.png";
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path));
            System.IO.File.WriteAllBytes(path, texture.EncodeToPNG());
            Require(System.IO.File.Exists(path), "HUD rendering exported " + resolution);
            RenderTexture.active = previous;
            UnityEngine.Object.Destroy(texture); camera.targetTexture = null; target.Release(); UnityEngine.Object.Destroy(target);
        }
        foreach (var c in canvases) { c.renderMode = RenderMode.ScreenSpaceOverlay; c.worldCamera = null; }
        for (var i = 0; i < objects.Length; i++) if (objects[i] != null) objects[i].gameObject.layer = layers[i];
        panel.SetSummaryItems();
        UnityEngine.Object.Destroy(cameraObject);
    }

    private static bool Inside(RectTransform root, RectTransform child, RectTransform viewport)
    {
        var a = BoundsIn(root, child); var b = BoundsIn(root, viewport);
        return a.xMin >= b.xMin - 1 && a.xMax <= b.xMax + 1 && a.yMin >= b.yMin - 1 && a.yMax <= b.yMax + 1;
    }

    private static void VerifyInputCancellation(CellPlacerAdapter placer, UIRaycasterAdapter raycaster,
        Vector2 point, CurrencyWalletApplication wallet, GridFieldDatabaseAdapter grid)
    {
        var cursor = UnityEngine.Object.FindAnyObjectByType<PlayerCursorBehaviourAdapter>();
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var cell = grid.GetCell(30, 30);
        var before = wallet.Current.Balance;
        placer.SetSelectedCellType(CellTypeDomain.Storage);
        placer.SelectGrid(cell.gameObject); placer.PointerBegin(); placer.PointerDrag();
        typeof(PlayerCursorBehaviourAdapter).GetField("_mousePosition", flags).SetValue(cursor, point);
        var callback = default(UnityEngine.InputSystem.InputAction.CallbackContext);
        typeof(PlayerCursorBehaviourAdapter).GetMethod("OnLeftClickDown", flags).Invoke(cursor, new object[] { callback });
        typeof(PlayerCursorBehaviourAdapter).GetMethod("OnLeftClickUp", flags).Invoke(cursor, new object[] { callback });
        Require(ReferenceEquals(grid.GetCell(30, 30), cell) && wallet.Current.Balance == before, "UI click does not confirm pending placement");
        typeof(PlayerCursorBehaviourAdapter).GetField("_isLeftClickPressed", flags).SetValue(cursor, true);
        typeof(PlayerCursorBehaviourAdapter).GetMethod("OnLeftClickUp", flags).Invoke(cursor, new object[] { callback });
        Require(placer.PreviewCount == 0 && wallet.Current.Balance == before && ReferenceEquals(grid.GetCell(30, 30), cell), "drag release over UI cancels without payment");
    }

    private static void VerifyOperationBar(CellPlacerAdapter placer, UIRaycasterAdapter raycaster,
        CurrencyWalletApplication wallet, GridFieldDatabaseAdapter grid)
    {
        var bar = UnityEngine.Object.FindAnyObjectByType<OperationBarAdapter>();
        var cursor = UnityEngine.Object.FindAnyObjectByType<PlayerCursorBehaviourAdapter>();
        var camera = UnityEngine.Object.FindAnyObjectByType<CameraControllerAdapter>();
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        Require(bar != null && bar.RotateButton.interactable && bar.ViewButton.interactable, "operation buttons reachable");
        var rotation = cursor.transform.rotation;
        bar.RotateButton.onClick.Invoke();
        Require(Quaternion.Angle(cursor.transform.rotation, rotation * Quaternion.Euler(0, 90, 0)) < 0.01f, "button rotates placement 90 degrees");
        for (var i = 0; i < 3; i++) bar.RotateButton.onClick.Invoke();
        Require(Quaternion.Angle(cursor.transform.rotation, rotation) < 0.01f, "four rotations restore placement direction");
        var switched = typeof(CameraControllerAdapter).GetField("_isCameraAngleSwitched", flags);
        var initial = (bool)switched.GetValue(camera);
        bar.ViewButton.onClick.Invoke();
        Require((bool)switched.GetValue(camera) != initial, "button switches view");
        bar.ViewButton.onClick.Invoke();
        Require((bool)switched.GetValue(camera) == initial, "second click restores view");
        foreach (var binding in new[] { (owner: (object)cursor, field: "rotateAction", key: "<Keyboard>/r"),
            (owner: (object)camera, field: "switchCameraAngleAction", key: "<Keyboard>/l") })
        {
            var action = (UnityEngine.InputSystem.InputAction)binding.owner.GetType().GetField(binding.field, flags).GetValue(binding.owner);
            Require(action.enabled && action.bindings.Any(b => string.Equals(b.path, binding.key, StringComparison.OrdinalIgnoreCase)), "keyboard shortcut retained: " + binding.key);
        }
        foreach (var button in new[] { bar.RotateButton, bar.ViewButton })
        {
            var position = RectTransformUtility.WorldToScreenPoint(null, button.transform.position);
            Require(raycaster.IsPointerOverUI(position), "operation button blocks field input");
            VerifyInputCancellation(placer, raycaster, position, wallet, grid);
        }
        var canvas = bar.GetComponentInChildren<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.GetComponent<CanvasScaler>().enabled = false;
        foreach (var resolution in new[] { new Vector2(1600, 900), new Vector2(1366, 768) })
        {
            var root = canvas.GetComponent<RectTransform>();
            root.sizeDelta = new Vector2(resolution.x * 720 / resolution.y, 720);
            Canvas.ForceUpdateCanvases();
            foreach (var text in canvas.GetComponentsInChildren<Text>())
            {
                Require(text.preferredHeight <= text.rectTransform.rect.height + 1, "operation text fits " + resolution);
                Require(text.preferredWidth <= text.rectTransform.rect.width + 1, "operation label width fits " + resolution);
            }
            foreach (var button in new[] { bar.RotateButton, bar.ViewButton })
            {
                var bounds = BoundsIn(root, button.GetComponent<RectTransform>());
                Require(root.rect.Contains(bounds.min) && root.rect.Contains(bounds.max), "operation button inside screen " + resolution);
            }
            Require(BoundsIn(root, bar.Bar).yMax <= root.rect.yMin + 96.1f, "bottom HUD reservation " + resolution);
            var view = UnityEngine.Object.FindAnyObjectByType<CellStatusViewAdapter>();
            var window = (RectTransform)view.transform;
            var parent = (RectTransform)window.parent;
            Require(BoundsIn(parent, window).yMin >= parent.rect.yMin + parent.rect.height * 108f / 720f - 1,
                "cell information clears operation bar");
        }
        canvas.GetComponent<CanvasScaler>().enabled = true;
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
    }

    private static void VerifyLayout(Canvas canvas, Vector2 resolution, int page)
    {
        var canvasRect = canvas.GetComponent<RectTransform>();
        var corners = new Vector3[4];
        foreach (var scroll in canvas.GetComponentsInChildren<ScrollRect>())
        {
            Require(BoundsIn(canvasRect, scroll.GetComponent<RectTransform>()).yMin >= canvasRect.rect.yMin + 108 - 1,
                "economy viewport clears operation bar");
            scroll.GetComponent<RectTransform>().GetWorldCorners(corners);
            foreach (var corner in corners)
                Require(canvasRect.rect.Contains(canvasRect.InverseTransformPoint(corner)), $"{resolution} page {page}: viewport inside canvas");
        }
        foreach (var text in canvas.GetComponentsInChildren<Text>())
            Require(text.preferredHeight <= text.rectTransform.rect.height + 1, $"{resolution} page {page}: text fits: {text.text} preferred={text.preferredHeight} rect={text.rectTransform.rect}");
        var buttons = canvas.GetComponentsInChildren<Button>();
        for (var i = 0; i < buttons.Length; i++)
        {
            var a = VisibleBounds(canvasRect, buttons[i]);
            for (var j = i + 1; j < buttons.Length; j++)
            {
                var b = VisibleBounds(canvasRect, buttons[j]);
                Require(Mathf.Min(a.xMax, b.xMax) - Mathf.Max(a.xMin, b.xMin) < 1 ||
                    Mathf.Min(a.yMax, b.yMax) - Mathf.Max(a.yMin, b.yMin) < 1,
                    $"{resolution} page {page}: visible buttons do not overlap");
            }
        }
        foreach (var button in buttons)
        {
            var rect = button.GetComponent<RectTransform>();
            Require(rect.rect.height >= 32, $"{resolution} page {page}: button height");
            rect.GetWorldCorners(corners);
            var parent = rect.parent as RectTransform;
            foreach (var corner in corners)
            {
                var local = parent.InverseTransformPoint(corner);
                Require(local.x >= parent.rect.xMin - 1 && local.x <= parent.rect.xMax + 1,
                    $"{resolution} page {page}: button inside parent width");
            }
        }
    }

    private static Rect BoundsIn(RectTransform canvas, RectTransform rect)
    {
        var corners = new Vector3[4]; rect.GetWorldCorners(corners);
        var min = canvas.InverseTransformPoint(corners[0]);
        var max = canvas.InverseTransformPoint(corners[2]);
        return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
    }
    private static Rect VisibleBounds(RectTransform canvas, Button button)
    {
        var bounds = BoundsIn(canvas, button.GetComponent<RectTransform>());
        var scroll = button.GetComponentInParent<ScrollRect>();
        if (scroll == null) return bounds;
        var viewport = BoundsIn(canvas, scroll.viewport);
        return Rect.MinMaxRect(Mathf.Max(bounds.xMin, viewport.xMin), Mathf.Max(bounds.yMin, viewport.yMin),
            Mathf.Min(bounds.xMax, viewport.xMax), Mathf.Min(bounds.yMax, viewport.yMax));
    }

    private static int Subscribers(object owner, string name)
    {
        for (var type = owner.GetType(); type != null; type = type.BaseType)
        {
            var field = type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            if (field != null) return (field.GetValue(owner) as Delegate)?.GetInvocationList().Length ?? 0;
        }
        throw new Exception("Missing event: " + name);
    }
    private static CellBaseAdapter Install(GridFieldDatabaseAdapter grid, string name, int x, int z)
    {
        var source = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Prefabs/Field/General/F_{name}Cell.prefab");
        var old = grid.GetCell(x, z);
        if (old is ConnectableCellBaseAdapter connected) connected.OnDisconnect();
        old.gameObject.SetActive(false);
        var cell = UnityEngine.Object.Instantiate(source, old.transform.position, Quaternion.identity, old.transform.parent).GetComponent<CellBaseAdapter>();
        UnityEngine.Object.Destroy(old.gameObject);
        grid.SaveCell(x, z, cell); cell.InitializeSystem();
        return cell;
    }
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("Economy UI check failed: " + message);
        _checks++;
    }
}

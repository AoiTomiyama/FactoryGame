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
        EditorSceneManager.OpenScene("Assets/Scenes/MainScene.unity");
        EditorUtility.audioMasterMute = true;
        Require(EditorUtility.audioMasterMute, "mute before play");
        SessionState.SetBool(Key, true);
        EditorApplication.isPlaying = true;
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
            Require(!panel.SkillButton(branch.Uid).interactable && panel.SkillButton(branch.Uid).GetComponentInChildren<Text>(true).text.Contains("前提スキルが必要"), "tree prerequisites and reason");
            wallet.TryReserveExpense(wallet.Current.AvailableBalance, out expense);
            Require(!panel.UpgradeInteractable && panel.EquipmentText.Contains("残高不足") && !panel.SkillButton(root.Uid).interactable, "reservations disable unaffordable actions");
            expense.TryCancel();
            wallet.TryReserveIncome(1000, out var income); income.TryCommit();
            panel.SkillButton(root.Uid).onClick.Invoke();
            Require(generator.Skills.IsOwned(root.Uid) && !panel.SkillButton(root.Uid).interactable && panel.SkillButton(branch.Uid).interactable, "real button purchases root and unlocks branch");
            var paidBalance = wallet.Current.Balance;
            panel.SkillButton(root.Uid).onClick.Invoke();
            Require(wallet.Current.Balance == paidBalance, "repeat root click never pays twice");
            panel.SkillButton(branch.Uid).onClick.Invoke();
            Require(generator.Skills.IsOwned(branch.Uid) && panel.SkillButton(branch.Uid).GetComponentInChildren<Text>(true).text.Contains("取得済み"), "branch state and effect label");
            view.UpdateUIStatusWindow(storage);
            view.SetStatusWindowActive(false);
            Require(Subscribers(storage, "StatusChanged") == 0 && !panel.UpgradeInteractable, "hide selection releases subscriptions");
            view.UpdateUIStatusWindow(storage);
            panel.SetExpanded(false);
            Require(!panel.IsSubscribed && Subscribers(wallet, "Changed") == 1, "collapsed panel releases wallet subscription; singularity remains active");
            var hiddenText = panel.BalanceText;
            wallet.TryReserveIncome(1, out income); income.TryCommit();
            Require(panel.BalanceText == hiddenText, "hidden panel does not receive wallet updates");
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
            var header = buttons.First(b => b.GetComponentInChildren<Text>(true).text.Contains("表示 / 非表示"));
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
            Require(wallet.LastNotificationException == null && generator.Skills.LastNotificationException == null, "notification handlers succeed");
            UnityEngine.Object.Destroy(panel);
            await UniTask.DelayFrame(2);
            Require(canvas == null && Subscribers(wallet, "Changed") == 1 && Subscribers(view, "SelectionChanged") == 0,
                "destroying panel releases generated canvas and subscriptions");
            Debug.Log($"Economy UI checks passed: {_checks} assertions; MainScene balance, conversion, quotes, buttons, prerequisites and subscription lifetime.");
            EditorApplication.Exit(0);
        }
        catch (Exception exception) { Debug.LogException(exception); EditorApplication.Exit(1); }
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

    private static void VerifyLayout(Canvas canvas, Vector2 resolution, int page)
    {
        var canvasRect = canvas.GetComponent<RectTransform>();
        var corners = new Vector3[4];
        foreach (var scroll in canvas.GetComponentsInChildren<ScrollRect>())
        {
            scroll.GetComponent<RectTransform>().GetWorldCorners(corners);
            foreach (var corner in corners)
                Require(canvasRect.rect.Contains(canvasRect.InverseTransformPoint(corner)), $"{resolution} page {page}: viewport inside canvas");
        }
        foreach (var text in canvas.GetComponentsInChildren<Text>())
            Require(text.preferredHeight <= text.rectTransform.rect.height + 1, $"{resolution} page {page}: text fits: {text.text}");
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

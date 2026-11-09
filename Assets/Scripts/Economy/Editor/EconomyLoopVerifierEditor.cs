using System;
using System.Linq;
using Cysharp.Threading.Tasks;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>MainSceneの収益だけで再投資まで進め、取引と能力の接続を検証する。</summary>
[InitializeOnLoad]
public static class EconomyLoopVerifierEditor
{
    private const string Key = "FactoryGame.EconomyLoopVerification";
    private static int _checks;
    static EconomyLoopVerifierEditor() => EditorApplication.playModeStateChanged += state =>
    {
        if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Key, false)) return;
        SessionState.EraseBool(Key);
        Verify().Forget();
    };

    public static void Run()
    {
        EditorUtility.audioMasterMute = true;
        EditorSceneManager.OpenScene("Assets/Scenes/MainScene.unity");
        SessionState.SetBool(Key, true);
        EditorApplication.isPlaying = true;
    }

    public static void RunTransfers()
    {
        EditorUtility.audioMasterMute = true;
        ResourceTransferVerifierEditor.Run();
    }

    public static void RunCrafting()
    {
        EditorUtility.audioMasterMute = true;
        CraftingVerifierEditor.RunPlay();
    }

    private static async UniTaskVoid Verify()
    {
        var originalScale = Time.timeScale;
        try
        {
            await UniTask.DelayFrame(3);
            Require(EditorUtility.audioMasterMute, "muted before playback");
            var generator = UnityEngine.Object.FindAnyObjectByType<GridFieldGeneratorAdapter>();
            var grid = GridFieldDatabaseAdapter.Instance;
            var wallet = generator.Wallet;
            var panel = generator.GetComponent<EconomyPanelAdapter>();
            var view = UnityEngine.Object.FindAnyObjectByType<CellStatusViewAdapter>();
            var placer = UnityEngine.Object.FindAnyObjectByType<CellPlacerAdapter>();
            Require(wallet.Current.Balance == 100 && panel.BalanceText.Contains("残高 100"), "initial shared balance");
            long earned = 0, spent = 0, previous = 100;
            wallet.Changed += snapshot =>
            {
                if (snapshot.Balance > previous) earned += snapshot.Balance - previous;
                previous = snapshot.Balance;
            };
            void Place(int x, int z, CellTypeDomain type, float rotation = 0)
            {
                Require(generator.Prices.TryGetPlacementAmount(type, 1, out var price), "defined equipment price");
                var balance = wallet.Current.Balance;
                placer.transform.rotation = Quaternion.Euler(0, rotation, 0);
                placer.SetSelectedCellType(type);
                placer.SelectGrid(grid.GetCell(x, z).gameObject);
                placer.PointerBegin(); placer.PointerDrag(); placer.PointerEnd();
                Require(wallet.Current.Balance == balance - price, "placement charged once");
                spent += price;
            }
            Place(50, 47, CellTypeDomain.ExtractorWood, 180);
            Place(50, 48, CellTypeDomain.ExportConveyor);
            Place(50, 49, CellTypeDomain.Conveyor);
            Require(spent == 30 && wallet.Current.Balance == 70, "starter route costs 30 of initial 100");
            var extractor = grid.GetCell(50, 47) as ExtractorCellAdapter;
            var conveyor = grid.GetCell(50, 49) as ConveyorCellAdapter;
            Require(extractor != null && conveyor != null, "route installed through pointer commands");
            await Funds(71);
            Require(earned > 0 && panel.BalanceText.Contains(wallet.Current.Balance.ToString()), "first real extraction transfer and conversion");
            Debug.Log($"Economy loop first sale: balance={wallet.Current.Balance}, earned={earned}, spent={spent}");

            // 初回は通常速度で確認し、以後は同じ周期・搬送処理を時間倍率で短縮する。
            Time.timeScale = 20;
            var identity = extractor.Identity;
            var baseExtraction = extractor.ExtractionAmount;
            var baseCapacity = extractor.StorageCapacity;
            var baseTransfer = conveyor.TransferAmount;
            view.UpdateUIStatusWindow(extractor);
            await Funds(50);
            Require(panel.PurchaseSkill("basic-research") == SkillPurchaseResultApplication.Success, "root purchased from earnings");
            spent += 50;
            foreach (var tier in new[] { 1, 2 })
            {
                foreach (var branch in new[] { "wood", "stone", "storage", "crafter", "export" })
                {
                    var uid = branch + "-" + tier;
                    var node = generator.Skills.Catalog.Nodes.Single(n => n.Uid == uid);
                    await Funds(node.Price);
                    Require(panel.SkillButton(uid).interactable, "earned funds enable skill button");
                    panel.SkillButton(uid).onClick.Invoke();
                    panel.EquipmentView.UpgradeButton.onClick.Invoke();
                    Require(generator.Skills.IsOwned(uid) && !panel.UpgradeInteractable, "detail action applies type skill once");
                    spent += node.Price;
                }
                Require(extractor.ExtractionAmount == baseExtraction * (tier + 1) &&
                    extractor.StorageCapacity == baseCapacity * (tier + 1) &&
                    conveyor.TransferAmount == baseTransfer, "I/II apply shared type levels to existing equipment");
            }
            // 空セルへ課金して新設し、既設・新設で同じ共有効果になることを確認する。
            var empty = UnityEngine.Object.FindObjectsByType<EmptyCellAdapter>(FindObjectsSortMode.None)
                .First(c => c.XIndex > 5 && c.ZIndex > 5 && c.XIndex < 45 && c.ZIndex < 45);
            await Funds(5);
            Place(empty.XIndex, empty.ZIndex, CellTypeDomain.ExportConveyor);
            var added = (ExportConveyorCellAdapter)grid.GetCell(empty.XIndex, empty.ZIndex);
            Require(added.TransferAmount == ((ExportConveyorCellAdapter)grid.GetCell(50, 48)).TransferAmount, "reinvested new equipment inherits global effects");
            var before = wallet.Current.Balance;
            await Funds(before + 1);
            Require(wallet.Current.Balance == 100 + earned - spent && wallet.Current.ReservedExpense == 0,
                "all purchases reconcile with actual conversion income");
            Require(wallet.LastNotificationException == null && generator.Skills.LastNotificationException == null,
                "UI and ability notifications succeed");
            Debug.Log($"Economy loop passed: {_checks} assertions; earned={earned}, spent={spent}, balance={wallet.Current.Balance}; initial100, actual sale, Lv3, all I/II, new equipment and continued income.");
            Time.timeScale = originalScale;
            EditorApplication.Exit(0);
        }
        catch (Exception exception)
        {
            Time.timeScale = originalScale;
            Debug.LogException(exception);
            EditorApplication.Exit(1);
        }
    }

    private static async UniTask Funds(long amount)
    {
        var deadline = Time.realtimeSinceStartup + 180;
        while (UnityEngine.Object.FindAnyObjectByType<GridFieldGeneratorAdapter>().Wallet.Current.AvailableBalance < amount)
        {
            if (Time.realtimeSinceStartup > deadline) throw new TimeoutException("Actual production did not fund next purchase: " + amount);
            await UniTask.Yield();
        }
    }
    private static void Require(bool condition, string message)
    {
        _checks++;
        if (!condition) throw new InvalidOperationException("Economy loop check failed: " + message);
    }
}

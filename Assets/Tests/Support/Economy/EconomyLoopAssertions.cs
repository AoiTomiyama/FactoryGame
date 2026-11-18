#if UNITY_EDITOR
using System;
using System.Linq;
using Cysharp.Threading.Tasks;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>MainSceneの収益だけで再投資まで進め、取引と能力の接続を検証する。</summary>
public static class EconomyLoopAssertions
{
    private static int _checks;








    public static async UniTask Verify()
    {
        var originalScale = Time.timeScale;
        try
        {
            await UniTask.DelayFrame(3);
            Require(EditorUtility.audioMasterMute, "muted before playback");
            var initializer = TestScenes.Find<GameInitializerAdapter>();
            var grid = GridFieldDatabaseAdapter.Instance;
            var wallet = initializer.Wallet;
            var panel = TestScenes.Find<EconomyPanelAdapter>();
            var placer = TestScenes.Find<CellPlacerAdapter>();
            Require(wallet.Current.Balance == 100 && panel.BalanceText.Contains("残高 100"), "initial shared balance");
            long earned = 0, spent = 0, previous = 100;
            wallet.Changed += snapshot =>
            {
                if (snapshot.Balance > previous) earned += snapshot.Balance - previous;
                previous = snapshot.Balance;
            };
            void Place(int x, int z, CellTypeDomain type, float rotation = 0)
            {
                Require(initializer.Prices.TryGetPlacementAmount(type, 1, out var price), "defined equipment price");
                var balance = wallet.Current.Balance;
                placer.transform.rotation = Quaternion.Euler(0, rotation, 0);
                placer.SetSelectedCellType(type);
                placer.SelectGrid(grid.GetCell(x, z).gameObject);
                placer.PointerBegin(); placer.PointerDrag(); placer.PointerEnd();
                Require(wallet.Current.Balance == balance - price, "placement charged once");
                spent += price;
            }
            var plan=initializer.Session.Selected;
            foreach(var command in TestScenes.StarterPlacements(initializer.Session.Selected)) Place(command.X,command.Z,command.Type,command.Yaw);
            Require(wallet.Current.Balance==100-spent,"starter route stays within initial budget");
            var extractor=grid.GetCell(plan.Extractor.x,plan.Extractor.z) as ExtractorCellAdapter;
            Require(extractor != null, "route installed through pointer commands");
            await Funds(wallet.Current.Balance+1);
            Require(earned > 0 && panel.BalanceText.Contains(wallet.Current.Balance.ToString()), "first real extraction transfer and conversion");
            Debug.Log($"Economy loop first sale: balance={wallet.Current.Balance}, earned={earned}, spent={spent}");

            // 初回は通常速度で確認し、以後は同じ周期・搬送処理を時間倍率で短縮する。
            Time.timeScale = 20;
            var baseExtraction = extractor.ExtractionAmount;
            var baseDuration = extractor.ExtractionSecond;
            await Funds(50);
            Require(initializer.Skills.Purchase("basic-research") == SkillPurchaseResultApplication.Success, "root purchased from earnings");
            spent += 50;

            const string uid = "extraction-speed-1";
            Require(initializer.Skills.Catalog.TryGet(uid, out var node), "extraction speed unlock is configured");
            await Funds(node.Price);
            panel.SkillScreen.Open();
            Require(panel.SkillScreen.NodeButton(uid) != null, "earned funds reveal speed skill");
            panel.SkillScreen.NodeButton(uid).onClick.Invoke();
            panel.SkillScreen.Details.UpgradeButton.onClick.Invoke();
            Require(initializer.Skills.IsOwned(uid) && !panel.SkillScreen.Details.Interactable, "detail action applies the speed skill once");
            panel.SkillScreen.Close();
            spent += node.Price;
            await UniTask.NextFrame();
            Require(Mathf.Approximately(extractor.ExtractionSecond, baseDuration / 1.25f) &&
                extractor.ExtractionAmount == baseExtraction, "early speed upgrade keeps batch size");

            // 収益から建てた設備にも同じ速度強化が適用される。
            var empty = grid.Owners.OfType<EmptyCellAdapter>().First(c => c.XIndex > 5 && c.ZIndex > 5 && c.XIndex < 45 && c.ZIndex < 45);
            Require(initializer.Prices.TryGetPlacementAmount(CellTypeDomain.ExtractorWood, 1, out var price), "new extractor price");
            await Funds(price);
            Place(empty.XIndex, empty.ZIndex, CellTypeDomain.ExtractorWood);
            var added = (ExtractorCellAdapter)grid.GetCell(empty.XIndex, empty.ZIndex);
            Require(Mathf.Approximately(added.ExtractionSecond, extractor.ExtractionSecond),
                "reinvested extractor inherits shared speed");
            var before = wallet.Current.Balance;
            await Funds(before + 1);
            Require(wallet.Current.Balance == 100 + earned - spent && wallet.Current.ReservedExpense == 0,
                "all purchases reconcile with actual conversion income");
            Require(wallet.LastNotificationException == null && initializer.Skills.LastNotificationException == null,
                "UI and ability notifications succeed");
            Debug.Log($"Economy loop passed: {_checks} assertions; earned={earned}, spent={spent}, balance={wallet.Current.Balance}; initial100, actual sale, shared speed, new equipment and continued income.");
            Time.timeScale = originalScale;
        }
        catch (Exception exception)
        {
            Time.timeScale = originalScale;
            throw;
        }
    }

    private static async UniTask Funds(long amount)
    {
        var deadline = Time.realtimeSinceStartup + 180;
        while (TestScenes.Find<GameInitializerAdapter>().Wallet.Current.AvailableBalance < amount)
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

#endif

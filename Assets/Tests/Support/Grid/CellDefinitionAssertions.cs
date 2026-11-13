#if UNITY_EDITOR
using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

public static class CellDefinitionAssertions
{
    private const BindingFlags Fields = BindingFlags.Instance | BindingFlags.NonPublic;
    public static IEnumerator VerifyPlacement()
    {
        // 通常のMainScene.StartとUI生成を待つ。辞書を手動Validateして起動不具合を隠さない。
        yield return null;
        yield return null;
        var placer = TestScenes.Find<CellPlacerAdapter>();
        Require(placer != null, "scene placer exists");
        foreach (var database in (CellDatabaseDefinition[])Get(placer, "cellDatabaseArr"))
        {
            var expected = (CellInfoDefinition[])Get(database, "cellPairingInfos");
            Require(database.GetAllCellInfos().Count == expected.Select(info => info.CellTypeDomain).Distinct().Count(), "cold definition count");
            foreach (var info in expected)
                Require(database.TryGetCellInfo(info.CellTypeDomain, out var actual) && actual.Equals(info), "cold prefab references");
        }

        var button = UnityEngine.Object.FindObjectsByType<CellSelectButtonUIAdapter>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .FirstOrDefault(candidate => candidate.name == "StorageButton");
        Require(button != null, "actual generated StorageButton exists");
        ((Button)Get(button, "button")).onClick.Invoke();
        Require(Get(placer, "_cachedCell") is StorageCellAdapter, "real button callback selects Storage");
        Require((GameObject)Get(placer, "_placeholderCell") != null, "selection creates placeholder");

        var initializer = TestScenes.Find<GameInitializerAdapter>();
        TestScenes.Find<EconomyPanelAdapter>().ShowPage(0);
        // 定義から実配置までの経路が対象なので、購入材料は現行価格表から準備する。
        Require(initializer.Prices.TryGetMaterialCosts(CellTypeDomain.Storage,1,out var materials), "Storage material costs are defined");
        foreach (var material in materials)
        {
            Require(initializer.ConstructionStock.TryReserveDeposit(material.Resource,material.Amount,out var deposit), "prepare Storage construction material");
            Require(deposit.TryCommit(), "construction material deposit commits");
        }
        Require(initializer.Prices.TryGetPlacementAmount(CellTypeDomain.Storage,1,out var price), "Storage price is defined");
        var balance = initializer.Wallet.Current.Balance;
        Require(balance >= price, "fixture has enough currency");
        var grid = GridFieldDatabaseAdapter.Instance;
        EmptyCellAdapter empty = null;
        for (var x = 0; grid.IsWithinBounds(x, 0) && empty == null; x++)
            for (var z = 0; grid.IsWithinBounds(x, z) && empty == null; z++)
                empty = grid.GetCell(x, z) as EmptyCellAdapter;
        Require(empty != null, "empty placement target exists");
        var targetX = empty.XIndex;
        var targetZ = empty.ZIndex;
        placer.SelectGrid(empty.gameObject);
        placer.PointerBegin();
        placer.PointerDrag();
        placer.PointerEnd();
        yield return null;
        Require(grid.GetCell(targetX, targetZ) is StorageCellAdapter placed && placed.gameObject.activeInHierarchy,
            "pointer placement creates active Storage in grid");
        Require(((CellBaseAdapter)Get(placer, "_selectedCell")) == grid.GetCell(targetX, targetZ), "selection follows placed cell");
        Require(placer.LastPlacedCount == 1 && initializer.Wallet.Current.Balance == balance-price, "placement charges once");
        foreach (var material in materials)
            Require(initializer.ConstructionStock.Stored(material.Resource) == 0, "placement consumes prepared construction material");
        yield return null;
    }

    private static object Get(object target, string name) => target.GetType().GetField(name, Fields).GetValue(target);
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("Cell definition Play Mode check failed: " + message);
    }
}
#endif

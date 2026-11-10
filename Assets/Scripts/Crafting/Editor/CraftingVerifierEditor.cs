using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Input = RecipeRulesDomain.Input;
using Ingredient = RecipeRulesDomain.Ingredient;
using Output = RecipeRulesDomain.Output;
using Recipe = RecipeRulesDomain.Recipe;

/// <summary>純粋な計算と加工セルの寿命を別の実行入口から検証する。</summary>
[InitializeOnLoad]
public static class CraftingVerifierEditor
{
    private const string SessionKey = "FactoryGame.CraftingVerifierEditor.Running";
    private const BindingFlags InstanceFields = BindingFlags.Instance | BindingFlags.NonPublic;
    private const ResourceTypeDomain Stone = ResourceTypeDomain.Stone;
    private const ResourceTypeDomain Wood = ResourceTypeDomain.Wood;

    static CraftingVerifierEditor() => EditorApplication.playModeStateChanged += OnPlayModeChanged;

    [MenuItem("Tools/FactoryGame/Verify Crafting Rules")]
    public static void RunRules()
    {
        var ingredients = new[] { new Ingredient(Stone, 3), new Ingredient(Stone, 5) };
        var recipe = new Recipe(ingredients, Wood, 2);
        ingredients[0] = new Ingredient(Wood, 100);
        var initial = new[] { new Input(1, Stone, 5), new Input(2, Stone, 3) };
        Require(RecipeRulesDomain.TryCreatePlan(recipe, initial, default, 5, out var plan) &&
                plan.Consumptions[0].InputId == 2 && plan.Consumptions[1].InputId == 1,
            "same-type inputs are matched without enumeration-order failure; recipe is a snapshot");

        var updated = new[] { new Input(2, Stone, 4, 1), new Input(1, Stone, 7, 2) };
        var operation = new CraftingOperationApplication(plan);
        Require(operation.TryComplete(updated, new Output(Wood, 1), 5, out var commit) &&
                commit.Inputs[0].Amount == 1 && commit.Inputs[0].Reserved == 1 &&
                commit.Inputs[1].Amount == 2 && commit.Inputs[1].Reserved == 2 && commit.Result.Amount == 3 &&
                updated[0].Amount == 4 && updated[1].Amount == 7,
            "same input IDs survive reordering and delivery; no mutation before full validation");
        Require(!operation.TryComplete(updated, default, 5, out _) && !operation.TryCancel(),
            "completion is one-shot");
        var cancelled = new CraftingOperationApplication(plan);
        Require(cancelled.TryCancel() && !cancelled.TryCancel() &&
                !cancelled.TryComplete(initial, default, 5, out _), "cancellation is terminal");

        var insufficient = new[] { new Input(1, Stone, 5), new Input(2, Stone, 2) };
        var rejected = new CraftingOperationApplication(plan);
        Require(!rejected.TryComplete(insufficient, default, 5, out var failed) && failed == null &&
                rejected.CurrentStage == CraftingOperationApplication.Stage.Cancelled &&
                !rejected.TryComplete(initial, default, 5, out _) && insufficient[0].Amount == 5,
            "missing material cancels without partial consumption or later recommit");
        Require(!RecipeRulesDomain.TryApply(plan, new[] { new Input(1, Stone, 5), new Input(3, Stone, 3) },
                default, 5, out _), "fixed plan does not silently switch input IDs");
        Require(!RecipeRulesDomain.TryApply(plan, initial, new Output(Stone, 1), 5, out _) &&
                !RecipeRulesDomain.TryApply(plan, initial, default, 1, out _),
            "result type mismatch and capacity loss do not consume material");

        Require(!RecipeRulesDomain.TryCreatePlan(new Recipe(new[] { new Ingredient(Stone, 4) }, Wood, 1),
                new[] { new Input(1, Stone, 2), new Input(2, Stone, 2) }, default, 5, out _),
            "one ingredient is not split across inputs");
        Require(!RecipeRulesDomain.TryCreatePlan(recipe, new[] { new Input(1, Stone, 5), new Input(1, Stone, 3) },
                default, 5, out _), "duplicate input IDs are invalid");
        foreach (var invalid in new[] { 0, -1 })
        {
            Require(!RecipeRulesDomain.TryCreatePlan(new Recipe(new[] { new Ingredient(Stone, invalid) }, Wood, 1),
                        initial, default, 5, out _) &&
                    !RecipeRulesDomain.TryCreatePlan(new Recipe(new[] { new Ingredient(Stone, 1) }, Wood, invalid),
                        initial, default, 5, out _), "non-positive material/result quantities are invalid");
        }
        Require(!RecipeRulesDomain.TryCreatePlan(new Recipe(Array.Empty<Ingredient>(), Wood, 1), initial,
                    default, 5, out _) &&
                !RecipeRulesDomain.TryCreatePlan(new Recipe(new[] { new Ingredient(ResourceTypeDomain.None, 1) }, Wood, 1),
                    initial, default, 5, out _) &&
                !RecipeRulesDomain.TryCreatePlan(recipe, new[] { new Input(1, Stone, -1) }, default, 5, out _) &&
                !RecipeRulesDomain.TryCreatePlan(recipe, new[] { new Input(1, Stone, 5, -1) }, default, 5, out _) &&
                !RecipeRulesDomain.TryCreatePlan(recipe, initial, default, -1, out _), "invalid state is rejected");
        var maxRecipe = new Recipe(new[] { new Ingredient(Stone, int.MaxValue) }, Wood, 1);
        var maxInputs = new[] { new Input(1, Stone, int.MaxValue) };
        Require(RecipeRulesDomain.TryCreatePlan(maxRecipe, maxInputs, new Output(Wood, int.MaxValue - 1),
                    int.MaxValue, out var maxPlan) &&
                RecipeRulesDomain.TryApply(maxPlan, maxInputs, new Output(Wood, int.MaxValue - 1), int.MaxValue,
                    out var maxCommit) && maxCommit.Result.Amount == int.MaxValue &&
                !RecipeRulesDomain.TryApply(maxPlan, maxInputs, new Output(Wood, int.MaxValue), int.MaxValue, out _),
            "quantity limits use subtraction before addition");
        VerifyMatchingAgainstSearch();
        Debug.Log("Crafting rule checks passed: matching, immutable plan, reservation preservation, atomic result, quantity limits, one-shot completion/cancellation.");
    }

    private static void VerifyMatchingAgainstSearch()
    {
        // 独立した全探索と729通りを比較し、数量順の割当で成立可能な組を見落とさないことを確認する。
        for (var a = 1; a <= 3; a++) for (var b = 1; b <= 3; b++) for (var c = 1; c <= 3; c++)
        for (var x = 1; x <= 3; x++) for (var y = 1; y <= 3; y++) for (var z = 1; z <= 3; z++)
        {
            var available = new[] { a, b, c };
            var required = new[] { x, y, z };
            var expected = Search(available, required, new bool[3], 0);
            var actual = RecipeRulesDomain.TryCreatePlan(
                new Recipe(new[] { new Ingredient(Stone, x), new Ingredient(Stone, y), new Ingredient(Stone, z) }, Wood, 1),
                new[] { new Input(1, Stone, a), new Input(2, Stone, b), new Input(3, Stone, c) }, default, 1, out _);
            Require(actual == expected, "quantity-order matching agrees with independent exhaustive search");
        }
    }

    private static bool Search(int[] available, int[] required, bool[] used, int index)
    {
        if (index == required.Length) return true;
        for (var i = 0; i < available.Length; i++)
        {
            if (used[i] || available[i] < required[index]) continue;
            used[i] = true;
            if (Search(available, required, used, index + 1)) return true;
            used[i] = false;
        }
        return false;
    }

    public static void RunPlay()
    {
        EditorUtility.audioMasterMute = true;
        Require(EditorUtility.audioMasterMute, "mute before crafting play check");
        SessionState.SetBool(SessionKey, true);
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        EditorApplication.isPlaying = true;
    }

    private static void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(SessionKey, false)) return;
        SessionState.EraseBool(SessionKey);
        VerifyPlayAsync().Forget();
    }

    private static async UniTaskVoid VerifyPlayAsync()
    {
        try
        {
            Time.timeScale = 1;
            var gridObject = new GameObject("CraftingCheck:Grid");
            var grid = gridObject.AddComponent<GridFieldDatabaseAdapter>();
            var row = new GameObject("Row").transform;
            row.SetParent(gridObject.transform);
            await VerifySavedProducts(grid, row);
            var cell = CreateCrafter(grid, row, 0.3f);
            await Wait(() => GetOperation(cell) != null);
            var operation = GetOperation(cell);
            ResourceReservationApplication pending = null;
            Require(ResourceReservationApplication.TryCreate(cell, 1, 1, Stone, out var incoming) &&
                    incoming.TryCommit() && ResourceReservationApplication.TryCreate(cell, 0, 2, Stone,
                        out pending), "delivery and outstanding reservation during crafting");
            await Wait(() => cell.ExportStorageAmount == 2);
            Require(operation.CurrentStage == CraftingOperationApplication.Stage.Completed &&
                    cell.GetInput(1).Amount == 1 &&
                    cell.GetInput(0).Amount == 0 &&
                    cell.GetInput(0).Allocated == 2 && pending.TryCommit() &&
                    cell.GetInput(0).Amount == 2 &&
                    cell.GetInput(0).Allocated == 0,
                "planned material is consumed once and pending delivery remains valid");
            UnityEngine.Object.Destroy(cell.gameObject);
            await UniTask.Yield();

            cell = CreateCrafter(grid, row, 0.3f);
            await Wait(() => GetOperation(cell) != null);
            operation = GetOperation(cell);
            cell.enabled = false;
            await UniTask.Delay(450);
            Require(operation.CurrentStage == CraftingOperationApplication.Stage.Cancelled &&
                    cell.ExportStorageAmount == 0 && cell.GetInput(1).Amount == 3 &&
                    cell.GetInput(0).Amount == 5 && cell.ProcessTime == 0,
                "disable cancels without consumption or output");
            cell.enabled = true;
            await Wait(() => cell.ExportStorageAmount == 2);
            Require(operation.CurrentStage == CraftingOperationApplication.Stage.Cancelled,
                "enable starts a new plan; old cancellation remains terminal");
            UnityEngine.Object.Destroy(cell.gameObject);
            await UniTask.Yield();

            cell = CreateCrafter(grid, row, 0.3f);
            await Wait(() => GetOperation(cell) != null);
            operation = GetOperation(cell);
            var inputs = GetInputs(cell);
            UnityEngine.Object.Destroy(cell.gameObject);
            await UniTask.Delay(450);
            Require(operation.CurrentStage == CraftingOperationApplication.Stage.Cancelled &&
                    inputs[1].Amount == 3 && inputs[0].Amount == 5,
                "deletion cannot consume the selected input snapshot");

            cell = CreateCrafter(grid, row, 0.3f);
            await Wait(() => GetOperation(cell) != null);
            operation = GetOperation(cell);
            SetField(cell, "exporterCapacity", 1);
            await Wait(() => operation.CurrentStage == CraftingOperationApplication.Stage.Cancelled);
            Require(cell.ExportStorageAmount == 0 && cell.GetInput(1).Amount == 3 &&
                    cell.GetInput(0).Amount == 5, "capacity loss does not partially consume");
            UnityEngine.Object.Destroy(cell.gameObject);
            await UniTask.Yield();

            cell = CreateCrafter(grid, row, 0.3f);
            await Wait(() => GetOperation(cell) != null);
            operation = GetOperation(cell);
            inputs = GetInputs(cell);
            inputs[1] = new CrafterCellAdapter.ResourceInputData { Type = Stone, Amount = 2 };
            await Wait(() => operation.CurrentStage == CraftingOperationApplication.Stage.Cancelled);
            Require(cell.ExportStorageAmount == 0 && inputs[0].Amount == 5,
                "material loss leaves the other input untouched");
            UnityEngine.Object.Destroy(cell.gameObject);
            await UniTask.Yield();

            cell = CreateCrafter(grid, row, 0.3f);
            await Wait(() => GetOperation(cell) != null);
            operation = GetOperation(cell);
            DOTween.KillAll();
            cell.enabled = false;
            Require(operation.CurrentStage == CraftingOperationApplication.Stage.Cancelled &&
                    cell.ExportStorageAmount == 0 && cell.GetInput(1).Amount == 3,
                "externally killed progress tween is not completion");
            UnityEngine.Object.Destroy(cell.gameObject);
            await UniTask.Yield();

            cell = CreateCrafter(grid, row, 0);
            await Wait(() => cell.ExportStorageAmount == 2);
            Require(cell.GetInput(1).Amount == 0 && cell.GetInput(0).Amount == 0,
                "zero duration still consumes and produces exactly once");
            UnityEngine.Object.Destroy(cell.gameObject);
            UnityEngine.Object.Destroy(gridObject);
            Debug.Log("Crafting play checks passed: completion, delivery/reservation, disable/enable, deletion, capacity/material loss, tween interruption, zero duration.");
            EditorApplication.Exit(0);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorApplication.Exit(1);
        }
    }

    private static async UniTask VerifySavedProducts(GridFieldDatabaseAdapter grid, Transform row)
    {
        var database = AssetDatabase.LoadAssetAtPath<RecipeDatabaseDefinition>("Assets/Scripts/ScriptableObject/Recipes/RecipeDB.asset");
        var resources = AssetDatabase.LoadAssetAtPath<ResourceDefinition>("Assets/Scripts/ScriptableObject/ResourceDB.asset");
        var prices = AssetDatabase.LoadAssetAtPath<CurrencyEconomyDefinition>("Assets/Scripts/ScriptableObject/CurrencyEconomy.asset").CreateCatalog();
        var wallet = new CurrencyWalletApplication(0);
        var intake = new SingularityIntakeApplication(wallet, prices, new[] { 0, 1, 2, 3 }) { Accepting = true };
        var poolRoot = new GameObject("CraftingCheck:ProductPool");
        var pool = poolRoot.AddComponent<ResourceItemObjectPoolAdapter>();
        SetField(pool, "resourceDatabase", resources);
        var products = new[] { ResourceTypeDomain.Plank, ResourceTypeDomain.StoneBrick, ResourceTypeDomain.BuildingParts };
        var recipes = database.recipes.Where(recipe => products.Contains(recipe.Result)).ToArray();
        Require(recipes.Length == products.Length && recipes.Select(recipe => recipe.Result).Distinct().Count() == products.Length,
            "saved definitions contain all three product recipes");
        var previousScale = Time.timeScale;
        Time.timeScale = 10;
        try
        {
            foreach (var recipe in recipes)
            for (var turn = 0; turn < 4; turn++)
            {
                var source = AssetDatabase.LoadAssetAtPath<CrafterCellAdapter>("Assets/Prefabs/Field/General/F_" +
                    (recipe.Ingredients.Length == 1 ? "Crafter" : "AdvancedCrafter") + "Cell.prefab");
                var cell = UnityEngine.Object.Instantiate(source, new Vector3(4, 0, 4), Quaternion.Euler(0, turn * 90, 0), row);
                var root = cell.gameObject;
                grid.InitializeCells(9); cell.InitializeSystem();
                Require(cell.InputCount == recipe.Ingredients.Length && cell.OccupiedCoordinates.All(p => grid.GetCell(p.x, p.z) == cell),
                    "saved crafting prefab input count and rotated occupancy");
                if (cell.InputCount == 1) Require(!cell.AllocateStorage(1, 1, ResourceTypeDomain.Wood), "basic crafter rejects second input");
                for (var i = 0; i < recipe.Ingredients.Length; i++)
                {
                    var ingredient = recipe.Ingredients[i]; var direction = i;
                    Require(cell.AllocateStorage(direction, ingredient.requiredAmount, ingredient.resourceType), "product materials reserve");
                    cell.StoreResource(direction, ingredient.requiredAmount);
                }
                await Wait(() => cell.ExportStorageAmount == recipe.ResultAmount);
                Require(cell.ExportResourceType == recipe.Result && cell.GetInput(0).Amount == 0 &&
                    (recipe.Ingredients.Length == 1 || cell.GetInput(1).Amount == 0),
                    "saved recipe consumes exact materials and produces " + recipe.Result);
                Require(!cell.TryExport(0, recipe.ResultAmount, out _, out _), "input port cannot export");
                Require(cell.TryExport(2, recipe.ResultAmount, out var amount, out var type) && type == recipe.Result,
                    "crafted product exports");
                var id = pool.CreateIdFromResourceData(type, amount);
                Require(pool.TryRestoreVisual(id) && pool.TakeResourceDataById(id).type == type,
                    "crafted product rents its saved visual prefab");
                var before = wallet.Current.Balance;
                Require(prices.TryGetSaleAmount(type, amount, out var income) && intake.TryReserve(0, amount, type),
                    "crafted product sale reserves");
                intake.Commit(0, amount);
                Require(wallet.Current.Balance == before + income && wallet.Current.ReservedIncome == 0,
                    "crafted product sale commits once");
                pool.DisposeId(id);
                UnityEngine.Object.Destroy(root); await UniTask.Yield();
            }
            Require(wallet.Current.Balance == 92, "three crafted product sale prices match saved definitions");
            await VerifyMultiblockRoute(grid, row, pool, prices);
            Debug.Log("Saved product checks passed: plank, stone brick, building parts; crafting, export, pooled visuals and sale.");
        }
        finally { Time.timeScale = previousScale; UnityEngine.Object.Destroy(poolRoot); }
    }

    private static async UniTask VerifyMultiblockRoute(GridFieldDatabaseAdapter grid, Transform row,
        ResourceItemObjectPoolAdapter pool, CurrencyPriceCatalogDomain prices)
    {
        for (var turn = 0; turn < 4; turn++)
        {
            var route = new GameObject("CraftingCheck:MultiblockRoute").transform; route.SetParent(row);
            var rotation = Quaternion.Euler(0, turn * 90, 0);
            var crafter = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<CrafterCellAdapter>(
                "Assets/Prefabs/Field/General/F_AdvancedCrafterCell.prefab"), new Vector3(8, 0, 8), rotation, route);
            ConveyorCellAdapter Input(int port, ResourceTypeDomain type)
            {
                var cell = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<ConveyorCellAdapter>(
                    "Assets/Prefabs/Field/General/F_ConveyorCell.prefab"), crafter.PortPosition(port) + crafter.PortDirection(port), rotation, route);
                var id = pool.CreateIdFromResourceData(type, 2);
                typeof(ConveyorCellAdapter).GetProperty("ResourceId", InstanceFields).SetValue(cell, id);
                typeof(ConveyorCellAdapter).GetProperty("HasResource", InstanceFields).SetValue(cell, true);
                SetField(cell, "_readyToSend", true);
                return cell;
            }
            var left = Input(0, ResourceTypeDomain.Plank);
            var right = Input(1, ResourceTypeDomain.StoneBrick);
            var output = crafter.PortPosition(2);
            var forward = crafter.PortDirection(2);
            var exporter = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<ExportConveyorCellAdapter>(
                "Assets/Prefabs/Field/General/F_ExportConveyorCell.prefab"), output + forward, rotation, route);
            // 中央以外の外周入力へ接続し、基準座標への接続に戻っていないことも確認する。
            var sink = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<SingularityCellAdapter>(
                "Assets/Prefabs/Field/General/F_SingularityCell.prefab"), output + forward * 4 + rotation * Vector3.right, rotation, route);
            var wallet = new CurrencyWalletApplication(0);
            sink.Configure(wallet, prices);
            grid.InitializeCells(18);
            sink.InitializeSystem(); crafter.InitializeSystem(); exporter.InitializeSystem(); left.InitializeSystem(); right.InitializeSystem();
            Require(crafter.Connections.Count == 3 && sink.Connections.Count == 1, "both rear inputs and front output connect to distinct exterior ports");
            await Wait(() => wallet.Current.Balance == 16);
            Require(crafter.GetInput(0).Amount == 0 && crafter.GetInput(1).Amount == 0 && crafter.ExportStorageAmount == 0 &&
                wallet.Current.ReservedIncome == 0 && pool.Resources.Count == 0, "two resources craft and sell once without remaining reservations or IDs");
            UnityEngine.Object.Destroy(route.gameObject); await UniTask.Yield();
        }
        Debug.Log("Multiblock route checks passed: four rotations, independent rear inputs, real building-parts craft, front export and off-center 5x5 singularity sale.");
    }

    private static CrafterCellAdapter CreateCrafter(GridFieldDatabaseAdapter grid, Transform row, float seconds)
    {
        var root = new GameObject("CraftingCheck:Cell");
        root.transform.SetParent(row);
        new GameObject("Model").transform.SetParent(root.transform);
        var cell = root.AddComponent<CrafterCellAdapter>();
        var definition = new RecipeDataDefinition();
        SetField(definition, "ingredients", new[]
        {
            new IngredientDefinition { resourceType = Stone, requiredAmount = 3 },
            new IngredientDefinition { resourceType = Stone, requiredAmount = 5 }
        });
        SetField(definition, "result", Wood);
        SetField(definition, "resultAmount", 2);
        SetField(definition, "craftSecond", seconds);
        var database = ScriptableObject.CreateInstance<RecipeDatabaseDefinition>();
        database.recipes = new[] { definition };
        SetField(cell, "recipeDatabase", database);
        SetField(cell, "ingredientCapacity", 10);
        SetField(cell, "exporterCapacity", 2);
        CellConnectionVerifierEditor.ConfigureCraftingPorts(cell, 1, 0);
        grid.InitializeCells(1);
        cell.InitializeSystem();
        Require(cell.AllocateStorage(1, 3, Stone) && cell.AllocateStorage(0, 5, Stone),
            "test materials reserve in configured inputs");
        cell.StoreResource(1, 3);
        cell.StoreResource(0, 5);
        return cell;
    }

    private static async UniTask Wait(Func<bool> predicate)
    {
        for (var i = 0; i < 200; i++)
        {
            if (predicate()) return;
            await UniTask.Delay(20, ignoreTimeScale: true);
        }
        throw new TimeoutException("Crafting state did not reach the expected condition.");
    }

    private static CraftingOperationApplication GetOperation(CrafterCellAdapter cell)
        => (CraftingOperationApplication)typeof(CrafterCellAdapter).GetField("_operation", InstanceFields).GetValue(cell);
    private static Dictionary<int, CrafterCellAdapter.ResourceInputData> GetInputs(CrafterCellAdapter cell)
        => (Dictionary<int, CrafterCellAdapter.ResourceInputData>)typeof(CrafterCellAdapter)
            .GetField("_resourceInputs", InstanceFields).GetValue(cell);
    private static void SetField(object target, string name, object value)
        => target.GetType().GetField(name, InstanceFields).SetValue(target, value);
    private static void Require(bool condition, string message)
    { if (!condition) throw new InvalidOperationException(message); }
}

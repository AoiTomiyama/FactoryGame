#if UNITY_EDITOR
using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;

[TestFixture,Category("Progression.Speed")]
public sealed class CraftingSpeedPlayModeTests : FactoryPlayModeScope
{
    private const BindingFlags Fields=BindingFlags.Instance|BindingFlags.NonPublic;
    [UnityTest] public IEnumerator BasicCraftingKeepsStartedTimeAndRestoresItAfterSpeedPurchase() => Verify(false).ToCoroutine();
    [UnityTest] public IEnumerator AdvancedCraftingKeepsStartedTimeAndRestoresItAfterSpeedPurchase() => Verify(true).ToCoroutine();
    private static async UniTask Verify(bool advanced)
    {
        var grid = new GameObject("SpeedGrid").AddComponent<GridFieldDatabaseAdapter>(); grid.InitializeCells(12);
        var name = advanced ? "AdvancedCrafter" : "Crafter";
        var prefab = AssetDatabase.LoadAssetAtPath<CrafterCellAdapter>("Assets/Prefabs/Field/General/F_"+name+"Cell.prefab");
        var originalDb = (RecipeDatabaseDefinition)typeof(CrafterCellAdapter).GetField("recipeDatabase",Fields).GetValue(prefab);
        var recipe = JsonUtility.FromJson<RecipeDataDefinition>(JsonUtility.ToJson(originalDb.recipes.First(r => r != null && r.Ingredients.Select(i => i.resourceType).Distinct().Count() <= prefab.Ports.Count(p => (p.Use&CellPortUseDomain.Input)!=0) && r.Ingredients.GroupBy(i => i.resourceType).All(g => g.Sum(i => i.requiredAmount)*2 <= prefab.IngredientCapacity) && r.ResultAmount*2 <= prefab.ExporterCapacity)));
        JsonUtility.FromJsonOverwrite("{\"craftSecond\":1}",recipe);
        var db = ScriptableObject.CreateInstance<RecipeDatabaseDefinition>(); db.recipes = new[] {recipe};
        try
        {
            var catalog = AssetDatabase.LoadAssetAtPath<SkillTreeDefinition>("Assets/Scripts/ScriptableObject/SkillTree.asset").CreateCatalog();
            var deliveries = new DeliveryProgressApplication(AssetDatabase.LoadAssetAtPath<DeliveryGoalsDefinition>("Assets/Scripts/ScriptableObject/DeliveryGoals.asset").CreateCatalog());
            var wallet = new CurrencyWalletApplication(10000); var skills = new SkillTreeApplication(catalog,wallet,grid.StatEffects,deliveries);
            Assert.That(skills.Purchase("basic-research"),Is.EqualTo(SkillPurchaseResultApplication.Success));
            CrafterCellAdapter Create()
            {
                var cell = Object.Instantiate(prefab,new Vector3(2,0,2),Quaternion.identity,grid.transform);
                typeof(CrafterCellAdapter).GetField("recipeDatabase",Fields).SetValue(cell,db);
                grid.SaveCell(2,2,cell); return cell;
            }
            var first = Create(); first.InitializeSystem();
            var ports = first.Ports.Where(p => (p.Use&CellPortUseDomain.Input)!=0).OrderBy(p => p.Id).ToArray();
            var ingredients = recipe.Ingredients.GroupBy(i => i.resourceType).Select(g => (type:g.Key,amount:g.Sum(i => i.requiredAmount)*2)).ToArray();
            for(var index=0;index<ingredients.Length;index++)
            {
                var ingredient = ingredients[index];
                Assert.That(first.AllocateStorage(ports[index].Id,ingredient.amount,ingredient.type)); first.StoreResource(ports[index].Id,ingredient.amount);
            }
            await UniTask.WaitUntil(() => first.ElapsedProcessTime>.05f).Timeout(TimeSpan.FromSeconds(5));
            var uid = advanced ? "advanced-crafter-speed-1" : "crafter-speed-1";
            Assert.That(skills.Purchase(uid),Is.EqualTo(SkillPurchaseResultApplication.Success));
            Assert.That(first.ProcessTime,Is.EqualTo(1),"started cycle keeps its original time");
            var saved = new SaveGameApplication.Cell {inventory=first.CaptureInventory(),batches=Array.Empty<SaveGameApplication.Slot>(),job=first.CaptureJob()};
            Assert.That(saved.job.recipeSeconds,Is.EqualTo(1)); first.ValidateSavedState(saved,grid.StatEffects);
            var remaining = saved.job.seconds-saved.job.elapsed;
            first.OnDisconnect(); first.gameObject.SetActive(false); Object.Destroy(first.gameObject);
            var restored = Create(); restored.ValidateSavedState(saved,grid.StatEffects); restored.RestoreState(saved); restored.InitializeSystem();
            await UniTask.WaitUntil(() => restored.ProcessTime>0).Timeout(TimeSpan.FromSeconds(5));
            Assert.That(restored.ProcessTime,Is.EqualTo(1));
            Assert.That(restored.ProcessTime-restored.ElapsedProcessTime,Is.LessThanOrEqualTo(remaining+.05f));
            await UniTask.WaitUntil(() => restored.ExportStorageAmount>=recipe.ResultAmount && restored.ProcessTime>0).Timeout(TimeSpan.FromSeconds(5));
            Assert.That(restored.ProcessTime,Is.EqualTo(.8f).Within(.00001f),"next cycle uses the purchased fractional speed");
            Assert.That(grid.StatEffects.GetModifier(advanced?CellTypeDomain.Crafter:CellTypeDomain.AdvancedCrafter,EquipmentStatDomain.CraftingSpeed).Multiplier,Is.EqualTo(1));
            restored.OnDisconnect(); restored.gameObject.SetActive(false);
        }
        finally { Object.Destroy(db); }
    }
}
#endif

#if UNITY_EDITOR
using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

[TestFixture,Category("Progression.Speed")]
public sealed class SpeedSkillTests
{
    [Test] public void SpeedKeepsFractionalTimeAndRejectsAdditionsDecreasesAndUnderflow()
    {
        Assert.That(EffectiveStatsRulesDomain.CalculateDuration(10,new StatModifierDomain(1.25m,0)),Is.EqualTo(8));
        Assert.That(EffectiveStatsRulesDomain.CalculateDuration(10,new StatModifierDomain(1.5m,0)),Is.EqualTo(10f/1.5f));
        Assert.That(EffectiveStatsRulesDomain.CalculateDuration(0,new StatModifierDomain(2,0)),Is.Zero);
        Assert.Throws<ArgumentOutOfRangeException>(() => EffectiveStatsRulesDomain.CalculateDuration(10,new StatModifierDomain(1,1)));
        Assert.Throws<ArgumentOutOfRangeException>(() => EffectiveStatsRulesDomain.CalculateDuration(10,new StatModifierDomain(.5m,0)));
        Assert.Throws<ArgumentOutOfRangeException>(() => EffectiveStatsRulesDomain.CalculateDuration(float.Epsilon,new StatModifierDomain(int.MaxValue,0)));
        var effects = new SharedStatEffectsApplication();
        effects.RegisterDurationBase(CellTypeDomain.Crafter,EquipmentStatDomain.CraftingSpeed,10);
        var catalog = new SkillTreeCatalogDomain(new[] {
            new SkillNodeDomain("speed","Speed",1,null,new[] {CellTypeDomain.Crafter},EquipmentStatDomain.CraftingSpeed,1.25m) });
        var skills = new SkillTreeApplication(catalog,new CurrencyWalletApplication(1),effects);
        Assert.That(skills.Purchase("speed"),Is.EqualTo(SkillPurchaseResultApplication.Success));
        Assert.That(effects.CanApply(CellTypeDomain.Crafter,EquipmentStatDomain.CraftingSpeed,new StatModifierDomain(1,0)),Is.False);
        Assert.That(effects.GetModifier(CellTypeDomain.AdvancedCrafter,EquipmentStatDomain.CraftingSpeed).Multiplier,Is.EqualTo(1));
    }
    [Serializable] private sealed class LegacyEnvelope { public int version = 1; public string payload,sha256; }
    [Test] public void VersionOneCraftingProgressIsUpgradedOnceWithoutChangingItsRemainingTime()
    {
        var data = new SaveGameApplication {formatVersion=1,revision=1,savedUtc=DateTime.UtcNow.ToString("O"),size=1,
            construction=Array.Empty<SaveGameApplication.Stock>(),deliveries=Array.Empty<SaveGameApplication.Stock>(),
            skills=Array.Empty<string>(),skillRules=Array.Empty<string>(),definitions=Array.Empty<SaveGameApplication.Definition>(),
            cells=new[] {new SaveGameApplication.Cell {kind=SaveKeysApplication.Key(CellTypeDomain.Crafter),
                inventory=Array.Empty<SaveGameApplication.Slot>(),batches=Array.Empty<SaveGameApplication.Slot>(),
                job=new SaveGameApplication.Job {seconds=10,elapsed=3,amount=1,result="plank",inputs=Array.Empty<SaveGameApplication.Slot>()}}}};
        var payload = JsonUtility.ToJson(data);
        using var sha = SHA256.Create();
        var json = JsonUtility.ToJson(new LegacyEnvelope {payload=payload,sha256=BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(payload))).Replace("-","").ToLowerInvariant()});
        var upgraded = SaveJsonAdapter.Decode(json);
        Assert.That(upgraded.formatVersion,Is.EqualTo(SaveGameApplication.CurrentFormat));
        Assert.That(upgraded.cells[0].job.recipeSeconds,Is.EqualTo(10));
        Assert.That(upgraded.cells[0].job.seconds-upgraded.cells[0].job.elapsed,Is.EqualTo(7));
        var loadedAgain = SaveJsonAdapter.Decode(SaveJsonAdapter.Encode(upgraded));
        Assert.That(loadedAgain.cells[0].job.seconds-loadedAgain.cells[0].job.elapsed,Is.EqualTo(7));
    }
    [Test] public void SavedCraftingSpeedNodesKeepBaseAndAdvancedBranchesIndependent()
    {
        var catalog = AssetDatabase.LoadAssetAtPath<SkillTreeDefinition>("Assets/Scripts/ScriptableObject/SkillTree.asset").CreateCatalog();
        foreach(var prefix in new[] {"crafter-speed","advanced-crafter-speed"})
        {
            var chain = catalog.Nodes.Where(n => n.Uid.StartsWith(prefix+"-")).ToArray();
            Assert.That(chain.Length,Is.EqualTo(3));
            Assert.That(chain.Select(n => n.Modifier.Multiplier),Is.EqualTo(new[] {1.25m,1.5m,2m}));
            Assert.That(chain.Select(n => n.Price),Is.EqualTo(new long[] {100,200,400}));
        }
    }
    [Test] public void TransportNodeUpdatesAllThreeKindsAndRestoresThemWithoutAnotherPayment()
    {
        var kinds = new[] {CellTypeDomain.Conveyor,CellTypeDomain.ExportConveyor,CellTypeDomain.Crossing};
        var node = new SkillNodeDomain("transport","Transport",100,null,kinds,EquipmentStatDomain.TransportSpeed,1.5m);
        kinds[0] = CellTypeDomain.Storage;
        var effects = new SharedStatEffectsApplication(); var wallet = new CurrencyWalletApplication(100);
        var catalog = new SkillTreeCatalogDomain(new[] {node}); var skills = new SkillTreeApplication(catalog,wallet,effects);
        var notifications = 0; var observersSawAllKinds = false;
        effects.Changed += () => { notifications++; observersSawAllKinds = node.TargetCellTypes.All(t => effects.GetModifier(t,EquipmentStatDomain.TransportSpeed).Multiplier==1.5m); };
        Assert.That(skills.Purchase(node.Uid),Is.EqualTo(SkillPurchaseResultApplication.Success));
        Assert.That(observersSawAllKinds); Assert.That(notifications,Is.EqualTo(1)); Assert.That(wallet.Current.Balance,Is.Zero);
        Assert.That(node.TargetCellTypes.Contains(CellTypeDomain.Storage),Is.False);
        var restoredEffects = new SharedStatEffectsApplication();
        new SkillTreeApplication(catalog,wallet,restoredEffects).RestoreOwned(skills.CaptureOwned());
        foreach(var type in node.TargetCellTypes) Assert.That(restoredEffects.GetModifier(type,EquipmentStatDomain.TransportSpeed).Multiplier,Is.EqualTo(1.5m));
        Assert.That(wallet.Current.Balance,Is.Zero);
    }
    [Test] public void InvalidOneOfMultipleTargetsPreventsEveryEffectAndThePayment()
    {
        var effects = new SharedStatEffectsApplication();
        effects.RegisterDurationBase(CellTypeDomain.Crossing,EquipmentStatDomain.TransportSpeed,float.Epsilon);
        var kinds = new[] {CellTypeDomain.Conveyor,CellTypeDomain.ExportConveyor,CellTypeDomain.Crossing};
        var catalog = new SkillTreeCatalogDomain(new[] {new SkillNodeDomain("transport","Transport",100,null,kinds,EquipmentStatDomain.TransportSpeed,2)});
        var wallet = new CurrencyWalletApplication(100); var skills = new SkillTreeApplication(catalog,wallet,effects);
        Assert.That(skills.Purchase("transport"),Is.EqualTo(SkillPurchaseResultApplication.InvalidEffect));
        foreach(var type in kinds) Assert.That(effects.GetModifier(type,EquipmentStatDomain.TransportSpeed).Multiplier,Is.EqualTo(1));
        Assert.That(wallet.Current.Balance,Is.EqualTo(100)); Assert.That(wallet.Current.ReservedExpense,Is.Zero);
    }
    [Test] public void PartiallyOverlappingTargetListsRequireTheSamePrerequisiteChain()
    {
        var first = new SkillNodeDomain("a","A",100,null,new[] {CellTypeDomain.Conveyor,CellTypeDomain.ExportConveyor},EquipmentStatDomain.TransportSpeed,1.25m);
        var second = new SkillNodeDomain("b","B",100,null,new[] {CellTypeDomain.ExportConveyor,CellTypeDomain.Crossing},EquipmentStatDomain.TransportSpeed,1.5m);
        Assert.Throws<ArgumentException>(() => new SkillTreeCatalogDomain(new[] {first,second}));
        var chained = new SkillNodeDomain("b","B",100,"a",second.TargetCellTypes,EquipmentStatDomain.TransportSpeed,1.5m);
        Assert.DoesNotThrow(() => new SkillTreeCatalogDomain(new[] {first,chained}));
    }
}
#endif

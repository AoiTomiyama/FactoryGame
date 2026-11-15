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
            new SkillNodeDomain("speed","Speed",1,null,CellTypeDomain.Crafter,EquipmentStatDomain.CraftingSpeed,1.25m) });
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
}
#endif

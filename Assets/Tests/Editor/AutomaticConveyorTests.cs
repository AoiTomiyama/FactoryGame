#if UNITY_EDITOR
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

[TestFixture,Category("AutomaticConveyor")]
public sealed class AutomaticConveyorTests : FactoryEditModeScope
{
    [TestCase(0)] [TestCase(90)] [TestCase(180)] [TestCase(270)]
    public void OnlyTheContactedOutputPortQualifies(int angle)
    {
        var grid = TestScenes.NewObject("ConveyorRuleGrid").AddComponent<GridFieldDatabaseAdapter>();
        var crafter = TestScenes.Clone(AssetDatabase.LoadAssetAtPath<CrafterCellAdapter>("Assets/Prefabs/Field/General/F_AdvancedCrafterCell.prefab"),
            new Vector3(8,0,8),Quaternion.Euler(0,angle,0),grid.transform);
        grid.InitializeCells(18);
        foreach(var port in crafter.Ports)
        {
            var position = Vector3Int.RoundToInt(crafter.PortPosition(port.Id) + crafter.PortDirection(port.Id));
            var rotation = Quaternion.LookRotation(crafter.PortDirection(port.Id));
            Assert.That(ConveyorCellAdapter.HasExtractionSource(grid,position.x,position.z,rotation),
                Is.EqualTo((port.Use & CellPortUseDomain.Output)!=0),"接したポート " + port.Id);
            Assert.That(ConveyorCellAdapter.HasExtractionSource(grid,position.x,position.z,rotation*Quaternion.Euler(0,180,0)),Is.False);
        }
        Assert.That(ConveyorCellAdapter.HasExtractionSource(grid,0,0,Quaternion.identity),Is.False);
    }
}
#endif

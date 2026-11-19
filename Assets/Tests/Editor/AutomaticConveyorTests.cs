#if UNITY_EDITOR
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

[TestFixture,Category("AutomaticConveyor")]
public sealed class AutomaticConveyorTests : FactoryEditModeScope
{
    [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)]
    public void RouteShapeUsesExistingDirectionAndDragEndpoints(int turns)
    {
        var input = ConveyorRouteRulesDomain.Rotate(DirectionsDomain.Back, turns);
        var output = ConveyorRouteRulesDomain.Rotate(DirectionsDomain.Forward, turns);
        var left = ConveyorRouteRulesDomain.Rotate(DirectionsDomain.Left, turns);
        var right = ConveyorRouteRulesDomain.Rotate(DirectionsDomain.Right, turns);
        var straight = new ConveyorRouteRulesDomain.Route(input, output);
        var crossing = ConveyorRouteRulesDomain.Extend(straight, left, right);
        Assert.That(crossing.Type, Is.EqualTo(ConveyorRouteRulesDomain.Kind.Crossing));
        Assert.That(crossing.Inputs, Is.EqualTo(input | left));
        Assert.That(crossing.Outputs, Is.EqualTo(output | right));
        var split = ConveyorRouteRulesDomain.Extend(straight, 0, right);
        Assert.That(split.Type, Is.EqualTo(ConveyorRouteRulesDomain.Kind.Split));
        Assert.That(ConveyorRouteRulesDomain.Extend(split, 0, left).Outputs, Is.EqualTo(output | left | right));
        var merge = ConveyorRouteRulesDomain.Extend(straight, left, 0);
        Assert.That(merge.Type, Is.EqualTo(ConveyorRouteRulesDomain.Kind.Merge));
        Assert.That(ConveyorRouteRulesDomain.Extend(merge, 0, right).Type, Is.EqualTo(ConveyorRouteRulesDomain.Kind.Crossing));
        Assert.That(ConveyorRouteRulesDomain.Extend(straight, output, input).Type, Is.EqualTo(ConveyorRouteRulesDomain.Kind.Invalid));
        Assert.That(new ConveyorRouteRulesDomain.Route(input | output, left | right).Type, Is.EqualTo(ConveyorRouteRulesDomain.Kind.Invalid));
    }

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

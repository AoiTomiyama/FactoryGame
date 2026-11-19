#if UNITY_EDITOR
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

[TestFixture,Category("AutomaticConveyor")]
public sealed class AutomaticConveyorTests : FactoryEditModeScope
{
    [TestCase("Assets/Prefabs/Field/General/F_CrafterCell.prefab")]
    [TestCase("Assets/Prefabs/Placeholder/General/P_CrafterCell.prefab")]
    public void BasicCrafterReusesConveyorArrowsAtBothPorts(string path)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        var model = prefab.transform.Find("Model/CrafterCellModel");
        Assert.That(model, Is.Not.Null);
        var input = model.Find("InputArrow");
        var output = model.Find("OutputArrow");
        Assert.That(input, Is.Not.Null);
        Assert.That(output, Is.Not.Null);
        Assert.That(input.localPosition.z, Is.LessThan(0));
        Assert.That(output.localPosition.z, Is.GreaterThan(0));
        foreach (var arrow in new[] { input, output })
        {
            Assert.That(arrow.GetComponentsInChildren<MeshRenderer>().Length, Is.EqualTo(2));
            Assert.That(Vector3.Dot(model.forward, arrow.forward), Is.EqualTo(1).Within(.001f));
        }
    }

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

        var line = new[] { (3, 4, default(ConveyorRouteRulesDomain.Route)), (4, 4, straight), (5, 4, default) };
        var replacement = ConveyorRouteRulesDomain.Plan(line, right).Replacements;
        Assert.That(replacement.Count, Is.EqualTo(1));
        Assert.That((replacement[0].X, replacement[0].Z), Is.EqualTo((4, 4)));
        var parallel = ConveyorRouteRulesDomain.Plan(line, output);
        Assert.That(parallel.Overlap);
        Assert.That(parallel.Invalid, Is.False);
        Assert.That(parallel.Replacements, Is.Empty);
        Assert.That(ConveyorRouteRulesDomain.Plan(line, input).Invalid);
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

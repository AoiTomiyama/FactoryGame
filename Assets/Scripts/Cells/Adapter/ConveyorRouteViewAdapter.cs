using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>確定した入口と出口をセル上の矢印として表示する。</summary>
public sealed class ConveyorRouteViewAdapter : MonoBehaviour
{
    private static readonly (DirectionsDomain Side, Vector3 Direction)[] Sides =
    {
        (DirectionsDomain.Forward, Vector3.forward), (DirectionsDomain.Back, Vector3.back),
        (DirectionsDomain.Right, Vector3.right), (DirectionsDomain.Left, Vector3.left)
    };
    private Mesh _mesh;
    private DirectionsDomain _inputs, _outputs;

    internal static void Show(Transform root, ConveyorRouteRulesDomain.Route route, Material material)
    {
        if (material == null) return;
        var child = root.Find("RouteArrows");
        if (child == null)
        {
            var view = new GameObject("RouteArrows", typeof(MeshFilter), typeof(MeshRenderer), typeof(ConveyorRouteViewAdapter));
            child = view.transform;
            child.SetParent(root, false);
            var renderer = view.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
        }
        child.GetComponent<ConveyorRouteViewAdapter>().SetRoute(route);
    }

    private void SetRoute(ConveyorRouteRulesDomain.Route route)
    {
        if (_inputs == route.Inputs && _outputs == route.Outputs) return;
        _inputs = route.Inputs;
        _outputs = route.Outputs;
        var vertices = new List<Vector3>();
        var triangles = new List<int>();
        foreach (var (side, direction) in Sides)
        {
            var input = (_inputs & side) != 0;
            if (!input && (_outputs & side) == 0) continue;
            var height = route.Type == ConveyorRouteRulesDomain.Kind.Crossing
                ? (direction.x == 0 ? .92f : 1.39f) : .51f;
            var origin = Vector3.up * height;
            var start = origin + direction * (input ? .46f : .07f);
            var tip = origin + direction * (input ? .07f : .46f);
            var travel = input ? -direction : direction;
            var lateral = Vector3.Cross(Vector3.up, travel);
            var head = tip - travel * .13f;
            var index = vertices.Count;
            vertices.AddRange(new[] { start - lateral * .042f, head - lateral * .042f,
                head + lateral * .042f, start + lateral * .042f, head - lateral * .095f, tip, head + lateral * .095f });
            triangles.AddRange(new[] { index, index + 1, index + 2, index, index + 2, index + 3,
                index + 4, index + 5, index + 6 });
        }
        if (_mesh == null)
        {
            _mesh = new Mesh { name = "Conveyor route arrows" };
            GetComponent<MeshFilter>().sharedMesh = _mesh;
        }
        _mesh.Clear();
        _mesh.SetVertices(vertices);
        _mesh.SetTriangles(triangles, 0);
        _mesh.RecalculateNormals();
    }

    private void OnDestroy() { if (_mesh != null) Destroy(_mesh); }
}

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
    private Transform[] _beltArms;
    private Transform[] _beltSurfaces;

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

    internal static void ShowBelt(Transform root, ConveyorRouteRulesDomain.Route route, Transform model, Transform surface)
    {
        var child = root.Find("JunctionBelt");
        if (child == null)
        {
            var view = new GameObject("JunctionBelt", typeof(ConveyorRouteViewAdapter));
            child = view.transform;
            child.SetParent(root, false);
        }
        child.GetComponent<ConveyorRouteViewAdapter>().SetBelt(route, model, surface);
    }

    private void SetBelt(ConveyorRouteRulesDomain.Route route, Transform model, Transform surface)
    {
        if (_inputs == route.Inputs && _outputs == route.Outputs) return;
        _inputs = route.Inputs;
        _outputs = route.Outputs;
        var surfaceScale = surface.localScale;
        var halfWidth = surfaceScale.x * .5f;
        var rimLength = .5f - halfWidth;
        var rimCenter = halfWidth + rimLength * .5f;
        const float arrowCenter = .28f;
        if (_beltArms == null)
        {
            var center = Instantiate(surface, transform, false);
            center.name = "BeltCenter";
            center.localScale = new Vector3(surfaceScale.x, surfaceScale.y, surfaceScale.x);

            // 同じPrefabのベルト面と矢印を使い、中央と外周の面が重ならないように分ける。
            _beltArms = new Transform[Sides.Length];
            _beltSurfaces = new Transform[Sides.Length];
            for (var i = 0; i < Sides.Length; i++)
            {
                var arm = _beltArms[i] = Instantiate(model, transform, false);
                arm.name = $"{Sides[i].Side}Belt";
                arm.localPosition = Sides[i].Direction * arrowCenter;
                var rim = _beltSurfaces[i] = arm.GetChild(surface.GetSiblingIndex());
                rim.localScale = new Vector3(surfaceScale.x, surfaceScale.y, rimLength);
                foreach (Transform marker in arm)
                {
                    if (marker == rim) continue;
                    var scale = new Vector3(.54f, .7f, .54f);
                    marker.localPosition = new Vector3(marker.localPosition.x * scale.x,
                        marker.localPosition.y, marker.localPosition.z * scale.z);
                    marker.localScale = Vector3.Scale(marker.localScale, scale);
                }
            }
        }
        for (var i = 0; i < Sides.Length; i++)
        {
            var (side, direction) = Sides[i];
            var arm = _beltArms[i];
            var input = (_inputs & side) != 0;
            arm.gameObject.SetActive(input || (_outputs & side) != 0);
            arm.localRotation = Quaternion.LookRotation(input ? -direction : direction);
            _beltSurfaces[i].localPosition = surface.localPosition + Vector3.forward * ((rimCenter - arrowCenter) * (input ? -1 : 1));
        }
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

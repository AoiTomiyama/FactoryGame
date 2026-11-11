#if UNITY_EDITOR
using System;
using System.Reflection;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>配置、回転、削除、再接続時の4方向の相互参照と通知を検証する。</summary>
public static class CellConnectionAssertions
{
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly FieldInfo OnConnected =
        typeof(ConnectableCellBaseAdapter).GetField("OnGetConnectedCell", PrivateInstance);
    private static readonly FieldInfo OnLost =
        typeof(ConnectableCellBaseAdapter).GetField("OnLostConnectedCell", PrivateInstance);

    public static void VerifyConnections()
    {



        var gridObject = TestScenes.NewObject("ConnectionCheck:Grid");
        try
        {
            var grid = gridObject.AddComponent<GridFieldDatabaseAdapter>(); TestScenes.UseGrid(grid);
            for (var x = 0; x < 3; x++)
            {
                var row = TestScenes.NewObject($"Row_{x}").transform;
                row.SetParent(gridObject.transform);
                for (var z = 0; z < 3; z++) CreateCell<EmptyCellAdapter>(row, x, z, Quaternion.identity);
            }
            grid.InitializeCells(3);

            var center = ReplaceWith<StorageCellAdapter>(grid, 1, 1);
            var connected = 0;
            var lost = 0;
            OnConnected.SetValue(center, (Action<ConnectableCellBaseAdapter.Connection>)(connection => connected++));
            OnLost.SetValue(center, (Action<CellBaseAdapter>)(cell => lost++));
            center.InitializeSystem();

            var east = ReplaceWith<StorageCellAdapter>(grid, 2, 1);
            var eastConnected = 0;
            OnConnected.SetValue(east, (Action<ConnectableCellBaseAdapter.Connection>)(connection => eastConnected++));
            east.InitializeSystem();
            var west = ReplaceWith<StorageCellAdapter>(grid, 0, 1);
            west.InitializeSystem();
            var north = ReplaceWith<StorageCellAdapter>(grid, 1, 2);
            north.InitializeSystem();
            var south = ReplaceWith<StorageCellAdapter>(grid, 1, 0);
            south.InitializeSystem();

            Require(connected == 4 && eastConnected == 1, "connection notifications are sent once to both cells");
            Require(Link(center, 0) == east && Link(east, 1) == center &&
                    Link(center, 1) == west && Link(west, 0) == center &&
                    Link(center, 2) == north && Link(north, 3) == center &&
                    Link(center, 3) == south && Link(south, 2) == center,
                "four direction slots and reciprocal links match");

            north.OnDisconnect();
            Require(Link(center, 2) == null && Link(north, 3) == null && lost == 1,
                "deletion clears both links and notifies once");
            ReplaceWith<EmptyCellAdapter>(grid, 1, 2);
            var replacement = ReplaceWith<StorageCellAdapter>(grid, 1, 2);
            replacement.InitializeSystem();
            Require(Link(center, 2) == replacement && Link(replacement, 3) == center && connected == 5,
                "replacement reconnects and notifies once");

            center.OnDisconnect();
            Require(Link(east, 1) == null && Link(west, 0) == null &&
                    Link(south, 2) == null && Link(replacement, 3) == null,
                "deletion clears every reciprocal link");
            var rotated = ReplaceWith<StorageCellAdapter>(grid, 1, 1, Quaternion.Euler(0, 90, 0));
            SetPorts(rotated, new CellPortDefinition(2, 0, 0, DirectionsDomain.Forward, CellPortUseDomain.Both));
            rotated.InitializeSystem();
            Require(Link(rotated, 0) == east && Link(east, 1) == rotated &&
                    Link(rotated, 1) == null && Link(rotated, 2) == null && Link(rotated, 3) == null,
                "rotated forward connects only to the east");

            var corner = ReplaceWith<StorageCellAdapter>(grid, 0, 0);
            corner.InitializeSystem();
            Require(Link(corner, 1) == null && Link(corner, 3) == null,
                "grid corner does not read outside the field");
            Debug.Log("Cell connection checks passed: four directions, reciprocal notifications, deletion, reconnection, rotation, boundary.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(gridObject);
        }
    }

    public static void VerifyFootprints()
    {
        foreach (var shape in new[] { new CellFootprintDomain(1, 1), new CellFootprintDomain(3, 2), new CellFootprintDomain(5, 5, -2, -2) })
        for (var turn = 0; turn < 4; turn++)
        {
            var points = shape.Coordinates(6, 6, turn).ToArray();
            Require(points.Distinct().Count() == shape.Area, "rotation preserves distinct occupied tiles");
            var width = points.Max(p => p.x) - points.Min(p => p.x) + 1;
            var depth = points.Max(p => p.z) - points.Min(p => p.z) + 1;
            Require(width == (turn % 2 == 0 ? shape.Width : shape.Depth) &&
                depth == (turn % 2 == 0 ? shape.Depth : shape.Width), "rotated bounds match dimensions");
            foreach (var point in shape.Coordinates(0, 0, turn))
            {
                var restored = CellFootprintDomain.Rotate(point.x, point.z, -turn);
                Require(shape.Coordinates(0, 0, 0).Contains(restored), "rotation can be reversed");
            }
            var cells = new object[13, 13];
            var owner = new object();
            Require(GridOccupancyApplication.TryReplace(cells, points, owner, old => old == null, out var snapshot), "whole footprint registers");
            Require(points.All(p => ReferenceEquals(cells[p.x, p.z], owner)), "all tiles return one owner");
            Require(!GridOccupancyApplication.TryReplace(cells, points, new object(), old => old == null, out _), "overlap rejected");
            Require(!GridOccupancyApplication.TryReplace(cells, shape.Coordinates(13, 13, turn), owner, old => true, out _), "boundary rejected atomically");
            Require(points.All(p => ReferenceEquals(cells[p.x, p.z], owner)), "failed registration preserves every tile");
            GridOccupancyApplication.Restore(cells, snapshot);
            Require(points.All(p => cells[p.x, p.z] == null), "whole footprint restores");
        }

        var root = TestScenes.NewObject("FootprintCheck");
        try
        {
            var grid = root.AddComponent<GridFieldDatabaseAdapter>(); TestScenes.UseGrid(grid);
            // 異なる順序・階層で保存した設備も、座標から一度ずつ登録する。
            var large = CreateCell<EmptyCellAdapter>(root.transform, 3, 3, Quaternion.Euler(0, 90, 0));
            var settings = new SerializedObject(large);
            settings.FindProperty("footprintWidth").intValue = 3;
            settings.FindProperty("footprintDepth").intValue = 2;
            settings.ApplyModifiedPropertiesWithoutUndo();
            var small = CreateCell<EmptyCellAdapter>(root.transform, 0, 0, Quaternion.identity);
            small.transform.SetAsFirstSibling();
            grid.InitializeCells(8);
            Require(grid.Owners.Count() == 2 && large.OccupiedCoordinates.All(p => grid.GetCell(p.x, p.z) == large), "hierarchy order does not duplicate owner registration");
            Require(large.XIndex == 3 && large.ZIndex == 3, "registration preserves anchor");
            Require(!grid.TryRegisterCell(3, 3, small, old => true, out _), "partial owner overwrite rejected");
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
        Debug.Log("Footprint checks passed: 1x1/3x2/5x5, four rotations, atomic overlap/boundary/restore, owner lookup, unordered hierarchy.");
    }

    public static void VerifyExteriorPorts()
    {
        for (var turn = 0; turn < 4; turn++)
        {

            var root = TestScenes.NewObject("PortCheck");
            try
            {
                var grid = root.AddComponent<GridFieldDatabaseAdapter>(); TestScenes.UseGrid(grid);
                for (var x = 0; x < 10; x++)
                for (var z = 0; z < 10; z++) CreateCell<EmptyCellAdapter>(root.transform, x, z, Quaternion.identity);
                grid.InitializeCells(10);
                var large = CreateCell<StorageCellAdapter>(root.transform, 4, 4, Quaternion.Euler(0, turn * 90, 0));
                typeof(StorageCellAdapter).GetField("capacity", PrivateInstance).SetValue(large, 10);
                var settings = new SerializedObject(large);
                settings.FindProperty("footprintWidth").intValue = 3;
                settings.FindProperty("footprintDepth").intValue = 2;
                settings.ApplyModifiedPropertiesWithoutUndo();
                SetPorts(large, new CellPortDefinition(10, 0, 0, DirectionsDomain.Back, CellPortUseDomain.Input),
                    new CellPortDefinition(20, 2, 0, DirectionsDomain.Back, CellPortUseDomain.Input),
                    new CellPortDefinition(30, 1, 1, DirectionsDomain.Forward, CellPortUseDomain.Output));
                var previous = grid.CaptureCells(large.Footprint.Coordinates(4, 4, turn));
                grid.SaveCell(4, 4, large);
                foreach (var entry in previous) UnityEngine.Object.DestroyImmediate(entry.Owner.gameObject);
                large.InitializeSystem();
                foreach (var id in new[] { 10, 20, 30 })
                {
                    var point = Vector3Int.RoundToInt(large.PortPosition(id)) + large.PortDirection(id);
                    ReplaceWith<StorageCellAdapter>(grid, point.x, point.z).InitializeSystem();
                }
                Require(large.Connections.Count == 3 && large.Connections.ContainsKey(10) && large.Connections.ContainsKey(20),
                    "same edge has independent stable port IDs after rotation");
                Require(large.Connections[10].Neighbor != large.Connections[20].Neighbor, "same-facing ports reach separate owners");
                var neighbors = large.Connections.Values.Select(c => c.Neighbor).ToArray();
                large.OnDisconnect();
                Require(neighbors.All(c => c.Connections.Count == 0), "multiblock removal clears every reciprocal port");
                SetPorts(large, new CellPortDefinition(0, 1, 0, DirectionsDomain.Right, CellPortUseDomain.Input));
                var rejected = false;
                try { large.ValidatePorts(); } catch (InvalidOperationException) { rejected = true; }
                Require(rejected, "interior-facing ports are rejected");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }
        Debug.Log("Exterior port checks passed: same-edge independent IDs, 3x2 at four rotations, reciprocal removal, invalid interior face.");
    }

    private static T ReplaceWith<T>(GridFieldDatabaseAdapter grid, int x, int z,
        Quaternion? rotation = null) where T : CellBaseAdapter
    {
        var old = grid.GetCell(x, z);
        var parent = old.transform.parent;
        var sibling = old.transform.GetSiblingIndex();
        UnityEngine.Object.DestroyImmediate(old.gameObject);
        var cell = CreateCell<T>(parent, x, z, rotation ?? Quaternion.identity);
        if (cell is StorageCellAdapter)
            typeof(StorageCellAdapter).GetField("capacity", PrivateInstance).SetValue(cell, 10);
        cell.transform.SetSiblingIndex(sibling);
        grid.SaveCell(x, z, cell);
        return cell;
    }

    private static T CreateCell<T>(Transform parent, int x, int z, Quaternion rotation)
        where T : CellBaseAdapter
    {
        var root = TestScenes.NewObject($"Cell_{x}_{z}");
        root.transform.SetParent(parent);
        root.transform.SetPositionAndRotation(new Vector3(x, 0, z), rotation);
        TestScenes.NewObject("Model").transform.SetParent(root.transform);
        return root.AddComponent<T>();
    }

    private static CellBaseAdapter Link(ConnectableCellBaseAdapter cell, int direction) =>
        cell.Connections.Values.FirstOrDefault(c => c.Direction == new[] { Vector3Int.right, Vector3Int.left, Vector3Int.forward, Vector3Int.back }[direction])?.Neighbor;

    public static void SetPorts(ConnectableCellBaseAdapter cell, params CellPortDefinition[] ports)
        => typeof(ConnectableCellBaseAdapter).GetField("ports", PrivateInstance).SetValue(cell, ports);

    public static void ConfigureCraftingPorts(CrafterCellAdapter cell, params int[] inputIds)
    {
        var ports = inputIds.Select(id => new CellPortDefinition(id, 0, 0,
            id == 1 ? DirectionsDomain.Left : DirectionsDomain.Right, CellPortUseDomain.Input)).ToList();
        ports.Add(new CellPortDefinition(2, 0, 0, DirectionsDomain.Forward, CellPortUseDomain.Output));
        SetPorts(cell, ports.ToArray());
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}

#endif

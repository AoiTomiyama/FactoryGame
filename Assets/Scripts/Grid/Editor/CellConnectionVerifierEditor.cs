using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>配置、回転、削除、再接続時の4方向の相互参照と通知を検証する。</summary>
public static class CellConnectionVerifierEditor
{
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly FieldInfo AllowedDirections =
        typeof(ConnectableCellBaseAdapter).GetField("allowedDirections", PrivateInstance);
    private static readonly FieldInfo OnConnected =
        typeof(ConnectableCellBaseAdapter).GetField("OnGetConnectedCell", PrivateInstance);
    private static readonly FieldInfo OnLost =
        typeof(ConnectableCellBaseAdapter).GetField("OnLostConnectedCell", PrivateInstance);
    private static readonly PropertyInfo Adjacent =
        typeof(ConnectableCellBaseAdapter).GetProperty("AdjacentCells", PrivateInstance);

    [MenuItem("Tools/FactoryGame/Verify Cell Connections")]
    public static void Run()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var gridObject = new GameObject("ConnectionCheck:Grid");
        try
        {
            var grid = gridObject.AddComponent<GridFieldDatabaseAdapter>();
            for (var x = 0; x < 3; x++)
            {
                var row = new GameObject($"Row_{x}").transform;
                row.SetParent(gridObject.transform);
                for (var z = 0; z < 3; z++) CreateCell<EmptyCellAdapter>(row, x, z, Quaternion.identity);
            }
            grid.InitializeCells(3);

            var center = ReplaceWith<StorageCellAdapter>(grid, 1, 1);
            var connected = 0;
            var lost = 0;
            OnConnected.SetValue(center, (Action<Vector3Int, CellBaseAdapter>)((direction, cell) => connected++));
            OnLost.SetValue(center, (Action<CellBaseAdapter>)(cell => lost++));
            center.InitializeSystem();

            var east = ReplaceWith<StorageCellAdapter>(grid, 2, 1);
            var eastConnected = 0;
            OnConnected.SetValue(east, (Action<Vector3Int, CellBaseAdapter>)((direction, cell) => eastConnected++));
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
            AllowedDirections.SetValue(rotated, DirectionsDomain.Forward);
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

    private static T ReplaceWith<T>(GridFieldDatabaseAdapter grid, int x, int z,
        Quaternion? rotation = null) where T : CellBaseAdapter
    {
        var old = grid.GetCell(x, z);
        var parent = old.transform.parent;
        var sibling = old.transform.GetSiblingIndex();
        UnityEngine.Object.DestroyImmediate(old.gameObject);
        var cell = CreateCell<T>(parent, x, z, rotation ?? Quaternion.identity);
        cell.transform.SetSiblingIndex(sibling);
        grid.SaveCell(x, z, cell);
        return cell;
    }

    private static T CreateCell<T>(Transform parent, int x, int z, Quaternion rotation)
        where T : CellBaseAdapter
    {
        var root = new GameObject($"Cell_{x}_{z}");
        root.transform.SetParent(parent);
        root.transform.SetPositionAndRotation(new Vector3(x, 0, z), rotation);
        new GameObject("Model").transform.SetParent(root.transform);
        return root.AddComponent<T>();
    }

    private static CellBaseAdapter Link(ConnectableCellBaseAdapter cell, int direction) =>
        ((CellBaseAdapter[])Adjacent.GetValue(cell))[direction];

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}

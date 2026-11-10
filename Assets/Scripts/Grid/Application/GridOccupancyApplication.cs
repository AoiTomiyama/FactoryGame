using System;
using System.Collections.Generic;

/// <summary>全占有マスを確認してから書き換え、失敗前の所有者を復元可能にする。</summary>
public static class GridOccupancyApplication
{
    public readonly struct Entry<T> where T : class
    {
        public readonly int X, Z;
        public readonly T Owner;
        public Entry(int x, int z, T owner) { X = x; Z = z; Owner = owner; }
    }

    public static bool TryReplace<T>(T[,] grid, IEnumerable<(int x, int z)> coordinates,
        T owner, Predicate<T> canReplace, out Entry<T>[] previous) where T : class
    {
        previous = null;
        if (grid == null || owner == null || canReplace == null) return false;
        var entries = new List<Entry<T>>();
        var visited = new HashSet<(int, int)>();
        foreach (var (x, z) in coordinates)
        {
            if (x < 0 || z < 0 || x >= grid.GetLength(0) || z >= grid.GetLength(1) ||
                !visited.Add((x, z)) || !canReplace(grid[x, z])) return false;
            entries.Add(new Entry<T>(x, z, grid[x, z]));
        }
        if (entries.Count == 0) return false;
        previous = entries.ToArray();
        foreach (var entry in previous) grid[entry.X, entry.Z] = owner;
        return true;
    }

    public static void Restore<T>(T[,] grid, IEnumerable<Entry<T>> previous) where T : class
    {
        foreach (var entry in previous) grid[entry.X, entry.Z] = entry.Owner;
    }
}

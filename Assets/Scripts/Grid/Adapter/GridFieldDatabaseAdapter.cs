using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public sealed class GridFieldDatabaseAdapter : SingletonMonoBehaviourAdapter<GridFieldDatabaseAdapter>
{
    [SerializeField, InspectorReadOnlyAttributeAdapter] private int generatedSize;
    [SerializeField, InspectorReadOnlyAttributeAdapter] private int generationSeed;
    public int GeneratedSize => generatedSize;
    public int GenerationSeed => generationSeed;

    // 入力設定の編集と、保存済み地形の生成結果を区別する。
    public void RecordGeneratedField(int size, int seed)
    {
        if (size <= 0) throw new ArgumentOutOfRangeException(nameof(size));
        generatedSize = size; generationSeed = seed;
    }
    public void InvalidateGeneratedField() { generatedSize = 0; generationSeed = 0; }

    public StartSiteRulesDomain.Terrain[,] CaptureTerrain()
    {
        if (generatedSize <= 0) throw new InvalidOperationException("Field has no completed generation.");
        var map = new StartSiteRulesDomain.Terrain[generatedSize, generatedSize];
        var occupied = new HashSet<(int x, int z)>();
        foreach (var cell in GetComponentsInChildren<CellBaseAdapter>())
        {
            var anchor = Vector3Int.RoundToInt(cell.transform.position);
            foreach (var p in cell.Footprint.Coordinates(anchor.x, anchor.z, cell.QuarterTurns))
            {
                if (p.x < 0 || p.z < 0 || p.x >= generatedSize || p.z >= generatedSize || !occupied.Add(p))
                    throw new InvalidOperationException("Saved field has invalid occupancy.");
                if (cell is EmptyCellAdapter) map[p.x, p.z] = new StartSiteRulesDomain.Terrain(true);
                else if (cell is ResourceCellAdapter raw) map[p.x, p.z] = new StartSiteRulesDomain.Terrain(false, raw.ResourceTypeDomain);
            }
        }
        if (occupied.Count != checked(generatedSize * generatedSize))
            throw new InvalidOperationException("Saved field is incomplete.");
        return map;
    }

    private CellBaseAdapter[,] _gridCells;
    private int _gridSize;
    public SharedStatEffectsApplication StatEffects { get; private set; } = new();

    /// <summary>
    /// 配列の初期化とセルの保存を行う
    /// </summary>
    public void InitializeCells(int size)
    {
        // 引数のチェック
        if (size <= 0)
        {
            Debug.LogError("サイズは1以上の整数でなければなりません。");
            return;
        }

        StatEffects = new SharedStatEffectsApplication();
        _gridCells = new CellBaseAdapter[size, size];
        _gridSize = size;

        // 名前や子の順序ではなく、設備の位置と定義から一度だけ登録する。
        foreach (var cell in GetComponentsInChildren<CellBaseAdapter>())
        {
            var anchor = Vector3Int.RoundToInt(cell.transform.position);
            if (!TryRegisterCell(anchor.x, anchor.z, cell, previous => previous == null, out _))
                throw new InvalidOperationException($"Invalid initial occupancy: {cell.name}");
        }
    }

    /// <summary>
    /// セルを配列に保存する
    /// </summary>
    public void SaveCell(int x, int z, CellBaseAdapter cellBase)
    {
        // 配列の初期化チェック
        if (_gridCells == null)
        {
            Debug.LogError($"{nameof(_gridCells)}が未割り当てです。");
            return;
        }

        // 引数のチェック
        if (cellBase == null)
        {
            Debug.LogError($"引数{nameof(cellBase)}がnullです。");
            return;
        }

        // 範囲外チェック
        if (IsOutOfRange(x, z))
        {
            Debug.LogError($"({x}, {z}) は範囲外です。");
            return;
        }

        var current = _gridCells[x, z];
        if (!TryRegisterCell(x, z, cellBase, old => old == null || old is EmptyCellAdapter || old == current,
                out _)) throw new InvalidOperationException("Cell footprint registration failed.");
    }

    public IEnumerable<CellBaseAdapter> Owners => _gridCells.Cast<CellBaseAdapter>().Where(c => c != null).Distinct();

    public bool TryRegisterCell(int x, int z, CellBaseAdapter cell, Predicate<CellBaseAdapter> canReplace,
        out GridOccupancyApplication.Entry<CellBaseAdapter>[] previous)
    {
        previous = null;
        var coordinates = cell.Footprint.Coordinates(x, z, cell.QuarterTurns).ToHashSet();
        foreach (var point in coordinates)
        {
            if (!IsWithinBounds(point.x, point.z)) return false;
            var old = _gridCells[point.x, point.z];
            // 大きい設備の一角だけを上書きして、本体の占有を分断させない。
            if (old != null && old.OccupiedCoordinates.Any(p => !coordinates.Contains(p))) return false;
        }
        // 能力値の検証も書き込み前に済ませ、登録途中の例外で部分占有を残さない。
        cell.ConfigureProgression(StatEffects);
        if (!GridOccupancyApplication.TryReplace(_gridCells, coordinates,
                cell, canReplace, out previous)) return false;
        cell.XIndex = x; cell.ZIndex = z;
        return true;
    }

    public void RestoreCells(GridOccupancyApplication.Entry<CellBaseAdapter>[] previous)
        => GridOccupancyApplication.Restore(_gridCells, previous);

    public GridOccupancyApplication.Entry<CellBaseAdapter>[] CaptureCells(IEnumerable<(int x, int z)> coordinates)
        => coordinates.Select(p => new GridOccupancyApplication.Entry<CellBaseAdapter>(p.x, p.z, GetCell(p.x, p.z))).ToArray();

    public bool TryReplaceCells(CellBaseAdapter[] replacements, GridOccupancyApplication.Entry<CellBaseAdapter>[] expected)
    {
        var entries = new Dictionary<(int x, int z), CellBaseAdapter>();
        foreach (var cell in replacements)
        {
            var anchor = Vector3Int.RoundToInt(cell.transform.position);
            foreach (var point in cell.Footprint.Coordinates(anchor.x, anchor.z, cell.QuarterTurns))
                if (!IsWithinBounds(point.x, point.z) || !entries.TryAdd(point, cell)) return false;
        }
        if (entries.Count != expected.Length || expected.Any(e => !entries.ContainsKey((e.X, e.Z)) ||
                GetCell(e.X, e.Z) != e.Owner)) return false;
        foreach (var owner in expected.Select(e => e.Owner).Where(c => c != null).Distinct())
            if (owner.OccupiedCoordinates.Any(p => !entries.ContainsKey(p))) return false;
        foreach (var cell in replacements) cell.ConfigureProgression(StatEffects);
        if (expected.Any(e => GetCell(e.X, e.Z) != e.Owner)) return false;
        // 全設備の構成と占有を検証した後にまとめて書き込む。
        foreach (var cell in replacements)
        {
            var anchor = Vector3Int.RoundToInt(cell.transform.position);
            cell.XIndex = anchor.x; cell.ZIndex = anchor.z;
        }
        foreach (var entry in entries) _gridCells[entry.Key.x, entry.Key.z] = entry.Value;
        return true;
    }

    /// <summary>
    /// 指定した座標のセルを取得する
    /// </summary>
    public CellBaseAdapter GetCell(int x, int z)
    {
        // 範囲外チェック
        if (IsOutOfRange(x, z))
        {
            Debug.LogError($"({x}, {z}) は範囲外です。");
            return null;
        }

        return _gridCells[x, z];
    }

    /// <summary>隣接セルを読む前に、グリッド端と未初期化を判定する。</summary>
    public bool IsWithinBounds(int x, int z) => _gridCells != null && !IsOutOfRange(x, z);

    /// <summary>
    /// 指定した座標を中心に、マンハッタン距離 `range` 以内に存在する型 `T` のセルを検索する。
    /// 条件を満たすセルが見つかった場合は `cellBase` に代入し`true` を返し、それ以外は `false` を返す。
    /// </summary>
    /// <param name="x">中心となるX座標</param>
    /// <param name="z">中心となるZ座標</param>
    /// <param name="range">検索するマンハッタン距離</param>
    /// <param name="cellBase">見つかったセル（型T）</param>
    /// <param name="excludingList">あらかじめ検索から除外するリスト</param>
    /// <typeparam name="T">検索するセルの型（CellBaseの派生型）</typeparam>
    /// <returns>条件を満たすセルが見つかったかどうか</returns>
    public bool TryGetCellFromRange<T>(int x, int z, int range, out T cellBase, List<T> excludingList = null)
        where T : CellBaseAdapter
    {
        cellBase = null;
        // 範囲外チェック
        if (IsOutOfRange(x, z))
        {
            Debug.LogError($"({x}, {z}) は範囲外です。");
            return false;
        }

        var visited = new bool[_gridSize][];
        for (int i = 0; i < _gridSize; i++)
        {
            visited[i] = new bool[_gridSize];
        }

        // BFS（幅優先探索）を使用して、マンハッタン距離 range 以内のセルを探索
        var queue = new Queue<(int x, int z, int dist)>();
        queue.Enqueue((x, z, 0));
        visited[x][z] = true;
        var directions = new (int dx, int dz)[]
        {
            (1, 0), (-1, 0), (0, 1), (0, -1)
        };

        while (queue.Count > 0)
        {
            var (centerX, centerZ, dist) = queue.Dequeue();
            if (dist >= range) continue;

            foreach (var (dirX, dirZ) in directions)
            {
                var nextX = centerX + dirX;
                var nextZ = centerZ + dirZ;

                if (IsOutOfRange(nextX, nextZ))
                    continue;
                if (visited[nextX][nextZ])
                    continue;
                
                // セルが型Tで、かつ除外リストに含まれていない場合は、cellBaseに代入してtrueを返す
                if (_gridCells[nextX, nextZ] is T selectedCell &&
                    (excludingList == null || !excludingList.Contains(selectedCell)))
                {
                    cellBase = selectedCell;
                    return true;
                }

                visited[nextX][nextZ] = true;
                queue.Enqueue((nextX, nextZ, dist + 1));
            }
        }

        return false;
    }

    private bool IsOutOfRange(int x, int z) => x < 0 || z < 0 || x >= _gridSize || z >= _gridSize;
}

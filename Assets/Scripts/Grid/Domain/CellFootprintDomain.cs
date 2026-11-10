using System;
using System.Collections.Generic;

/// <summary>整数の基準マスを中心に回転する長方形。描画や階層から独立して占有を計算する。</summary>
public readonly struct CellFootprintDomain
{
    public int Width { get; }
    public int Depth { get; }
    public int OffsetX { get; }
    public int OffsetZ { get; }
    public int Area => checked(Width * Depth);

    public CellFootprintDomain(int width, int depth, int offsetX = 0, int offsetZ = 0)
    {
        if (width <= 0 || depth <= 0) throw new ArgumentOutOfRangeException(nameof(width));
        Width = width; Depth = depth; OffsetX = offsetX; OffsetZ = offsetZ;
    }

    public static (int x, int z) Rotate(int x, int z, int quarterTurns)
        => ((quarterTurns % 4 + 4) % 4) switch
        {
            0 => (x, z), 1 => (z, -x), 2 => (-x, -z), _ => (-z, x)
        };

    public IEnumerable<(int x, int z)> Coordinates(int anchorX, int anchorZ, int quarterTurns)
    {
        for (var x = 0; x < Width; x++)
        for (var z = 0; z < Depth; z++)
        {
            var offset = Rotate(OffsetX + x, OffsetZ + z, quarterTurns);
            yield return (anchorX + offset.x, anchorZ + offset.z);
        }
    }
}

using System;
using UnityEngine;

/// <summary>Prefab内の局所座標と、回転しても変わらない接点ID。</summary>
[Serializable]
public struct CellPortDefinition
{
    public int Id;
    public Vector2Int Cell;
    public DirectionsDomain Facing;
    public CellPortUseDomain Use;
    public CellPortDefinition(int id, int x, int z, DirectionsDomain facing, CellPortUseDomain use)
    { Id = id; Cell = new Vector2Int(x, z); Facing = facing; Use = use; }
}

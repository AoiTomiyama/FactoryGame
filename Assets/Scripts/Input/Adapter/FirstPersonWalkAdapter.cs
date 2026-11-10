using System.Linq;
using UnityEngine;

/// <summary>平らなセル床での散策。設備の占有を使い、モデル用Colliderを増やさずに進路を判定する。</summary>
public sealed class FirstPersonWalkAdapter
{
    private readonly GridFieldDatabaseAdapter _grid;
    private readonly float _eyeHeight, _speed, _sensitivity;
    private float _yaw, _pitch;
    private const float Radius = .2f;
    public Vector3 Position { get; private set; }
    public Quaternion Rotation => Quaternion.Euler(_pitch, _yaw, 0);

    public FirstPersonWalkAdapter(GridFieldDatabaseAdapter grid, float eyeHeight, float speed, float sensitivity)
    { _grid = grid; _eyeHeight = eyeHeight; _speed = speed; _sensitivity = sensitivity; }

    public bool TryStart(Vector3 focus)
    {
        var floor = _grid.Owners.Where(Walkable).OrderBy(c => (c.WorldCenter - focus).sqrMagnitude)
            .ThenBy(c => c.XIndex).ThenBy(c => c.ZIndex).FirstOrDefault();
        if (floor == null) return false;
        Position = new Vector3(floor.XIndex, FloorHeight(floor) + _eyeHeight, floor.ZIndex);
        var direction = focus - Position;
        _yaw = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
        _pitch = 0;
        return true;
    }

    public void Step(Vector2 movement, Vector2 look, bool fast, float seconds)
    {
        _yaw = Mathf.Repeat(_yaw + look.x * _sensitivity, 360);
        _pitch = Mathf.Clamp(_pitch - look.y * _sensitivity, -80, 80);
        var input = Vector2.ClampMagnitude(movement, 1);
        // 長いフレームでも設備1マスを飛び越えない距離へ抑える。
        var delta = Quaternion.Euler(0, _yaw, 0) * new Vector3(input.x, 0, input.y) *
            (_speed * (fast ? 2 : 1) * Mathf.Clamp(seconds, 0, .05f));
        var next = Position;
        if (CanStandAt(next + Vector3.right * delta.x)) next.x += delta.x;
        if (CanStandAt(next + Vector3.forward * delta.z)) next.z += delta.z;
        var floor = _grid.GetCell(Mathf.RoundToInt(next.x), Mathf.RoundToInt(next.z));
        next.y = FloorHeight(floor) + _eyeHeight;
        Position = next;
    }

    public bool CanStandAt(Vector3 position)
    {
        foreach (var x in new[] { position.x - Radius, position.x + Radius })
        foreach (var z in new[] { position.z - Radius, position.z + Radius })
            if (!_grid.IsWithinBounds(Mathf.RoundToInt(x), Mathf.RoundToInt(z)) ||
                !Walkable(_grid.GetCell(Mathf.RoundToInt(x), Mathf.RoundToInt(z)))) return false;
        return true;
    }

    private static bool Walkable(CellBaseAdapter cell)
        => cell != null && cell.isActiveAndEnabled && cell is EmptyCellAdapter or ConveyorCellAdapter or CrossingCellAdapter;
    private static float FloorHeight(CellBaseAdapter cell)
        => cell.GetComponent<BoxCollider>().bounds.max.y;
}

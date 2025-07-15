using UnityEngine;

public static class CalculationExtensions 
{
    /// <summary>
    /// Vector3を正規化し、最も近い方向（前後左右）に変換します。
    /// </summary>
    /// <param name="dir"></param>
    /// <returns></returns>
    public static Vector3Int ToCardinalDirection(this Vector3 dir)
    {
        dir = dir.normalized;

        if (Mathf.Abs(dir.x) > Mathf.Abs(dir.z))
            return dir.x > 0 ? Vector3Int.right : Vector3Int.left;
        else
            return dir.z > 0 ? Vector3Int.forward : Vector3Int.back;
    }
}
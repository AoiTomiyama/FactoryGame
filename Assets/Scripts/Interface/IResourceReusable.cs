using UnityEngine;

public interface IResourceReusable
{
    /// <summary>
    /// リソースIDを再利用します。
    /// </summary>
    /// <param name="dir">アクセスされた方向</param>
    /// <param name="id">再利用するリソースID</param>
    public void Reuse(Vector3Int dir, int id);
}
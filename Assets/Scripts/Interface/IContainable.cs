using UnityEngine;

public interface IContainable
{
    /// <summary>
    /// リソースの搬入を予約します。
    /// </summary>
    /// <param name="dir">アクセスされた入力方向</param>
    /// <param name="amount">予約する量</param>
    /// <param name="resourceType">リソースの種類</param>
    /// <returns>予約に成功した量</returns>
    public int AllocateStorage(Vector3Int dir, int amount, ResourceType resourceType);

    /// <summary>
    /// ストレージにリソースを追加します。入りきらなかった分は戻り値として返される
    /// </summary>
    /// <param name="dir">アクセスされた入力方向</param>
    /// <param name="amount">ストレージに入れる量</param>
    public void StoreResource(Vector3Int dir, int amount);
}

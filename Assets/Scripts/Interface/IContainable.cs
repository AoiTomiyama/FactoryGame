using UnityEngine;

public interface IContainable
{
    /// <summary>
    /// 指定量のリソースの搬入を全量予約します。
    /// 成功時は amount と同量を確保し、失敗時は状態を変更しません。
    /// </summary>
    /// <param name="dir">アクセスされた入力方向</param>
    /// <param name="amount">予約する正の量</param>
    /// <param name="resourceType">None 以外のリソースの種類</param>
    /// <returns>指定量の全量を予約できた場合のみ true</returns>
    public bool AllocateStorage(Vector3Int dir, int amount, ResourceType resourceType);

    /// <summary>
    /// 事前に予約した量のリソースを確定します。
    /// </summary>
    /// <param name="dir">アクセスされた入力方向</param>
    /// <param name="amount">ストレージに入れる量</param>
    public void StoreResource(Vector3Int dir, int amount);
}

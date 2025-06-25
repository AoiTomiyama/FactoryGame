public interface IContainable
{
    public ResourceType StoredResourceType { get; }
    /// <summary>
    /// ストレージにリソースを追加します。入りきらなかった分は戻り値として返される
    /// </summary>
    /// <param name="amount">ストレージに入れる量</param>
    /// <param name="resourceType">リソースの種類</param>
    /// <returns>ストレージに入りきらなかった量</returns>
    public int StoreResource(int amount, ResourceType resourceType);

    /// <summary>
    /// リソースの搬入を予約します。
    /// </summary>
    /// <param name="amount">予約する量</param>
    /// <returns>予約に成功した量</returns>
    public int AllocateStorage(int amount);
    
    /// <summary>
    /// 容量上限に達しているかどうかを確認。
    /// </summary>
    public bool IsFull();
}

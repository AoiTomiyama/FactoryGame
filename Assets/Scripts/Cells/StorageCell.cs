using UnityEngine;

public class StorageCell : CellBase
{
    [SerializeField] [InspectorReadOnly] private int currentLoad;
    [SerializeField] private int capacity;

    private ResourceType _storedResourceType = ResourceType.None;
    public int Capacity => capacity;
    public int CurrentLoad => currentLoad;

    /// <summary>
    /// ストレージにリソースを追加します。入りきらなかった分は戻り値として返される
    /// </summary>
    /// <param name="amount">ストレージに入れる量</param>
    /// <param name="resourceType">リソースの種類</param>
    /// <returns>ストレージに入りきらなかった猟</returns>
    public int StoreResource(int amount, ResourceType resourceType)
    {
        // 初めてのリソース追加
        if (_storedResourceType == ResourceType.None)
        {
            _storedResourceType = resourceType;
        }

        if (_storedResourceType != resourceType)
        {
            // 設定済みのリソースタイプと異なる場合
            Debug.LogWarning(
                "ストレージセルのリソースタイプが一致しません。" +
                $"現在のリソースタイプ: {_storedResourceType}, " +
                $"追加しようとしたリソースタイプ: {resourceType}");

            // 追加できないので、全量を戻す
            return amount;
        }

        if (currentLoad + amount > capacity)
        {
            Debug.LogWarning("ストレージセルの容量を超えています。" +
                             $"現在の容量: {currentLoad}, " +
                             $"追加しようとした量: {amount}");

            // 容量を超えないように調整
            var overflow = currentLoad + amount - capacity;
            currentLoad = capacity;
            return overflow;
        }

        currentLoad += amount;
        return 0;
    }

    /// <summary>
    /// ストレージからリソースを取り出します。取り出せる量は現在の容量に依存する
    /// </summary>
    /// <param name="amount">取り出す要求値</param>
    /// <param name="resourceType">取り出すリソースの種類</param>
    /// <returns>取り出しに成功した量</returns>
    public int TakeResource(int amount, ResourceType resourceType)
    {
        if (_storedResourceType != resourceType)
        {
            Debug.LogWarning(
                "ストレージセルのリソースタイプが一致しません。" +
                $"現在のリソースタイプ: {_storedResourceType}, " +
                $"取り出そうとしたリソースタイプ: {resourceType}");

            // 取り出せないので、0を返す
            return 0;
        }

        if (currentLoad - amount >= 0)
        {
            currentLoad -= amount;
            if (currentLoad == 0)
            {
                // 取り出した後に容量が0になった場合、リソースタイプをリセット
                _storedResourceType = ResourceType.None;
            }

            return amount;
        }

        Debug.LogWarning("ストレージセルの現在の容量が不足しています。" +
                         $"現在の容量: {currentLoad}, " +
                         $"削除しようとした量: {amount}");

        var takenAmount = currentLoad;
        currentLoad = 0;
        // リソースを全部取り出した後は、リソースタイプをリセット
        _storedResourceType = ResourceType.None;

        // 取り出せる量は現在の容量まで
        return takenAmount;
    }
}
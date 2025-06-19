using System.Collections;
using UnityEngine;

public class ExtractorCell : CellBase
{
    [SerializeField] private ResourceType resourceType;
    [SerializeField] private int extractionSecond;
    [SerializeField] private int extractionAmount;
    [SerializeField] private int extractionCapacity;

    private CellBase _forwardCell;
    private StorageCell[] _adjacentStorageCells;
    private int _currentExtractedAmount;

    private void Start()
    {
        _forwardCell = GridFieldDatabase.Instance.GetCell(
            XIndex + Mathf.RoundToInt(transform.forward.x),
            ZIndex + Mathf.RoundToInt(transform.forward.z));

        const int adjacentCount = 3;
        _adjacentStorageCells = new StorageCell[adjacentCount];
        // 周囲のストレージセルを取得
        for (int i = 0; i < adjacentCount; i++)
        {
            _ = GridFieldDatabase.Instance.TryGetCellFromRange(XIndex, ZIndex, 1, out _adjacentStorageCells[i]);
        }

        if (_forwardCell == null) return;
        StartCoroutine(ExtractEnumerator());
    }

    private IEnumerator ExtractEnumerator()
    {
        while (true)
        {
            // ストレージに保存できる容量があるか確認
            if (_currentExtractedAmount < extractionCapacity)
            {
                Extract();
            }
            else
            {
#if UNITY_EDITOR
                Debug.Log($"容量上限に到達: {_currentExtractedAmount}");
#endif
                // 容量上限に達した場合はスペースが空くまで待機
                yield return new WaitUntil(HasStorageCapacity);
                OutputResources();
            }

            yield return new WaitForSeconds(extractionSecond);
        }
    }

    /// <summary>
    /// 周囲のストレージセルが見つかり、かつ容量に空きがあるかを確認する
    /// </summary>
    /// <returns></returns>
    private bool HasStorageCapacity()
    {
        for (var i = 0; i < _adjacentStorageCells.Length; i++)
        {
            var storage = _adjacentStorageCells[i];

            // 既にストレージセルが見つかっていて、容量に空きがある場合はtrueを返す
            // もしくは、ストレージセルがない場合、周囲のセルを再度検索し、
            // 未発見の容量に空きがあるストレージ見つかった場合はtrueを返す
            if (storage != null)
            {
                if (!storage.IsFull())
                {
                    return true;
                }

                // 既にストレージセルが見つかっていて、容量がいっぱいの場合は次のセルを探す
                continue;
            }

            // ストレージセルが見つからない場合、周囲のセルを検索
            if (!GridFieldDatabase.Instance.TryGetCellFromRange(XIndex, ZIndex, 1, out storage,
                    _adjacentStorageCells) || storage.IsFull()) continue;

            // ストレージセルが見つかった場合、配列に保存
            _adjacentStorageCells[i] = storage;
            return true;
        }

        return false;
    }

    private void Extract()
    {
        // 前方のセルがリソースセルで、指定されたリソースタイプと一致する場合
        if (_forwardCell is ResourceCell resourceCell &&
            resourceCell.ResourceType == resourceType)
        {
            _currentExtractedAmount += extractionAmount;
#if UNITY_EDITOR
            Debug.Log("生産: " + _currentExtractedAmount + "/" + extractionCapacity);
#endif
        }

        OutputResources();
    }

    private void OutputResources()
    {
        // ストレージセルがない場合、処理を終了
        if (!HasStorageCapacity()) return;

        // ストレージセルにリソースを保存
        foreach (var storage in _adjacentStorageCells)
        {
            if (storage == null || storage.IsFull())
            {
                continue;
            }

            // ストレージセルに保存できる量がある場合は、保存する
            var output = storage.StoreResource(_currentExtractedAmount, resourceType);
            _currentExtractedAmount = 0;

            // ストレージに保存できなかった分は戻す
            _currentExtractedAmount += (output > 0) ? output : 0;
        }
    }
}
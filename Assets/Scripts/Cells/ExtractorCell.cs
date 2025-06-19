using System;
using System.Collections;
using UnityEngine;

public class ExtractorCell : CellBase
{
    [SerializeField] private ResourceType resourceType;
    [SerializeField] private int extractionSecond;
    [SerializeField] private int extractionAmount;
    [SerializeField] private int extractionCapacity;

    public ResourceType ResourceType => resourceType;
    public int ExtractionSecond => extractionSecond;

    private CellBase _forwardCell;
    private int _currentExtractedAmount;

    private void Start()
    {
        _forwardCell = GridFieldDatabase.Instance.GetCell(
            XIndex + Mathf.RoundToInt(transform.forward.x),
            ZIndex + Mathf.RoundToInt(transform.forward.z));
        
        if (_forwardCell == null) return;
        StartCoroutine(ExtractEnumerator());
    }

    private IEnumerator ExtractEnumerator()
    {
        while (true)
        {
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
                yield return new WaitUntil(() => _currentExtractedAmount < extractionCapacity);
            }

            yield return new WaitForSeconds(extractionSecond);
        }
    }

    private void Extract()
    {
        if (_forwardCell.TryGetComponent<ResourceCell>(out var resourceCell) &&
            resourceCell.ResourceType == resourceType)
        {
            _currentExtractedAmount += extractionAmount;
#if UNITY_EDITOR
            Debug.Log("生産: " + _currentExtractedAmount + "/" + extractionCapacity);
#endif
        }
    }
}
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

public class CellStatusView : MonoBehaviour
{
    [SerializeField] private StatusRowInfo[] statusRowPrefabs;
    [SerializeField] private Transform statsRowContainer;
    [SerializeField] private int defaultPoolCapacity = 100;
    [SerializeField] private int maxPoolCapacity = 500;

    private readonly Dictionary<UIStatusRowType, ObjectPool<UIStatusRowBase>> _statsRowUIPool = new();
    private readonly Queue<(UIStatusRowType, UIStatusRowBase)> _activeStatsRows = new();

    private void Start()
    {
        if (statusRowPrefabs == null || statusRowPrefabs.Length == 0)
        {
            Debug.LogError("StatsRowPrefabs is not assigned or empty.");
            return;
        }

        foreach (var info in statusRowPrefabs)
        {
            if (info.RowType == UIStatusRowType.None) continue;
            if (info.Prefab == null)
            {
                Debug.LogError($"Prefab for {info.RowType} is null.");
                continue;
            }

            _statsRowUIPool[info.RowType] = new(
                createFunc: () => Instantiate(info.Prefab, transform),
                actionOnGet: statsRow => statsRow.gameObject.SetActive(false),
                actionOnRelease: statsRow => statsRow.gameObject.SetActive(true),
                actionOnDestroy: statsRow => Destroy(statsRow.gameObject),
                collectionCheck: true,
                defaultCapacity: defaultPoolCapacity,
                maxSize: maxPoolCapacity
            );
        }
    }

    public UIStatusRowBase CreateStatusRow(UIStatusRowType statsRowType, UIElementDataBase data)
    {
        if (!_statsRowUIPool.TryGetValue(statsRowType, out var pool))
        {
            Debug.LogError($"No pool found for {statsRowType}");
            return null;
        }

        var rowUI = pool.Get();
        rowUI.RenderUIByData(data);

        _activeStatsRows.Enqueue((statsRowType, rowUI));
        return rowUI;
    }

    public void ResetStatusUI()
    {
        foreach (var (statsRowType, rowUI) in _activeStatsRows)
        {
            _statsRowUIPool[statsRowType].Release(rowUI);
        }
    }
}

[Serializable]
public struct StatusRowInfo
{
    [SerializeField] private UIStatusRowType rowType;
    [SerializeField] private UIStatusRowBase prefab;

    public UIStatusRowType RowType => rowType;

    public UIStatusRowBase Prefab => prefab;
}
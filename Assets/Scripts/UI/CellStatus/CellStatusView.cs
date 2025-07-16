using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

public class CellStatusView : SingletonMonoBehaviour<CellStatusView>
{
    [SerializeField] private Transform statusRowContainer;
    [SerializeField] private Transform elementWindowTransform;
    [SerializeField] private Transform selectionMarker;
    [SerializeField] private StatusRowInfo[] statusRowPrefabs;
    [SerializeField] private int defaultPoolCapacity = 100;
    [SerializeField] private int maxPoolCapacity = 500;
    
    private IDataProvidable _renderingCell;

    private readonly Dictionary<UIStatusRowType, ObjectPool<UIStatusRowBase>> _statusRowUIPool = new();
    private readonly Stack<(UIStatusRowType, UIStatusRowBase)> _activeStatusRows = new();
    private UIElementRenderer _uiRenderer;

    protected override void Awake()
    {
        base.Awake();
        _uiRenderer = new();
    }

    private void Start()
    {
        InitializeUIElementPool();
    }

    private void InitializeUIElementPool()
    {
        if (statusRowPrefabs == null || statusRowPrefabs.Length == 0)
        {
#if UNITY_EDITOR
            Debug.LogError("StatsRowPrefabs is not assigned or empty.");
#endif
            return;
        }

        foreach (var info in statusRowPrefabs)
        {
            if (info.RowType == UIStatusRowType.None) continue;
            if (info.Prefab == null)
            {
#if UNITY_EDITOR
                Debug.LogError($"Prefab for {info.RowType} is null.");
#endif
                continue;
            }

            _statusRowUIPool[info.RowType] = new(
                createFunc: () => Instantiate(info.Prefab, statusRowContainer),
                actionOnGet: statsRow => statsRow.gameObject.SetActive(true),
                actionOnRelease: statsRow => statsRow.gameObject.SetActive(false),
                actionOnDestroy: statsRow => Destroy(statsRow.gameObject),
                collectionCheck: true,
                defaultCapacity: defaultPoolCapacity,
                maxSize: maxPoolCapacity
            );
        }
    }

    public void UpdateUI() => _uiRenderer.UpdateUI();

    public void SetStatusWindowActive(bool isActive)
    {
        if (elementWindowTransform != null)
        {
            elementWindowTransform.gameObject.SetActive(isActive);
        }
    }

    public UIStatusRowBase CreateStatusRow(UIElementDataBase data)
    {
        if (!_statusRowUIPool.TryGetValue(data.UIStatusRowType, out var pool))
        {
#if UNITY_EDITOR
            Debug.LogError($"No pool found for {data.UIStatusRowType}");
#endif
            return null;
        }

        var rowUI = pool.Get();
        rowUI.transform.SetAsLastSibling();
        rowUI.RenderUIByData(data);

        _activeStatusRows.Push((data.UIStatusRowType, rowUI));
        return rowUI;
    }
    
    public void UpdateUIStatusWindow(CellBase selectedCell)
    {
        // 選択中のセルがインターフェイスを持たない場合
        if (selectedCell is not IDataProvidable renderingCell)
        {
            DeactivateCurrentStatus();
            SetStatusWindowActive(false);
            return;
        }

        // ステータスUIを表示
        SetStatusWindowActive(true);

        // 現在のセルが選択されているセルと同じ場合は何もしない
        if (_renderingCell == renderingCell) return;

        DeactivateCurrentStatus();

        // 新しいセルを設定
        _renderingCell = renderingCell;
        _renderingCell.IsUIActive = true;

        // データプロバイダーを取得・設定
        var provider = _renderingCell.GetDataProvider();
        provider.SwitchSystem(renderingCell);
        _uiRenderer.InitUI(provider);

        // マーカーの位置を更新
        selectionMarker.transform.position = selectedCell.transform.position;
    }

    /// <summary>
    /// 現在のステータスを非アクティブにする
    /// </summary>
    private void DeactivateCurrentStatus()
    {
        if (_renderingCell == null) return;

        _renderingCell.IsUIActive = false;
        _renderingCell = null;
        ResetStatusUI();
    }

    private void ResetStatusUI()
    {
        while (_activeStatusRows.Count > 0)
        {
            var (statsRowType, rowUI) = _activeStatusRows.Pop();
            _statusRowUIPool[statsRowType].Release(rowUI);
        }
        _uiRenderer.ResetUI();
    }

    private void OnDestroy()
    {
        foreach (var pool in _statusRowUIPool.Values)
        {
            pool.Clear();
        }

        _statusRowUIPool.Clear();
        _activeStatusRows.Clear();
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
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

public class CellStatusViewAdapter : SingletonMonoBehaviourAdapter<CellStatusViewAdapter>
{
    [SerializeField] private Transform statusRowContainer;
    [SerializeField] private Transform elementWindowTransform;
    [SerializeField] private Transform selectionMarker;
    [SerializeField] private StatusRowInfoAdapter[] statusRowPrefabs;
    [SerializeField] private int defaultPoolCapacity = 100;
    [SerializeField] private int maxPoolCapacity = 500;
    
    private IDataProvidableApplication _renderingCell;

    private readonly Dictionary<UIStatusRowTypeApplication, ObjectPool<UIStatusRowBaseAdapter>> _statusRowUIPool = new();
    private readonly Stack<(UIStatusRowTypeApplication, UIStatusRowBaseAdapter)> _activeStatusRows = new();
    private readonly UIElementRendererAdapter _uiRenderer = new();

    private void Start()
    {
        SetStatusWindowActive(false);
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
            if (info.RowType == UIStatusRowTypeApplication.None) continue;
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
        
        if (selectionMarker != null)
        {
            selectionMarker.gameObject.SetActive(isActive);
        }
    }

    public UIStatusRowBaseAdapter CreateStatusRow(UIElementDataBaseApplication data)
    {
        if (!_statusRowUIPool.TryGetValue(data.UIStatusRowTypeApplication, out var pool))
        {
#if UNITY_EDITOR
            Debug.LogError($"No pool found for {data.UIStatusRowTypeApplication}");
#endif
            return null;
        }

        var rowUI = pool.Get();
        rowUI.transform.SetAsLastSibling();
        rowUI.RenderUIByData(data);

        _activeStatusRows.Push((data.UIStatusRowTypeApplication, rowUI));
        return rowUI;
    }
    
    public void UpdateUIStatusWindow(CellBaseAdapter selectedCell)
    {
        // 選択中のセルがインターフェイスを持たない場合
        if (selectedCell is not IDataProvidableApplication renderingCell)
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
public struct StatusRowInfoAdapter
{
    [SerializeField] private UIStatusRowTypeApplication rowType;
    [SerializeField] private UIStatusRowBaseAdapter prefab;

    public UIStatusRowTypeApplication RowType => rowType;

    public UIStatusRowBaseAdapter Prefab => prefab;
}
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;
using UnityEngine.UI;

public class CellStatusViewAdapter : SingletonMonoBehaviourAdapter<CellStatusViewAdapter>
{
    [SerializeField] private Transform statusRowContainer;
    [SerializeField] private Transform elementWindowTransform;
    [SerializeField] private Transform selectionMarker;
    [SerializeField] private StatusRowInfoAdapter[] statusRowPrefabs;
    [SerializeField] private int defaultPoolCapacity = 100;
    [SerializeField] private int maxPoolCapacity = 500;
    
    private IDataProvidableApplication _renderingCell;
    private CellBaseAdapter _selectedCell;
    public CellBaseAdapter SelectedCell => _selectedCell;
    public Transform StatusContent => statusRowContainer;
    private bool _integrated;
    private Font _hudFont;
    private ResourceDefinition _hudResources;
    [SerializeField] private HudStatusRowAdapter hudRowPrefab;
    [SerializeField] private ScrollRect statusScroll;
    [SerializeField] private Button closeButton;
    public ScrollRect StatusScroll => statusScroll;
    public Button CloseButton => closeButton;
    public float TopInset { get; set; } = 88;
    private Vector2 _baseMin, _baseMax;

    public void ConfigureHud(Font font, ResourceDefinition resources)
    {
        if (!_integrated)
        {
            var rect = (RectTransform)transform;
            _baseMin = rect.offsetMin; _baseMax = rect.offsetMax;
        }
        _integrated = hudRowPrefab != null;
        _hudFont = font; _hudResources = resources;
        closeButton.onClick.RemoveListener(CloseStatus);
        closeButton.onClick.AddListener(CloseStatus);
    }
    private void CloseStatus() => SetStatusWindowActive(false);
    public event Action<CellBaseAdapter> SelectionChanged;

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
                createFunc: () => CreatePooledRow(info.Prefab),
                actionOnGet: statsRow => statsRow.gameObject.SetActive(true),
                actionOnRelease: statsRow => statsRow.gameObject.SetActive(false),
                actionOnDestroy: statsRow => Destroy(statsRow.gameObject),
                collectionCheck: true,
                defaultCapacity: defaultPoolCapacity,
                maxSize: maxPoolCapacity
            );
        }
    }

    private UIStatusRowBaseAdapter CreatePooledRow(UIStatusRowBaseAdapter prefab)
    {
        if (!_integrated) return Instantiate(prefab, statusRowContainer);
        var adapter = Instantiate(hudRowPrefab, statusRowContainer);
        adapter.Configure(_hudFont, _hudResources);
        return adapter;
    }

    public void UpdateUI() => _uiRenderer.UpdateUI();

    public void SetStatusWindowActive(bool isActive)
    {
        if (!isActive) DeactivateCurrentStatus();
        if (elementWindowTransform != null)
        {
            elementWindowTransform.gameObject.SetActive(isActive);
        }
        
        if (selectionMarker != null)
        {
            selectionMarker.gameObject.SetActive(isActive);
        }
    }
    public void ShowDetails()
    {
        // セルの購読と状態行を返してから、同じ右側枠を別の詳細へ貸し出す。
        DeactivateCurrentStatus();
        elementWindowTransform.gameObject.SetActive(true);
        if (selectionMarker != null) selectionMarker.gameObject.SetActive(false);
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
        if (selectedCell == null)
        {
            SetStatusWindowActive(false);
            return;
        }
        if (_selectedCell == selectedCell) { SetStatusWindowActive(true); UpdateUI(); return; }
        DeactivateCurrentStatus();
        var renderingCell = selectedCell as IDataProvidableApplication;
        var provider = renderingCell?.GetDataProvider();
        if (provider == null && !selectedCell.SupportsProgression) { SetStatusWindowActive(false); return; }
        _selectedCell = selectedCell;
        _renderingCell = renderingCell;
        // 選択中の表示だけが通知を受ける。切替・非表示・削除で購読を解除する。
        if (provider != null)
        {
            _renderingCell.StatusChanged += UpdateUI;
            _uiRenderer.InitUI(_integrated ? new HudStatusProviderAdapter(provider) : provider, CreateStatusRow);
        }
        SelectionChanged?.Invoke(_selectedCell);
        SetStatusWindowActive(true);
        if (selectionMarker != null) selectionMarker.position = selectedCell.transform.position;
    }

    private void LateUpdate()
    {
        // 項目追加による上部枠の高さだけを反映し、Prefabの左右・下余白を保つ。
        if (_integrated && transform is RectTransform window)
        {
            window.offsetMin = _baseMin;
            window.offsetMax = _baseMax - new Vector2(0, TopInset - 88);
        }
        if (!ReferenceEquals(_selectedCell, null) && _selectedCell == null)
            SetStatusWindowActive(false);
    }

    private void OnDisable() => SetStatusWindowActive(false);

    private void DeactivateCurrentStatus()
    {
        if (_renderingCell != null) _renderingCell.StatusChanged -= UpdateUI;
        _renderingCell = null;
        _selectedCell = null;
        SelectionChanged?.Invoke(null);
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
        DeactivateCurrentStatus();
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

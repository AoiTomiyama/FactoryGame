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
    private Transform _originalParent, _originalWindow, _originalContentParent;
    private Vector2 _originalAnchorMin, _originalAnchorMax, _originalPivot, _originalPosition, _originalSize;
    private bool _originalControlHeight, _addedFitter;
    public ScrollRect StatusScroll { get; private set; }
    public Button CloseButton { get; private set; }
    public float TopInset { get; set; } = 88;

    public void AttachToHud(Transform canvas, Font font, ResourceDefinition resources)
    {
        if (_integrated) return;
        _integrated = true;
        _hudFont = font; _hudResources = resources;
        _originalParent = transform.parent;
        _originalWindow = elementWindowTransform;
        _originalContentParent = statusRowContainer.parent;
        // 既存のProviderと行プールを残し、左右HUDと同じ論理サイズへ揃える。
        elementWindowTransform.gameObject.SetActive(false);
        transform.SetParent(canvas, false);
        var window = new GameObject("CellInformation", typeof(RectTransform), typeof(Image), typeof(ScrollRect));
        window.transform.SetParent(transform, false);
        var rect = window.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
        window.GetComponent<Image>().color = HudUIAdapter.Background;
        var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D));
        viewport.transform.SetParent(window.transform, false);
        var viewportRect = viewport.GetComponent<RectTransform>();
        viewportRect.anchorMin = Vector2.zero; viewportRect.anchorMax = Vector2.one;
        viewportRect.offsetMin = new Vector2(12, 12); viewportRect.offsetMax = new Vector2(-12, -48);
        CloseButton = HudUIAdapter.MakeButton(font, window.transform, "閉じる", () => SetStatusWindowActive(false), 32);
        var closeRect = CloseButton.GetComponent<RectTransform>();
        closeRect.anchorMin = closeRect.anchorMax = Vector2.one; closeRect.pivot = Vector2.one;
        closeRect.anchoredPosition = new Vector2(-12, -8); closeRect.sizeDelta = new Vector2(80, 32);
        statusRowContainer.SetParent(viewport.transform, false);
        var content = (RectTransform)statusRowContainer;
        _originalAnchorMin = content.anchorMin; _originalAnchorMax = content.anchorMax;
        _originalPivot = content.pivot; _originalPosition = content.anchoredPosition; _originalSize = content.sizeDelta;
        content.anchorMin = Vector2.up; content.anchorMax = Vector2.one;
        content.pivot = Vector2.up; content.anchoredPosition = Vector2.zero; content.sizeDelta = Vector2.zero;
        var layout = content.GetComponent<VerticalLayoutGroup>();
        _originalControlHeight = layout.childControlHeight;
        layout.childControlHeight = true;
        _addedFitter = content.GetComponent<ContentSizeFitter>() == null;
        var fitter = content.GetComponent<ContentSizeFitter>() ?? content.gameObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        StatusScroll = window.GetComponent<ScrollRect>();
        StatusScroll.viewport = viewportRect; StatusScroll.content = content; StatusScroll.horizontal = false;
        StatusScroll.movementType = ScrollRect.MovementType.Clamped; StatusScroll.scrollSensitivity = 25;
        elementWindowTransform = window.transform;
        window.SetActive(_selectedCell != null);
    }

    public void ReleaseHud()
    {
        if (!_integrated) return;
        SetStatusWindowActive(false);
        statusRowContainer.SetParent(_originalContentParent, false);
        var content = (RectTransform)statusRowContainer;
        if (_addedFitter) Destroy(content.GetComponent<ContentSizeFitter>());
        content.GetComponent<VerticalLayoutGroup>().childControlHeight = _originalControlHeight;
        content.anchorMin = _originalAnchorMin; content.anchorMax = _originalAnchorMax;
        content.pivot = _originalPivot; content.anchoredPosition = _originalPosition; content.sizeDelta = _originalSize;
        transform.SetParent(_originalParent, false);
        Destroy(elementWindowTransform.gameObject);
        elementWindowTransform = _originalWindow;
        StatusScroll = null;
        _integrated = false;
        // HUD用の行は借用フォントを参照するため、返却後に破棄して元のプールへ戻す。
        foreach (var pool in _statusRowUIPool.Values) pool.Clear();
        _statusRowUIPool.Clear();
        InitializeUIElementPool();
    }
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
        var row = new GameObject("CellStatusRow", typeof(RectTransform), typeof(HudStatusRowAdapter));
        row.transform.SetParent(statusRowContainer, false);
        var adapter = row.GetComponent<HudStatusRowAdapter>();
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
        // 既存Canvasの倍率へ換算し、セル情報を下部操作帯より上に収める。
        if (transform is RectTransform window && window.parent is RectTransform parent)
        {
            window.anchorMin = Vector2.right;
            window.anchorMax = Vector2.one;
            window.pivot = Vector2.one;
            window.offsetMin = new Vector2(_integrated ? -412 : -562.25f, parent.rect.height * 108f / 720f);
            window.offsetMax = new Vector2(-12, _integrated ? -TopInset : -12);
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

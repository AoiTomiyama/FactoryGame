using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

/// <summary>前提関係を枝とノードで表示する。選択を通知し、購入は所有しない。</summary>
public sealed class SkillTreeViewAdapter : IDisposable
{
    private readonly HudLayoutAdapter _hud;
    private readonly SkillTreeApplication _skills;
    private readonly RectTransform _root, _viewport;
    private readonly ScrollRect _scroll;
    private bool _focusSelection, _centered;
    private readonly LayoutElement _size;
    private readonly Dictionary<string, Button> _buttons = new();
    private readonly Dictionary<string, float> _lanes = new();
    private readonly Dictionary<string, int> _depths = new();
    private readonly Dictionary<string, Text> _marks = new();
    private readonly Dictionary<string, List<SkillNodeDomain>> _children = new();
    private readonly List<(string parent, string child, Image image)> _links = new();
    private readonly RectTransform _selection;
    private string _selected;
    private int _leaves;
    public IReadOnlyDictionary<string, Button> Buttons => _buttons;

    public SkillTreeViewAdapter(Transform parent, HudLayoutAdapter hud, SkillTreeApplication skills, Action<string> select)
    {
        _hud = hud; _skills = skills;
        // 名前は右側詳細へ集約し、ノードを小さく保つ。枝が収まらない画面では樹形図だけ横へ移動できる。
        _viewport = new GameObject("SkillTreeViewport", typeof(RectTransform), typeof(LayoutElement), typeof(CanvasRenderer), typeof(Image), typeof(RectMask2D), typeof(ScrollRect)).GetComponent<RectTransform>();
        _viewport.SetParent(parent, false); _viewport.GetComponent<Image>().color = Color.clear;
        _size = _viewport.GetComponent<LayoutElement>();
        _root = new GameObject("SkillTree", typeof(RectTransform)).GetComponent<RectTransform>();
        _root.SetParent(_viewport, false); _root.anchorMin = _root.anchorMax = Vector2.up; _root.pivot = Vector2.up;
        _scroll = _viewport.GetComponent<ScrollRect>(); _scroll.viewport = _viewport; _scroll.content = _root;
        _scroll.horizontal = true; _scroll.vertical = false; _scroll.movementType = ScrollRect.MovementType.Clamped;
        var track = new GameObject("HorizontalScrollbar", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Scrollbar)).GetComponent<Scrollbar>();
        track.transform.SetParent(_viewport, false); track.GetComponent<Image>().color = hud.IdleTabColor;
        var trackRect = (RectTransform)track.transform; trackRect.anchorMin = Vector2.zero; trackRect.anchorMax = Vector2.right;
        trackRect.pivot = new Vector2(.5f, 0); trackRect.sizeDelta = new Vector2(-16, 8); trackRect.anchoredPosition = new Vector2(0, 2);
        var handle = new GameObject("Handle", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)).GetComponent<Image>();
        handle.transform.SetParent(track.transform, false); handle.color = hud.SelectedTabColor;
        handle.rectTransform.anchorMin = Vector2.zero; handle.rectTransform.anchorMax = Vector2.one; handle.rectTransform.sizeDelta = Vector2.zero;
        track.handleRect = handle.rectTransform; track.targetGraphic = handle; track.direction = Scrollbar.Direction.LeftToRight;
        _scroll.horizontalScrollbar = track; _scroll.horizontalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
        foreach (var node in skills.Catalog.Nodes) _children.Add(node.Uid, new List<SkillNodeDomain>());
        foreach (var node in skills.Catalog.Nodes)
            if (node.Prerequisite != null) _children[node.Prerequisite].Add(node);
        foreach (var node in skills.Catalog.Nodes.Where(node => node.Prerequisite == null)) Assign(node, 0);
        foreach (var node in skills.Catalog.Nodes)
        {
            var uid = node.Uid;
            var button = UnityEngine.Object.Instantiate(hud.SkillRow, _root);
            button.name = "SkillNode_" + uid;
            var label = button.GetComponentInChildren<Text>(true);
            label.text = ""; label.gameObject.SetActive(false);
            button.gameObject.AddComponent<CanvasGroup>();
            var mark = new GameObject("Applied", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text)).GetComponent<Text>();
            mark.transform.SetParent(button.transform, false); mark.font = hud.Font; mark.fontSize = 14; mark.color = Color.white;
            mark.text = hud.Font.HasCharacter('✓') ? "✓" : "●"; mark.alignment = TextAnchor.MiddleCenter; mark.raycastTarget = false;
            var markRect = mark.rectTransform; markRect.anchorMin = markRect.anchorMax = new Vector2(.5f, .5f); markRect.pivot = new Vector2(.5f, .5f);
            markRect.anchoredPosition = Vector2.zero; markRect.sizeDelta = new Vector2(20, 20);
            _marks.Add(uid, mark);
            button.onClick.AddListener(() => select(uid));
            _buttons.Add(uid, button);
            if (node.Prerequisite == null) continue;
            var image = new GameObject("Prerequisite_" + uid, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)).GetComponent<Image>();
            image.transform.SetParent(_root, false); image.transform.SetAsFirstSibling();
            image.raycastTarget = false;
            _links.Add((node.Prerequisite, uid, image));
        }
        // 選択枠はButtonのColorTintから分け、適用済みの色と選択中の白枠を識別する。
        _selection = new GameObject("SelectionFrame", typeof(RectTransform)).GetComponent<RectTransform>();
        _selection.SetParent(_root, false);
        for (var edge = 0; edge < 4; edge++)
        {
            var image = new GameObject("Edge", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)).GetComponent<Image>();
            image.transform.SetParent(_selection, false); image.color = Color.white; image.raycastTarget = false;
            var rect = image.rectTransform;
            rect.anchorMin = edge == 0 ? Vector2.up : edge == 3 ? Vector2.right : Vector2.zero;
            rect.anchorMax = edge == 1 ? Vector2.right : edge == 2 ? Vector2.up : Vector2.one;
            rect.sizeDelta = edge < 2 ? new Vector2(0, 2) : new Vector2(2, 0);
            rect.anchoredPosition = Vector2.zero;
        }
        Refresh(null);
    }
    private float Assign(SkillNodeDomain node, int depth)
    {
        _depths.Add(node.Uid, depth);
        var children = _children[node.Uid];
        var lane = children.Count == 0 ? _leaves++ : 0f;
        if (children.Count > 0)
        {
            var first = Assign(children[0], depth + 1); var last = first;
            for (var i = 1; i < children.Count; i++) last = Assign(children[i], depth + 1);
            lane = (first + last) / 2;
        }
        _lanes.Add(node.Uid, lane); return lane;
    }
    public void Refresh(string selected)
    {
        _focusSelection |= selected != null && selected != _selected;
        _selected = selected;
        _selection.gameObject.SetActive(selected != null);
        foreach (var node in _skills.Catalog.Nodes)
        {
            var button = _buttons[node.Uid];
            // 未解放・適用済みも選択できるようにし、詳細側だけで購入可否を制御する。
            button.interactable = true;
            var owned = _skills.IsOwned(node.Uid);
            var chosen = node.Uid == selected;
            var state = _skills.CanPurchase(node.Uid);
            // 前提不足は固有のグレーで示す。残高不足は通常色を保ち、選択は白枠で伝える。
            if (state == SkillPurchaseResultApplication.PrerequisiteMissing)
            {
                button.targetGraphic.color = Color.white;
                var colors = button.colors;
                colors.normalColor = colors.highlightedColor = colors.pressedColor = colors.selectedColor = new Color(.18f, .18f, .18f);
                button.colors = colors;
            }
            else _hud.StyleButton(button, owned || chosen);
            button.GetComponent<CanvasGroup>().alpha = 1;
            _marks[node.Uid].gameObject.SetActive(owned);
        }
        foreach (var link in _links) link.image.color = _skills.IsOwned(link.parent) ? _hud.SelectedTabColor : _hud.IdleTabColor;
        UpdateLayout();
    }
    public void UpdateLayout()
    {
        if (!_root.gameObject.activeInHierarchy) return;
        var viewportWidth = _viewport.rect.width;
        if (viewportWidth <= 1) return;
        var width = Mathf.Max(viewportWidth, _leaves * 64);
        _root.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
        var slot = width / Mathf.Max(1, _leaves);
        const float nodeWidth = 40;
        var heights = new float[_depths.Values.Max() + 1];
        foreach (var pair in _buttons)
        {
            var rect = (RectTransform)pair.Value.transform;
            rect.anchorMin = rect.anchorMax = Vector2.up; rect.pivot = new Vector2(.5f, 1);
            rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, nodeWidth);
            heights[_depths[pair.Key]] = 40;
        }
        var tops = new float[heights.Length];
        for (var i = 1; i < tops.Length; i++) tops[i] = tops[i - 1] + heights[i - 1] + 32;
        var treeHeight = tops[tops.Length - 1] + heights[heights.Length - 1];
        _size.preferredHeight = treeHeight + 16;
        _root.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, treeHeight);
        foreach (var pair in _buttons)
        {
            var rect = (RectTransform)pair.Value.transform; var depth = _depths[pair.Key];
            rect.anchoredPosition = new Vector2((_lanes[pair.Key] + .5f) * slot, -tops[depth]);
            rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, heights[depth]);
        }
        if (_selected != null)
        {
            var selected = (RectTransform)_buttons[_selected].transform;
            _selection.anchorMin = _selection.anchorMax = Vector2.up; _selection.pivot = selected.pivot;
            _selection.anchoredPosition = selected.anchoredPosition; _selection.sizeDelta = selected.rect.size;
        }
        if (!_centered) { _scroll.horizontalNormalizedPosition = .5f; _centered = true; }
        if (_focusSelection && _selected != null)
        {
            var center = ((RectTransform)_buttons[_selected].transform).anchoredPosition.x;
            _scroll.horizontalNormalizedPosition = width <= viewportWidth ? 0 : Mathf.Clamp01((center - viewportWidth / 2) / (width - viewportWidth));
            _focusSelection = false;
        }
        foreach (var link in _links)
        {
            var parent = (RectTransform)_buttons[link.parent].transform;
            var child = (RectTransform)_buttons[link.child].transform;
            var start = parent.anchoredPosition - new Vector2(0, parent.rect.height);
            var end = child.anchoredPosition; var direction = end - start;
            var rect = link.image.rectTransform;
            rect.anchorMin = rect.anchorMax = Vector2.up; rect.pivot = new Vector2(0, .5f);
            rect.anchoredPosition = start; rect.sizeDelta = new Vector2(direction.magnitude, 2);
            rect.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
        }
    }
    public void Dispose()
    {
        foreach (var button in _buttons.Values) if (button != null) button.onClick.RemoveAllListeners();
        _buttons.Clear();
        if (_viewport != null) { _viewport.gameObject.SetActive(false); UnityEngine.Object.Destroy(_viewport.gameObject); }
    }
}

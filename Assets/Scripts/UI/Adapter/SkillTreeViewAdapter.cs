using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

/// <summary>表示対象だけを配置し、購入可能・取得済み・選択を独立して表示する。</summary>
public sealed class SkillTreeViewAdapter : IDisposable
{
    private readonly ScrollRect _scroll;
    private readonly RectTransform _root, _viewport, _selection;
    private readonly HudLayoutAdapter _theme;
    private readonly Action<string> _select;
    private readonly Dictionary<string,Button> _buttons = new();
    private readonly Dictionary<string,Text> _marks = new();
    private readonly Dictionary<string,Image> _links = new();
    private readonly Dictionary<string,List<SkillNodeDomain>> _children = new();
    private readonly Dictionary<string,float> _lanes = new();
    private readonly Dictionary<string,int> _depths = new();
    private SkillNodeDomain[] _visible = Array.Empty<SkillNodeDomain>();
    private string _selected;
    private bool _dirty, _focus, _centered;
    private Vector2 _viewportSize;
    private Vector3? _anchor;
    private int _leaves;
    public IReadOnlyDictionary<string,Button> Buttons => _buttons;

    public SkillTreeViewAdapter(ScrollRect scroll, HudLayoutAdapter theme, Action<string> select)
    {
        _scroll = scroll; _root = scroll.content; _viewport = scroll.viewport; _theme = theme; _select = select;
        _selection = new GameObject("SelectionFrame",typeof(RectTransform)).GetComponent<RectTransform>();
        _selection.SetParent(_root,false);
        for (var edge=0;edge<4;edge++)
        {
            var image = new GameObject("Edge",typeof(RectTransform),typeof(CanvasRenderer),typeof(Image)).GetComponent<Image>();
            image.transform.SetParent(_selection,false); image.color = Color.white; image.raycastTarget = false;
            var rect = image.rectTransform;
            rect.anchorMin = edge == 0 ? Vector2.up : edge == 3 ? Vector2.right : Vector2.zero;
            rect.anchorMax = edge == 1 ? Vector2.right : edge == 2 ? Vector2.up : Vector2.one;
            rect.sizeDelta = edge < 2 ? new Vector2(0,2) : new Vector2(2,0);
        }
        _selection.gameObject.SetActive(false);
    }
    public void Render(IReadOnlyList<SkillTreePresentationApplication.NodeState> snapshot, string selected)
    {
        var visible = snapshot.Where(s => s.Visible).ToArray();
        var changed = !_visible.Select(n => n.Uid).SequenceEqual(visible.Select(s => s.Node.Uid));
        if (changed && _selected != null && _buttons.TryGetValue(_selected,out var anchor) && anchor.gameObject.activeSelf)
            _anchor = _viewport.InverseTransformPoint(anchor.transform.TransformPoint(((RectTransform)anchor.transform).rect.center));
        _focus |= selected != null && selected != _selected;
        _selected = selected;
        var visibleIds = visible.Select(s => s.Node.Uid).ToHashSet();
        foreach (var pair in _buttons) if (!visibleIds.Contains(pair.Key)) pair.Value.gameObject.SetActive(false);
        foreach (var state in visible)
        {
            var node = state.Node;
            if (!_buttons.TryGetValue(node.Uid,out var button))
            {
                var uid = node.Uid;
                button = UnityEngine.Object.Instantiate(_theme.SkillRow,_root);
                button.name = "SkillNode_" + uid;
                var label = button.GetComponentInChildren<Text>(true); label.gameObject.SetActive(false);
                button.onClick.AddListener(() => _select(uid)); _buttons.Add(uid,button);
                var mark = new GameObject("State",typeof(RectTransform),typeof(CanvasRenderer),typeof(Text)).GetComponent<Text>();
                mark.transform.SetParent(button.transform,false); mark.font = _theme.Font; mark.fontSize = 18;
                mark.alignment = TextAnchor.MiddleCenter; mark.color = Color.white; mark.raycastTarget = false;
                mark.rectTransform.anchorMin = Vector2.zero; mark.rectTransform.anchorMax = Vector2.one; mark.rectTransform.sizeDelta = Vector2.zero;
                _marks.Add(uid,mark);
            }
            if (!button.gameObject.activeSelf) button.gameObject.SetActive(true);
            button.interactable = true;
            _theme.StyleButton(button,state.Highlighted);
            _marks[node.Uid].text = state.Owned ? (_theme.Font.HasCharacter('✓') ? "✓" : "●") : state.Highlighted ? "+" : "";
        }
        foreach (var link in _links.Values) link.gameObject.SetActive(false);
        var ids = visible.Select(s => s.Node.Uid).ToHashSet();
        foreach (var state in visible)
        {
            var node = state.Node;
            if (node.Prerequisite == null || !ids.Contains(node.Prerequisite)) continue;
            if (!_links.TryGetValue(node.Uid,out var image))
            {
                image = new GameObject("Prerequisite_"+node.Uid,typeof(RectTransform),typeof(CanvasRenderer),typeof(Image)).GetComponent<Image>();
                image.transform.SetParent(_root,false); image.raycastTarget = false; _links.Add(node.Uid,image);
            }
            image.gameObject.SetActive(true); image.transform.SetAsFirstSibling(); image.color = _theme.SelectedTabColor;
        }
        _visible = visible.Select(s => s.Node).ToArray(); _dirty |= changed;
        _selection.gameObject.SetActive(_selected != null && ids.Contains(_selected));
        UpdateLayout();
    }
    private float Assign(SkillNodeDomain node,int depth)
    {
        _depths.Add(node.Uid,depth);
        var children = _children[node.Uid];
        var lane = children.Count == 0 ? _leaves++ : 0f;
        if (children.Count > 0)
        {
            var first = Assign(children[0],depth+1); var last = first;
            foreach (var child in children.Skip(1)) last = Assign(child,depth+1);
            lane = (first+last)*.5f;
        }
        _lanes.Add(node.Uid,lane); return lane;
    }
    public void UpdateLayout()
    {
        if (!_root.gameObject.activeInHierarchy || _visible.Length == 0 || _viewport.rect.width <= 1) return;
        _dirty |= _viewportSize != _viewport.rect.size;
        if (_dirty)
        {
            _scroll.StopMovement(); _viewportSize = _viewport.rect.size;
            _children.Clear(); _lanes.Clear(); _depths.Clear(); _leaves = 0;
            foreach (var node in _visible) _children[node.Uid] = new List<SkillNodeDomain>();
            foreach (var node in _visible) if (node.Prerequisite != null && _children.ContainsKey(node.Prerequisite)) _children[node.Prerequisite].Add(node);
            foreach (var node in _visible.Where(n => n.Prerequisite == null || !_children.ContainsKey(n.Prerequisite))) Assign(node,0);
            var width = Mathf.Max(_viewportSize.x,_leaves*72+32);
            _root.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal,width);
            _root.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,Mathf.Max(_viewportSize.y,(_depths.Values.Max()+1)*88+32));
            foreach (var node in _visible)
            {
                var rect = (RectTransform)_buttons[node.Uid].transform;
                rect.anchorMin = rect.anchorMax = Vector2.up; rect.pivot = new Vector2(.5f,1); rect.sizeDelta = new Vector2(40,40);
                rect.anchoredPosition = new Vector2((_lanes[node.Uid]+.5f)*width/Mathf.Max(1,_leaves),-16-_depths[node.Uid]*88);
            }
            foreach (var node in _visible.Where(n => n.Prerequisite != null && _children.ContainsKey(n.Prerequisite)))
            {
                var parent = (RectTransform)_buttons[node.Prerequisite].transform; var child = (RectTransform)_buttons[node.Uid].transform;
                var start = parent.anchoredPosition-new Vector2(0,parent.rect.height); var end = child.anchoredPosition; var direction = end-start;
                var rect = _links[node.Uid].rectTransform; rect.anchorMin = rect.anchorMax = Vector2.up; rect.pivot = new Vector2(0,.5f);
                rect.anchoredPosition = start; rect.sizeDelta = new Vector2(direction.magnitude,2);
                rect.localRotation = Quaternion.Euler(0,0,Mathf.Atan2(direction.y,direction.x)*Mathf.Rad2Deg);
            }
            if (!_centered) { _scroll.horizontalNormalizedPosition = .5f; _scroll.verticalNormalizedPosition = 1; _centered = true; }
            else if (_anchor.HasValue && _selected != null && _buttons.TryGetValue(_selected,out var anchor))
            {
                var now = _viewport.InverseTransformPoint(anchor.transform.TransformPoint(((RectTransform)anchor.transform).rect.center));
                _root.anchoredPosition += (Vector2)(_anchor.Value-now);
            }
            _anchor = null; _dirty = false;
        }
        if (_selected != null && _buttons.TryGetValue(_selected,out var selected) && selected.gameObject.activeSelf)
        {
            var rect = (RectTransform)selected.transform;
            _selection.anchorMin = _selection.anchorMax = Vector2.up; _selection.pivot = rect.pivot;
            _selection.anchoredPosition = rect.anchoredPosition; _selection.sizeDelta = rect.rect.size;
            if (_focus)
            {
                // 選んだノードが見えている間は、横に並ぶ候補の位置を保つ。
                var hiddenWidth = _root.rect.width - _viewport.rect.width;
                if (hiddenWidth > 0)
                {
                    var view = _viewport.rect;
                    var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(_viewport, rect);
                    var offset = bounds.min.x < view.xMin ? bounds.min.x - view.xMin : Mathf.Max(0, bounds.max.x - view.xMax);
                    if (!Mathf.Approximately(offset, 0))
                        _scroll.horizontalNormalizedPosition = Mathf.Clamp01(_scroll.horizontalNormalizedPosition + offset / hiddenWidth);
                }
                _scroll.verticalNormalizedPosition = _root.rect.height <= _viewportSize.y ? 1 : 1-Mathf.Clamp01((-rect.anchoredPosition.y-_viewportSize.y*.5f)/(_root.rect.height-_viewportSize.y));
            }
        }
        _focus = false;
    }
    public void Dispose()
    {
        foreach (var button in _buttons.Values) if (button != null) UnityEngine.Object.Destroy(button.gameObject);
        foreach (var link in _links.Values) if (link != null) UnityEngine.Object.Destroy(link.gameObject);
        if (_selection != null) UnityEngine.Object.Destroy(_selection.gameObject);
        _buttons.Clear(); _links.Clear();
    }
}

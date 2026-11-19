using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>保存済み全画面UIの開閉と購読を所有し、背後のHUD・入力を復元する。</summary>
public sealed class SkillScreenAdapter : MonoBehaviour
{
    public RectTransform ScreenRoot;
    public Text WalletText, Heading, Description, Feedback;
    public Button CloseButton, PurchaseButton;
    public ScrollRect TreeScroll;
    public GameObject DetailsGroup;
    public bool IsOpen { get; private set; }
    public bool BlocksGameplayInput => IsOpen || Time.frameCount == _closedFrame;
    public string SelectedUid => _presenter?.SelectedUid;
    public EquipmentDetailsViewAdapter Details { get; private set; }
    public IReadOnlyDictionary<string, Button> NodeButtons => _tree.Buttons;
    public event Action<bool> VisibilityChanged;
    private int _closedFrame = -1;
    private CurrencyWalletApplication _wallet;
    private SkillTreeApplication _skills;
    private DeliveryProgressApplication _deliveries;
    private SkillTreePresentationApplication _presentation;
    private SkillDetailsPresenterAdapter _presenter;
    private SkillTreeViewAdapter _tree;
    private Action _cancelGesture;
    private Transform[] _hiddenRoots;
    private readonly Dictionary<CanvasGroup,(float alpha,bool interactable,bool raycasts)> _visibility = new();
    private CursorLockMode _cursorLock;
    private bool _cursorVisible;

    public void ValidateBindings()
    {
        if (ScreenRoot == null || WalletText == null || Heading == null || Description == null || Feedback == null ||
            CloseButton == null || PurchaseButton == null || TreeScroll == null || TreeScroll.viewport == null ||
            TreeScroll.content == null || DetailsGroup == null)
            throw new MissingReferenceException($"SkillScreenAdapter: incomplete UI references on '{name}' (Scene: '{gameObject.scene.path}').");
    }
    public void Configure(CurrencyWalletApplication wallet, SkillTreeApplication skills, DeliveryProgressApplication deliveries,
        SharedStatEffectsApplication effects, HudLayoutAdapter theme, Func<string> clearingPrices, Action cancelGesture,
        IEnumerable<Transform> hiddenRoots)
    {
        ValidateBindings();
        if (_tree != null) throw new InvalidOperationException("Skill screen is already configured.");
        _wallet = wallet; _skills = skills; _deliveries = deliveries; _cancelGesture = cancelGesture;
        _hiddenRoots = hiddenRoots.Where(t => t != null).Distinct().ToArray();
        if (_hiddenRoots.Any(t => transform.IsChildOf(t))) throw new InvalidOperationException("Hidden HUD must not contain the skill screen.");
        _hiddenRoots = _hiddenRoots.Where(t => !_hiddenRoots.Any(parent => parent != t && t.IsChildOf(parent))).ToArray();
        foreach (var text in ScreenRoot.GetComponentsInChildren<Text>(true)) if (text.font == null) text.font = theme.Font;
        theme.StyleButton(CloseButton); theme.StyleButton(PurchaseButton);
        Details = new EquipmentDetailsViewAdapter(Heading, DetailsGroup, Description, PurchaseButton, Feedback);
        _presentation = new SkillTreePresentationApplication(skills);
        _presenter = new SkillDetailsPresenterAdapter(skills, effects, deliveries, Details, clearingPrices);
        _tree = new SkillTreeViewAdapter(TreeScroll, theme, Select);
        CloseButton.onClick.AddListener(Close);
        ScreenRoot.gameObject.SetActive(false);
    }
    public void Open()
    {
        if (IsOpen || _tree == null) return;
        IsOpen = true;
        _cancelGesture();
        _cursorLock = Cursor.lockState; _cursorVisible = Cursor.visible;
        Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
        _visibility.Clear();
        foreach (var root in _hiddenRoots)
        {
            var group = root.GetComponent<CanvasGroup>();
            if (group == null) group = root.gameObject.AddComponent<CanvasGroup>();
            _visibility[group] = (group.alpha, group.interactable, group.blocksRaycasts);
            group.alpha = 0; group.interactable = group.blocksRaycasts = false;
        }
        ScreenRoot.gameObject.SetActive(true); transform.SetAsLastSibling();
        _wallet.Changed += WalletChanged; _skills.Changed += Refresh; _deliveries.Changed += DeliveryChanged;
        VisibilityChanged?.Invoke(true);
        Canvas.ForceUpdateCanvases(); Refresh();
    }
    public void Close()
    {
        if (!IsOpen) return;
        _wallet.Changed -= WalletChanged; _skills.Changed -= Refresh; _deliveries.Changed -= DeliveryChanged;
        ScreenRoot.gameObject.SetActive(false); IsOpen = false; _closedFrame = Time.frameCount;
        foreach (var pair in _visibility)
            if (pair.Key != null) { pair.Key.alpha = pair.Value.alpha; pair.Key.interactable = pair.Value.interactable; pair.Key.blocksRaycasts = pair.Value.raycasts; }
        _visibility.Clear();
        Cursor.lockState = _cursorLock; Cursor.visible = _cursorVisible;
        VisibilityChanged?.Invoke(false);
    }
    public void Select(string uid)
    {
        if (!IsOpen || !IsNodeVisible(uid)) return;
        _presenter.Select(uid); Refresh();
    }
    public bool IsNodeVisible(string uid) => uid != null && _tree.Buttons.TryGetValue(uid, out var button) && button.gameObject.activeSelf;
    public Button NodeButton(string uid) => _tree.Buttons.TryGetValue(uid, out var button) && button.gameObject.activeSelf ? button : null;
    public SkillPurchaseResultApplication PurchaseSelected() => _presenter.PurchaseSelected();
    private void WalletChanged(CurrencyWalletApplication.Snapshot _) => Refresh();
    private void DeliveryChanged(DeliveryProgressApplication.Snapshot _) => Refresh();
    private void Refresh()
    {
        if (!IsOpen) return;
        WalletText.text = $"残高 {_wallet.Current.Balance}";
        var snapshot = _presentation.Capture();
        if (SelectedUid != null && !snapshot.Any(s => s.Node.Uid == SelectedUid && s.Visible)) _presenter.Select(null);
        _tree.Render(snapshot, SelectedUid); _presenter.Refresh();
    }
    private void Update() { if (IsOpen && Keyboard.current?.escapeKey.wasPressedThisFrame == true) Close(); }
    private void LateUpdate() { if (IsOpen) { _tree.UpdateLayout(); Details.UpdateLayout(); } }
    private void OnDisable() => Close();
    private void OnDestroy() => ReleaseWorld();
    internal void ReleaseWorld()
    {
        Close();
        if (CloseButton != null) CloseButton.onClick.RemoveListener(Close);
        _presenter?.Dispose(); _tree?.Dispose();
        _presenter = null; _tree = null; _presentation = null;
        _wallet = null; _skills = null; _deliveries = null;
    }
}

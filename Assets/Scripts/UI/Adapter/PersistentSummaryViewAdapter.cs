using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>常時表示項目を指定順で並べ、項目増加時は枠内をスクロールする。</summary>
public sealed class PersistentSummaryViewAdapter
{
    public enum WalletField { Balance, Available, Expense, Income }
    private WalletField[] _walletOrder = { WalletField.Balance, WalletField.Available, WalletField.Expense, WalletField.Income };
    private CurrencyWalletApplication.Snapshot _wallet;
    private readonly Font _font;
    private readonly Transform _content;
    private readonly List<Text> _items = new();
    public RectTransform Rect { get; }
    public Text Balance { get; }
    public ScrollRect Scroll { get; }
    public IReadOnlyList<Text> Items => _items;
    public PersistentSummaryViewAdapter(Transform canvas, Font font)
    {
        _font = font;
        var root = new GameObject("PersistentSummary", typeof(RectTransform), typeof(Image), typeof(ScrollRect));
        root.transform.SetParent(canvas, false);
        Rect = root.GetComponent<RectTransform>();
        HudUIAdapter.Anchor(Rect, Vector2.up, Vector2.one, new Vector2(12, -12), new Vector2(-24, 68));
        root.GetComponent<Image>().color = HudUIAdapter.Background;
        var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D));
        viewport.transform.SetParent(root.transform, false);
        var viewportRect = viewport.GetComponent<RectTransform>();
        HudUIAdapter.Stretch(viewportRect);
        viewportRect.offsetMin = new Vector2(12, 8); viewportRect.offsetMax = new Vector2(-12, -8);
        var content = new GameObject("SummaryItems", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        content.transform.SetParent(viewport.transform, false);
        var contentRect = content.GetComponent<RectTransform>();
        contentRect.anchorMin = Vector2.up; contentRect.anchorMax = Vector2.one; contentRect.pivot = Vector2.up; contentRect.sizeDelta = Vector2.zero;
        var layout = content.GetComponent<VerticalLayoutGroup>();
        layout.childControlHeight = true; layout.childForceExpandHeight = false; layout.spacing = 4;
        content.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        _content = content.transform;
        Balance = HudUIAdapter.MakeText(font, _content, "", 16, 48);
        Balance.fontStyle = FontStyle.Bold;
        Scroll = root.GetComponent<ScrollRect>();
        Scroll.viewport = viewportRect; Scroll.content = contentRect; Scroll.horizontal = false;
        Scroll.movementType = ScrollRect.MovementType.Clamped; Scroll.scrollSensitivity = 25;
    }
    public void RenderWallet(CurrencyWalletApplication.Snapshot wallet)
    {
        _wallet = wallet;
        var entries = _walletOrder.Select(field => field switch
        {
            WalletField.Balance => $"残高 {wallet.Balance}", WalletField.Available => $"使用可能 {wallet.AvailableBalance}",
            WalletField.Expense => $"支出予約 {wallet.ReservedExpense}", _ => $"換金待ち {wallet.ReservedIncome}"
        }).ToArray();
        Balance.text = $"{entries[0]}　{entries[1]}\n{entries[2]}　{entries[3]}";
    }
    public void SetWalletOrder(params WalletField[] order)
    {
        if (order == null || order.Length != 4 || order.Distinct().Count() != 4 || order.Any(f => !Enum.IsDefined(typeof(WalletField), f)))
            throw new ArgumentException("All wallet fields must occur once.", nameof(order));
        _walletOrder = (WalletField[])order.Clone();
        RenderWallet(_wallet);
    }
    public void SetItems(params string[] items)
    {
        while (_items.Count < items.Length) _items.Add(HudUIAdapter.MakeText(_font, _content, "", 16, 24));
        for (var i = 0; i < _items.Count; i++)
        {
            _items[i].gameObject.SetActive(i < items.Length);
            if (i < items.Length) _items[i].text = items[i];
        }
        UpdateLayout();
    }
    public void UpdateLayout()
    {
        var height = Fit(Balance);
        foreach (var text in _items)
            if (text.gameObject.activeSelf) height += Fit(text) + 4;
        // 左右パネルと下部操作帯の領域を残し、増えた項目へスクロールで到達させる。
        Rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, Mathf.Clamp(height + 16, 64, 220));
    }
    private static float Fit(Text text)
    {
        var height = Mathf.Max(text.GetComponent<LayoutElement>().minHeight, text.preferredHeight);
        text.GetComponent<LayoutElement>().preferredHeight = height;
        return height;
    }
}

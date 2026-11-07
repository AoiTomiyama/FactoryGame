using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>常時表示項目を指定順で横に並べ、画面幅に応じて折り返す。</summary>
public sealed class PersistentSummaryViewAdapter
{
    public enum WalletField { Balance, Available, Expense, Income }
    private WalletField[] _walletOrder = { WalletField.Balance, WalletField.Available, WalletField.Expense, WalletField.Income };
    private CurrencyWalletApplication.Snapshot _wallet;
    private readonly Font _font;
    private readonly List<Text> _items = new();
    private readonly Text[] _walletItems = new Text[4];
    public RectTransform Rect { get; }
    public string BalanceText => string.Join("　", _walletItems.Select(text => text.text));
    public IReadOnlyList<Text> WalletItems => _walletItems;
    public IReadOnlyList<Text> Items => _items;
    public PersistentSummaryViewAdapter(Transform canvas, Font font)
    {
        _font = font;
        var root = new GameObject("PersistentSummary", typeof(RectTransform), typeof(Image));
        root.transform.SetParent(canvas, false);
        Rect = root.GetComponent<RectTransform>();
        HudUIAdapter.Anchor(Rect, Vector2.up, Vector2.one, new Vector2(12, -12), new Vector2(-24, 64));
        root.GetComponent<Image>().color = HudUIAdapter.Background;
        for (var i = 0; i < _walletItems.Length; i++)
        {
            _walletItems[i] = HudUIAdapter.MakeText(font, root.transform, "", 16, 28);
            _walletItems[i].fontStyle = FontStyle.Bold;
        }
    }
    public void RenderWallet(CurrencyWalletApplication.Snapshot wallet)
    {
        _wallet = wallet;
        var entries = _walletOrder.Select(field => field switch
        {
            WalletField.Balance => $"残高 {wallet.Balance}", WalletField.Available => $"使用可能 {wallet.AvailableBalance}",
            WalletField.Expense => $"支出予約 {wallet.ReservedExpense}", _ => $"換金待ち {wallet.ReservedIncome}"
        }).ToArray();
        for (var i = 0; i < entries.Length; i++) _walletItems[i].text = entries[i];
        UpdateLayout();
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
        while (_items.Count < items.Length) _items.Add(HudUIAdapter.MakeText(_font, Rect, "", 16, 28));
        for (var i = 0; i < _items.Count; i++)
        {
            _items[i].gameObject.SetActive(i < items.Length);
            if (i < items.Length) _items[i].text = items[i];
        }
        UpdateLayout();
    }
    public void UpdateLayout()
    {
        // 全項目を通常の表示領域へ置き、画面幅に応じて行を増やす。
        var available = Mathf.Max(1, Rect.rect.width - 24);
        var x = 0f; var y = 8f; var rowHeight = 0f;
        foreach (var text in _walletItems.Concat(_items).Where(text => text.gameObject.activeSelf))
        {
            var width = Mathf.Min(available, Mathf.Ceil(text.preferredWidth) + 8);
            if (x > 0 && x + width > available)
            {
                x = 0; y += rowHeight + 6; rowHeight = 0;
            }
            var rect = text.rectTransform;
            rect.anchorMin = rect.anchorMax = Vector2.up; rect.pivot = Vector2.up;
            rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
            var height = Mathf.Max(28, Mathf.Ceil(text.preferredHeight));
            rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
            rect.anchoredPosition = new Vector2(12 + x, -y);
            rowHeight = Mathf.Max(rowHeight, height);
            x += width + 16;
        }
        Rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, Mathf.Max(64, y + rowHeight + 8));
    }
}

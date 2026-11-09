using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>常時表示項目を指定順で横に並べ、画面幅に応じて折り返す。</summary>
public sealed class PersistentSummaryViewAdapter
{
    private readonly Font _font;
    private readonly List<Text> _items = new();
    private readonly Text[] _walletItems;
    public RectTransform Rect { get; }
    public string BalanceText => _walletItems[0].text;
    public IReadOnlyList<Text> WalletItems => _walletItems;
    public IReadOnlyList<Text> Items => _items;
    private readonly Text _rowPrefab;
    public PersistentSummaryViewAdapter(RectTransform root, Font font, Text[] walletItems, Text rowPrefab)
    {
        _font = font;
        Rect = root;
        _rowPrefab = rowPrefab;
        _walletItems = walletItems;
    }
    public void RenderWallet(CurrencyWalletApplication.Snapshot wallet)
    {
        // 通貨の予約値は取引内部で保持し、プレイヤーには確定した残高を表示する。
        _walletItems[0].text = $"残高 {wallet.Balance}";
        UpdateLayout();
    }
    public void SetItems(params string[] items)
    {
        while (_items.Count < items.Length) _items.Add(CreateItem());
        for (var i = 0; i < _items.Count; i++)
        {
            _items[i].gameObject.SetActive(i < items.Length);
            if (i < items.Length) _items[i].text = items[i];
        }
        UpdateLayout();
    }
    public void Dispose()
    {
        foreach (var text in _items) if (text != null) UnityEngine.Object.Destroy(text.gameObject);
        _items.Clear();
    }
    private Text CreateItem()
    {
        var text = UnityEngine.Object.Instantiate(_rowPrefab, Rect);
        if (text.font == null) text.font = _font;
        return text;
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

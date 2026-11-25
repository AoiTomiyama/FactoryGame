using System;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>保存済みHUDの部品を使い、起動時に枠一覧と復帰ダイアログを一度だけ組む。</summary>
internal sealed class SaveSlotMenuAdapter
{
    private readonly GameObject _overlay;
    private readonly RectTransform _panel;
    private readonly Text _title, _status;
    private readonly Button[] _slots = new Button[SaveSlotCatalogApplication.NormalSlotCount];
    private readonly Button _debug, _saveAndReturn, _return, _cancel, _retry;
    private readonly Button _returnShortcut, _start, _back;
    private readonly Text _nameLabel, _seedLabel;
    private readonly InputField _name, _seed;
    private readonly Text[] _slotDetails = new Text[SaveSlotCatalogApplication.NormalSlotCount];
    private readonly Vector2 _shortcutPosition;
    private readonly float _saveWidth;
    private bool _busy;
    public bool BlocksGameplay => _overlay.activeSelf;

    internal SaveSlotMenuAdapter(HudLayoutAdapter hud, RectTransform host, Action<int> chooseSlot,
        Action chooseDebug, Action openReturn, Action<bool> returnToSlots, Action recover,
        Action<string, int?> beginNewGame, Action cancelNewGame)
    {
        _overlay = new GameObject("SaveSlotOverlay", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster), typeof(Image));
        var overlayRect = (RectTransform)_overlay.transform;
        overlayRect.SetParent(host, false);
        overlayRect.anchorMin = Vector2.zero;
        overlayRect.anchorMax = Vector2.one;
        overlayRect.offsetMin = overlayRect.offsetMax = Vector2.zero;
        var canvas = _overlay.GetComponent<Canvas>();
        canvas.overrideSorting = true;
        canvas.sortingOrder = 1000;
        _overlay.GetComponent<Image>().color = new Color(.06f, .07f, .09f, .76f);

        var panel = new GameObject("SaveSlotPanel", typeof(RectTransform), typeof(Image));
        _panel = (RectTransform)panel.transform;
        _panel.SetParent(overlayRect, false);
        _panel.sizeDelta = new Vector2(420, 330);
        panel.GetComponent<Image>().color = new Color(.10f, .12f, .14f, .98f);
        _title = Label("Title", 24, 30);
        _status = Label("Status", 278, 20);
        _title.fontStyle = FontStyle.Bold;

        for (var i = 0; i < _slots.Length; i++)
        {
            var slot = i + SaveSlotCatalogApplication.FirstSlot;
            _slots[i] = MakeButton($"SaveSlot{slot}", "", _panel, () => chooseSlot(slot));
            Place((RectTransform)_slots[i].transform, 68 + 49 * i, 42);
            var slotLabel = _slots[i].GetComponentInChildren<Text>(true);
            slotLabel.alignment = TextAnchor.UpperCenter;
            slotLabel.supportRichText = false;
            slotLabel.verticalOverflow = VerticalWrapMode.Truncate;
            _slotDetails[i] = Object.Instantiate(slotLabel, _slots[i].transform);
            _slotDetails[i].name = "Details";
            _slotDetails[i].alignment = TextAnchor.LowerCenter;
            _slotDetails[i].fontSize = 13;
            _slotDetails[i].raycastTarget = false;
        }
        _debug = MakeButton("DebugSlot", "デバッグワールド", _panel, chooseDebug);
        Place((RectTransform)_debug.transform, 222, 42);
        _saveAndReturn = MakeButton("SaveAndReturn", "保存して戻る", _panel, () => returnToSlots(true));
        _return = MakeButton("ReturnWithoutSaving", "保存せず戻る", _panel, () => returnToSlots(false));
        _cancel = MakeButton("CancelReturn", "キャンセル", _panel, () => { if (!_busy) Hide(); });
        _retry = MakeButton("RetrySlotSelection", "セーブ選択へ戻る", _panel, recover);
        Place((RectTransform)_saveAndReturn.transform, 68, 42);
        Place((RectTransform)_return.transform, 117, 42);
        Place((RectTransform)_cancel.transform, 166, 42);
        Place((RectTransform)_retry.transform, 124, 42);

        _nameLabel = Label("SaveNameLabel", 56, 17);
        _nameLabel.text = "セーブ名";
        _seedLabel = Label("WorldSeedLabel", 133, 17);
        _seedLabel.text = "シード（任意）";
        _name = MakeInput("SaveNameInput", 88, "");
        _seed = MakeInput("WorldSeedInput", 165, "空欄ならランダム");
        _start = MakeButton("BeginNewGame", "始める", _panel, () =>
        {
            int? seed = null;
            var input = _seed.text.Trim();
            if (input.Length != 0)
            {
                if (!int.TryParse(input, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
                {
                    _status.text = "シードは -2147483648 ～ 2147483647 の整数";
                    return;
                }
                seed = value;
            }
            beginNewGame(_name.text, seed);
        });
        _back = MakeButton("BackFromNewGame", "戻る", _panel, cancelNewGame);
        PlaceHalf(_back, false); PlaceHalf(_start, true);

        _returnShortcut = MakeButton("ReturnToSaveSlots", "セーブ選択へ戻る", hud.Summary, openReturn);
        hud.ReturnToSlots = _returnShortcut;
        var saveRect = (RectTransform)hud.Save.transform;
        var shortcut = (RectTransform)_returnShortcut.transform;
        _shortcutPosition = saveRect.anchoredPosition;
        _saveWidth = saveRect.rect.width;
        shortcut.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 186);
        SetDebugSession(false);
        Hide();

        Text Label(string name, float top, int size)
        {
            var label = Object.Instantiate(hud.TextRow, _panel);
            label.name = name;
            label.gameObject.SetActive(true);
            label.fontSize = size;
            label.alignment = TextAnchor.MiddleCenter;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Truncate;
            label.raycastTarget = false;
            Place(label.rectTransform, top, 36);
            return label;
        }

        InputField MakeInput(string name, float top, string hint)
        {
            var root = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(InputField));
            root.transform.SetParent(_panel, false);
            Place((RectTransform)root.transform, top, 36);
            var image = root.GetComponent<Image>();
            image.color = new Color(.19f, .21f, .23f);
            var input = root.GetComponent<InputField>();
            input.targetGraphic = image;
            input.textComponent = InputText("Text", Color.white);
            var placeholder = InputText("Placeholder", new Color(.73f, .75f, .77f));
            placeholder.text = hint;
            input.placeholder = placeholder;
            return input;

            Text InputText(string name, Color color)
            {
                var text = Object.Instantiate(hud.TextRow, root.transform, false);
                text.name = name;
                text.gameObject.SetActive(true);
                text.text = "";
                text.color = color;
                text.fontSize = 17;
                text.alignment = TextAnchor.MiddleLeft;
                text.supportRichText = false;
                text.raycastTarget = false;
                var rect = text.rectTransform;
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = new Vector2(10, 2);
                rect.offsetMax = new Vector2(-10, -2);
                return text;
            }
        }

        Button MakeButton(string name, string text, Transform parent, Action action)
        {
            var button = Object.Instantiate(hud.Save, parent, false);
            button.name = name;
            button.gameObject.SetActive(true);
            button.onClick = new Button.ButtonClickedEvent();
            button.onClick.AddListener(() => { if (!_busy) action(); });
            SetLabel(button, text);
            hud.StyleButton(button);
            return button;
        }
    }

    private static void Place(RectTransform rect, float top, float height)
    {
        rect.anchorMin = Vector2.up;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(.5f, 1);
        rect.anchoredPosition = new Vector2(0, -top);
        rect.sizeDelta = new Vector2(-44, height);
    }

    private static void PlaceHalf(Button button, bool right)
    {
        var rect = (RectTransform)button.transform;
        Place(rect, 218, 42);
        rect.anchorMin = new Vector2(right ? .5f : 0, 1);
        rect.anchorMax = new Vector2(right ? 1 : .5f, 1);
    }

    private static void SetLabel(Button button, string value) =>
        button.GetComponentInChildren<Text>(true).text = value;

    internal void SetDebugSession(bool debug)
    {
        var rect = (RectTransform)_returnShortcut.transform;
        rect.anchoredPosition = _shortcutPosition - Vector2.right * (debug ? 0 : _saveWidth + 8);
    }

    internal void ShowSlots((DateTime? Date, string Name, int? Seed)[] slots)
    {
        Display("セーブデータを選択");
        for (var i = 0; i < _slots.Length; i++)
        {
            var number = i + SaveSlotCatalogApplication.FirstSlot;
            var slot = slots[i];
            var name = $"セーブ {number}　{(slot.Date.HasValue ? slot.Name : "新規開始")}";
            SetLabel(_slots[i], FitSlotName(_slots[i].GetComponentInChildren<Text>(true), name));
            var date = slot.Date?.ToLocalTime().ToString("yyyy/MM/dd HH:mm") ?? "";
            _slotDetails[i].text = slot.Seed is int seed ? $"{date}　シード {seed}" : date;
            _slots[i].gameObject.SetActive(true);
        }
        _debug.gameObject.SetActive(true);
    }

    private static string FitSlotName(Text label, string name)
    {
        var settings = label.GetGenerationSettings(Vector2.zero);
        var generator = label.cachedTextGeneratorForLayout;
        const float width = 350;
        if (generator.GetPreferredWidth(name, settings) / label.pixelsPerUnit <= width) return name;
        var starts = StringInfo.ParseCombiningCharacters(name);
        var low = 0;
        var high = starts.Length - 1;
        while (low < high)
        {
            var end = low + (high - low + 1) / 2;
            var shortened = name.Substring(0, starts[end]) + "…";
            if (generator.GetPreferredWidth(shortened, settings) / label.pixelsPerUnit <= width) low = end;
            else high = end - 1;
        }
        return name.Substring(0, starts[low]) + "…";
    }

    internal void ShowNewGame(int slot)
    {
        _name.text = SaveSlotCatalogApplication.DefaultName(slot);
        _seed.text = "";
        ShowNewGameForm("");
    }

    internal void ShowNewGameForm(string message)
    {
        Display("新しいゲーム");
        _nameLabel.gameObject.SetActive(true); _seedLabel.gameObject.SetActive(true);
        _name.gameObject.SetActive(true); _seed.gameObject.SetActive(true);
        _start.gameObject.SetActive(true); _back.gameObject.SetActive(true);
        _status.text = message;
    }

    internal void ShowTerrainPreparation()
    {
        Display("地形を準備中…");
        _back.gameObject.SetActive(true);
    }

    internal void ShowReturn(bool canSave)
    {
        Display("セーブ選択へ戻る");
        _saveAndReturn.gameObject.SetActive(canSave);
        _return.gameObject.SetActive(true);
        SetLabel(_return, canSave ? "保存せず戻る" : "戻る");
        _cancel.gameObject.SetActive(true);
    }

    internal void ShowFailure(string status)
    {
        Display(status);
        _retry.gameObject.SetActive(true);
    }

    internal void SetBusy(string status)
    {
        _busy = true;
        _status.text = status;
        foreach (var button in _panel.GetComponentsInChildren<Button>())
            button.interactable = false;
    }

    internal void SetReturnFailure(string status)
    {
        _busy = false;
        _status.text = status;
        foreach (var button in _panel.GetComponentsInChildren<Button>())
            button.interactable = true;
    }

    private void Display(string title)
    {
        _busy = false;
        _title.text = title;
        _status.text = "";
        _nameLabel.gameObject.SetActive(false); _seedLabel.gameObject.SetActive(false);
        _name.gameObject.SetActive(false); _seed.gameObject.SetActive(false);
        foreach (var button in _panel.GetComponentsInChildren<Button>(true))
        {
            button.gameObject.SetActive(false);
            button.interactable = true;
        }
        _overlay.SetActive(true);
    }

    internal void Hide()
    {
        _busy = false;
        _overlay.SetActive(false);
    }
}

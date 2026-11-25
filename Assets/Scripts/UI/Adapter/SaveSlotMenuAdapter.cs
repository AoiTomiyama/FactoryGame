using System;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;
using System.Linq;

/// <summary>Prefab のセーブメニューに操作と表示データを渡す。</summary>
public sealed class SaveSlotMenuAdapter : MonoBehaviour
{
    [Serializable]
    private sealed class SlotRow
    {
        public Button Button;
        public Text Name;
        public Text Details;
        public bool IsBound => Button != null && Name != null && Details != null;
    }

    [SerializeField] private Text title, status;
    [SerializeField] private GameObject slotList, newGameForm, returnActions;
    [SerializeField] private SlotRow[] slots;
    [SerializeField] private Button debugSlot, saveAndReturn, returnWithoutSaving, cancelReturn, retrySlotSelection;
    [SerializeField] private Text returnWithoutSavingLabel;
    [SerializeField] private Button start, back;
    [SerializeField] private InputField saveName, worldSeed;
    private RectTransform _returnShortcut;
    private Vector2 _shortcutPosition;
    private float _debugShortcutOffset;
    private bool _busy;
    public bool BlocksGameplay => gameObject.activeInHierarchy;

    internal void ValidateBindings()
    {
        if (title == null || status == null || slotList == null || newGameForm == null || returnActions == null ||
            slots == null || slots.Length != SaveSlotCatalogApplication.NormalSlotCount || slots.Any(slot => slot == null || !slot.IsBound) ||
            debugSlot == null || saveAndReturn == null || returnWithoutSaving == null || returnWithoutSavingLabel == null ||
            cancelReturn == null || retrySlotSelection == null || start == null || back == null || saveName == null || worldSeed == null)
            throw new MissingReferenceException($"{nameof(SaveSlotMenuAdapter)}: incomplete bindings on '{name}' (Scene: '{gameObject.scene.path}').");
    }

    internal void Configure(HudLayoutAdapter hud, Action<int> chooseSlot, Action chooseDebug, Action openReturn,
        Action<bool> returnToSlots, Action recover, Action<string, int?> beginNewGame, Action cancelNewGame)
    {
        for (var i = 0; i < slots.Length; i++)
        {
            var slot = i + SaveSlotCatalogApplication.FirstSlot;
            Bind(slots[i].Button, () => chooseSlot(slot));
        }
        Bind(debugSlot, chooseDebug);
        Bind(saveAndReturn, () => returnToSlots(true));
        Bind(returnWithoutSaving, () => returnToSlots(false));
        Bind(cancelReturn, Hide);
        Bind(retrySlotSelection, recover);
        Bind(back, cancelNewGame);
        Bind(start, () => SubmitNewGame(beginNewGame));
        Bind(hud.ReturnToSlots, openReturn);

        _returnShortcut = (RectTransform)hud.ReturnToSlots.transform;
        _shortcutPosition = _returnShortcut.anchoredPosition;
        _debugShortcutOffset = ((RectTransform)hud.Save.transform).rect.width + 8;
        SetDebugSession(false);
        Hide();

        void Bind(Button button, Action action)
        {
            button.onClick.AddListener(() => { if (!_busy) action(); });
            hud.StyleButton(button);
        }
    }

    private void SubmitNewGame(Action<string, int?> beginNewGame)
    {
        var input = worldSeed.text.Trim();
        if (input.Length == 0)
        {
            beginNewGame(saveName.text, null);
            return;
        }
        if (int.TryParse(input, NumberStyles.Integer, CultureInfo.InvariantCulture, out var seed))
            beginNewGame(saveName.text, seed);
        else
            status.text = "シードは -2147483648 ～ 2147483647 の整数";
    }

    internal void SetDebugSession(bool debug)
        => _returnShortcut.anchoredPosition = _shortcutPosition + Vector2.right * (debug ? _debugShortcutOffset : 0);

    internal void ShowSlots((DateTime? Date, string Name, int? Seed)[] summaries)
    {
        Display("セーブデータを選択");
        slotList.SetActive(true);
        for (var i = 0; i < slots.Length; i++)
        {
            var number = i + SaveSlotCatalogApplication.FirstSlot;
            var summary = summaries[i];
            var row = slots[i];
            var name = $"セーブ {number}　{(summary.Date.HasValue ? summary.Name : "新規開始")}";
            row.Name.text = FitSlotName(row.Name, name);
            var date = summary.Date?.ToLocalTime().ToString("yyyy/MM/dd HH:mm") ?? "";
            row.Details.text = summary.Seed is int seed ? $"{date}　シード {seed}" : date;
        }
    }

    private static string FitSlotName(Text label, string name)
    {
        var settings = label.GetGenerationSettings(Vector2.zero);
        var generator = label.cachedTextGeneratorForLayout;
        var width = label.rectTransform.rect.width;
        if (width <= 0) return name;
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
        saveName.text = SaveSlotCatalogApplication.DefaultName(slot);
        worldSeed.text = "";
        ShowNewGameForm("");
    }

    internal void ShowNewGameForm(string message)
    {
        Display("新しいゲーム");
        newGameForm.SetActive(true);
        back.gameObject.SetActive(true);
        status.text = message;
    }

    internal void ShowTerrainPreparation()
    {
        Display("地形を準備中…");
        back.gameObject.SetActive(true);
    }

    internal void ShowReturn(bool canSave)
    {
        Display("セーブ選択へ戻る");
        returnActions.SetActive(true);
        saveAndReturn.gameObject.SetActive(canSave);
        returnWithoutSavingLabel.text = canSave ? "保存せず戻る" : "戻る";
    }

    internal void ShowFailure(string message)
    {
        Display(message);
        retrySlotSelection.gameObject.SetActive(true);
    }

    internal void SetBusy(string message)
    {
        _busy = true;
        status.text = message;
        foreach (var button in GetComponentsInChildren<Button>())
            button.interactable = false;
    }

    internal void SetReturnFailure(string message)
    {
        _busy = false;
        status.text = message;
        foreach (var button in GetComponentsInChildren<Button>())
            button.interactable = true;
    }

    private void Display(string heading)
    {
        _busy = false;
        title.text = heading;
        status.text = "";
        slotList.SetActive(false);
        newGameForm.SetActive(false);
        returnActions.SetActive(false);
        back.gameObject.SetActive(false);
        retrySlotSelection.gameObject.SetActive(false);
        foreach (var button in GetComponentsInChildren<Button>(true)) button.interactable = true;
        gameObject.SetActive(true);
    }

    internal void Hide()
    {
        _busy = false;
        gameObject.SetActive(false);
    }
}

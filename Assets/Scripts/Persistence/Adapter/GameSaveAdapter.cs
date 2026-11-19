using System;
using System.IO;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>手動保存要求を同期取引の外へ送り、取得済みデータだけをファイルへ書く。</summary>
public sealed class GameSaveAdapter : MonoBehaviour
{
    private SaveSlotCatalogApplication _slots;
    private GameInitializerAdapter _game;
    private SaveWorldAdapter _world;
    private Button _button;
    private Text _label;
    private bool _requested;
    private UniTaskCompletionSource<bool> _completion;
    public int SelectedSlot { get; private set; } = SaveSlotCatalogApplication.FirstSlot;
    public bool IsDebug { get; private set; }
    public bool IsSaving { get; private set; }
    public Exception LastError { get; private set; }
    public long SavedRevision { get; private set; }
    public SaveSlotCatalogApplication Slots => _slots ??= new SaveSlotCatalogApplication(Path.Combine(Application.persistentDataPath, "Saves"));
    public SaveFileApplication Storage => Slots.FileFor(SelectedSlot);
    // Test RunnerはSceneのStartより先に専用ディレクトリを注入する。
    public void SetDirectory(string directory)
    {
        if (_game != null || IsSaving) throw new InvalidOperationException("Set save storage before startup.");
        _slots = new SaveSlotCatalogApplication(directory);
    }
    internal void SelectSlot(int slot)
    {
        if (!SaveSlotCatalogApplication.IsNormal(slot)) throw new ArgumentOutOfRangeException(nameof(slot));
        SelectedSlot = slot;
        IsDebug = false;
        _button.gameObject.SetActive(true);
    }
    internal void SelectDebug()
    {
        IsDebug = true;
        _button.gameObject.SetActive(false);
    }
    public void Configure(GameInitializerAdapter game, SaveWorldAdapter world, Button button)
    {
        _game = game; _world = world;
        _button = button != null ? button : throw new MissingReferenceException("HUD save button is missing.");
        _label = button.GetComponentInChildren<Text>(true);
        button.onClick.AddListener(SaveClicked);
    }
    private void SaveClicked() => RequestSave();
    public bool RequestSave()
    {
        if (_game == null || _game.Session.CurrentStage != GameStartApplication.Stage.Running || IsSaving || IsDebug) return false;
        _completion = new UniTaskCompletionSource<bool>();
        IsSaving = true; _requested = true; LastError = null;
        _button.interactable = false; _label.text = "保存中…";
        return true;
    }
    internal UniTask<bool> CompleteSaveBeforeReturn(bool save)
    {
        if (IsSaving) return _completion.Task;
        return save ? RequestSave() ? _completion.Task : UniTask.FromResult(false) : UniTask.FromResult(true);
    }
    private void Update()
    { if (_game != null && !_game.SaveMenuBlocksInput && Keyboard.current?.f5Key.wasPressedThisFrame == true) RequestSave(); }
    private void LateUpdate()
    {
        if (!_requested) return;
        _requested = false;
        SaveAsync();
    }
    private async void SaveAsync()
    {
        var succeeded = false;
        try
        {
            // awaitより前に全状態を写す。通知中の要求はLateUpdateへ集約される。
            var data = _world.Capture(_game,Math.Max(DateTime.UtcNow.Ticks,SavedRevision + 1));
            await _world.ValidateAsync(data,_game.Skills.Catalog,_game.RegisteredResources,_game.Deliveries.Catalog,destroyCancellationToken);
            var storage = Storage;
            await Task.Run(() => storage.Write(SaveJsonAdapter.Encode(data, parallelValidation: true)));
            SavedRevision = data.revision;
            succeeded = true;
            if (this != null && _label != null) _label.text = "保存済み";
        }
        catch (OperationCanceledException) when (this == null) { }
        catch (Exception exception)
        {
            Debug.LogError($"Game save failed: {exception}",this);
            LastError = exception;
            if (this != null && _label != null) _label.text = "保存失敗";
        }
        finally
        {
            var completion = _completion;
            _completion = null;
            IsSaving = false;
            if (this != null && _button != null) _button.interactable = true;
            completion.TrySetResult(succeeded);
        }
    }
    public SaveGameApplication Read() => SaveJsonAdapter.Decode(Storage.Read());
    public Task<SaveGameApplication> ReadAsync(System.Threading.CancellationToken cancellation)
    {
        var storage = Storage;
        return Task.Run(() => SaveJsonAdapter.Decode(storage.Read(), parallelValidation: true), cancellation);
    }
    private void OnDestroy() { if (_button != null) _button.onClick.RemoveListener(SaveClicked); }
}

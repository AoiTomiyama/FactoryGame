using System;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>手動保存要求を同期取引の外へ送り、取得済みデータだけをファイルへ書く。</summary>
public sealed class GameSaveAdapter : MonoBehaviour
{
    private SaveFileApplication _storage;
    private GameInitializerAdapter _game;
    private SaveWorldAdapter _world;
    private Button _button;
    private Text _label;
    private bool _requested;
    public bool IsSaving { get; private set; }
    public Exception LastError { get; private set; }
    public long SavedRevision { get; private set; }
    public SaveFileApplication Storage => _storage ??= new SaveFileApplication(Path.Combine(Application.persistentDataPath,"Saves","factory.json"));
    // Test RunnerはSceneのStartより先に専用ディレクトリを注入する。
    public void SetStorage(SaveFileApplication storage)
    {
        if (_game != null || IsSaving) throw new InvalidOperationException("Set save storage before startup.");
        _storage = storage ?? throw new ArgumentNullException(nameof(storage));
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
        if (_game == null || _game.Session.CurrentStage != GameStartApplication.Stage.Running || IsSaving) return false;
        IsSaving = true; _requested = true; LastError = null;
        _button.interactable = false; _label.text = "保存中…";
        return true;
    }
    private void Update()
    { if (Keyboard.current?.f5Key.wasPressedThisFrame == true) RequestSave(); }
    private void LateUpdate()
    {
        if (!_requested) return;
        _requested = false;
        SaveAsync();
    }
    private async void SaveAsync()
    {
        try
        {
            // awaitより前に全状態を写す。通知中の要求はLateUpdateへ集約される。
            var data = _world.Capture(_game,Math.Max(DateTime.UtcNow.Ticks,SavedRevision + 1));
            _world.Validate(data,_game.Skills.Catalog,_game.RegisteredResources);
            var json = SaveJsonAdapter.Encode(data);
            var storage = Storage;
            await Task.Run(() => storage.Write(json));
            SavedRevision = data.revision;
            if (this != null && _label != null) _label.text = "保存済み";
        }
        catch (Exception exception)
        {
            Debug.LogError($"Game save failed: {exception}",this);
            LastError = exception;
            if (this != null && _label != null) _label.text = "保存失敗";
        }
        finally { IsSaving = false; if (this != null && _button != null) _button.interactable = true; }
    }
    public SaveGameApplication Read() => SaveJsonAdapter.Decode(Storage.Read());
    private void OnDestroy() { if (_button != null) _button.onClick.RemoveListener(SaveClicked); }
}

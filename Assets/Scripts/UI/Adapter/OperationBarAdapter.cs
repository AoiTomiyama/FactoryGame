using UnityEngine;
using UnityEngine.UI;

/// <summary>保存済み操作帯のボタンを既存入力へ接続する。</summary>
public sealed class OperationBarAdapter : MonoBehaviour
{
    [SerializeField] private Button rotateButton, viewButton, walkButton;
    [SerializeField] private RectTransform walkingHelp;
    [SerializeField] private RectTransform bar;
    [SerializeField] private Text placementInfo;
    public Text PlacementInfo => placementInfo;
    public RectTransform WalkingHelp => walkingHelp;
    public Button WalkButton => walkButton;
    private bool _placementActive, _firstPerson;
    private StartSiteSelectionAdapter _startSelection;
    private System.Action _newGame, _continue;
    private bool _canContinue;
    private bool InMenu => _newGame != null;
    private readonly System.Collections.Generic.Dictionary<Text,string> _savedStartText = new();
    private void RememberStartText()
    {
        if (_savedStartText.Count == 0)
            foreach (var text in bar.GetComponentsInChildren<Text>(true)) _savedStartText[text] = text.text;
    }
    public void SetStartMenu(System.Action newGame, System.Action resume, bool canResume)
    {
        RememberStartText();
        _newGame = newGame; _continue = resume; _canContinue = canResume;
        rotateButton.GetComponentInChildren<Text>(true).text = "新規開始";
        viewButton.GetComponentInChildren<Text>(true).text = "続きから";
        walkButton.gameObject.SetActive(false);
        foreach (var pair in _savedStartText)
            if (pair.Key.GetComponentInParent<Button>() == null) pair.Key.text = "";
        walkingHelp.gameObject.SetActive(false); bar.gameObject.SetActive(true);
        BindButtons(); RefreshStartAvailability();
    }
    public void SetStartSelection(StartSiteSelectionAdapter selection)
    {
        _startSelection=selection;
        _newGame = _continue = null;
        walkButton.gameObject.SetActive(true);
        if(selection!=null)
        {
            RememberStartText();
            rotateButton.GetComponentInChildren<Text>(true).text="前の候補";
            viewButton.GetComponentInChildren<Text>(true).text="次の候補";
            walkButton.GetComponentInChildren<Text>(true).text="ここから開始";
            var line=0;
            foreach(var pair in _savedStartText)
                if(pair.Key!=placementInfo && pair.Key.GetComponentInParent<Button>()==null)
                    pair.Key.text=line++==0 ? "候補地をクリックして選択\n中ドラッグ：移動　ホイール：拡大・縮小" : "";
            walkingHelp.gameObject.SetActive(false);
        }
        else
        {
            foreach(var pair in _savedStartText) if(pair.Key!=null) pair.Key.text=pair.Value;
            _savedStartText.Clear();
        }
        BindButtons();
        RefreshStartAvailability();
        if(bar!=null) bar.gameObject.SetActive(selection!=null || _placementActive && !_firstPerson);
    }
    public void RefreshStartAvailability()
    {
        // 入力側のConfigureが後でも、候補の操作可否を上書きしない。
        if (_startSelection != null)
            rotateButton.interactable = viewButton.interactable = walkButton.interactable = _startSelection.CanChoose;
        else if (InMenu) { rotateButton.interactable = true; viewButton.interactable = _canContinue; }
        else
        { rotateButton.interactable = walkButton.interactable = true; viewButton.interactable = _cameraController != null; }
    }
    private void BindButtons()
    {
        rotateButton.onClick.RemoveListener(Rotate);viewButton.onClick.RemoveListener(SwitchView);walkButton.onClick.RemoveListener(Walk);
        rotateButton.onClick.AddListener(Rotate);viewButton.onClick.AddListener(SwitchView);walkButton.onClick.AddListener(Walk);
    }
    public void SetPlacementActive(bool active)
    {
        // シーン終了時は操作帯が先に破棄される場合もある。
        _placementActive = active;
        if (bar != null) bar.gameObject.SetActive(InMenu || _startSelection!=null || active && !_firstPerson);
    }
    public void SetFirstPersonMode(bool active)
    {
        _firstPerson = active;
        if (walkingHelp != null) walkingHelp.gameObject.SetActive(active);
        if (bar != null) bar.gameObject.SetActive(InMenu || _startSelection!=null || _placementActive && !active);
    }
    private PlayerCursorBehaviourAdapter _cursor;
    private CameraControllerAdapter _cameraController;
    public Button RotateButton => rotateButton;
    public Button ViewButton => viewButton;
    public RectTransform Bar => bar;
    public void Configure(PlayerCursorBehaviourAdapter cursor, CameraControllerAdapter cameraController)
    {
        HudMotionAdapter.Reveal(bar);
        HudMotionAdapter.Reveal(walkingHelp);
        rotateButton.onClick.RemoveListener(Rotate);
        viewButton.onClick.RemoveListener(SwitchView);
        walkButton.onClick.RemoveListener(Walk);
        _cursor = cursor; _cameraController = cameraController;
        rotateButton.onClick.AddListener(Rotate);
        viewButton.onClick.AddListener(SwitchView);
        walkButton.onClick.AddListener(Walk);
        RefreshStartAvailability();
        foreach (var root in gameObject.scene.GetRootGameObjects())
        {
            var hud = root.GetComponentInChildren<HudLayoutAdapter>(true);
            if (hud == null) continue;
            ApplyColors(hud);
            break;
        }
    }
    public void ApplyColors(HudLayoutAdapter hud)
    {
        hud.StyleButton(rotateButton); hud.StyleButton(viewButton); hud.StyleButton(walkButton);
    }
    private void Rotate() { if(InMenu) _newGame(); else if(_startSelection!=null) _startSelection.Step(-1); else _cursor?.RotatePlacement(); }
    private void SwitchView() { if(InMenu) { if(_canContinue) _continue(); } else if(_startSelection!=null) _startSelection.Step(1); else _cameraController?.SwitchCameraAngle(); }
    private void Walk() { if(_startSelection!=null) _startSelection.Confirm(); else _cameraController?.ToggleFirstPerson(); }
    private void OnDestroy()
    {
        if (rotateButton != null) rotateButton.onClick.RemoveListener(Rotate);
        if (viewButton != null) viewButton.onClick.RemoveListener(SwitchView);
        if (walkButton != null) walkButton.onClick.RemoveListener(Walk);
    }
}

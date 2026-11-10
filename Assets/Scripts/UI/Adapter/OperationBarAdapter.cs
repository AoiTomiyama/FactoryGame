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
    public void SetPlacementActive(bool active)
    {
        // シーン終了時は操作帯が先に破棄される場合もある。
        _placementActive = active;
        if (bar != null) bar.gameObject.SetActive(active && !_firstPerson);
    }
    public void SetFirstPersonMode(bool active)
    {
        _firstPerson = active;
        if (walkingHelp != null) walkingHelp.gameObject.SetActive(active);
        if (bar != null) bar.gameObject.SetActive(_placementActive && !active);
    }
    private PlayerCursorBehaviourAdapter _cursor;
    private CameraControllerAdapter _cameraController;
    public Button RotateButton => rotateButton;
    public Button ViewButton => viewButton;
    public RectTransform Bar => bar;
    public void Configure(PlayerCursorBehaviourAdapter cursor, CameraControllerAdapter cameraController)
    {
        rotateButton.onClick.RemoveListener(Rotate);
        viewButton.onClick.RemoveListener(SwitchView);
        walkButton.onClick.RemoveListener(Walk);
        _cursor = cursor; _cameraController = cameraController;
        rotateButton.onClick.AddListener(Rotate);
        viewButton.onClick.AddListener(SwitchView);
        walkButton.onClick.AddListener(Walk);
        viewButton.interactable = cameraController != null;
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
    private void Rotate() => _cursor.RotatePlacement();
    private void SwitchView() => _cameraController?.SwitchCameraAngle();
    private void Walk() => _cameraController?.ToggleFirstPerson();
    private void OnDestroy()
    {
        if (rotateButton != null) rotateButton.onClick.RemoveListener(Rotate);
        if (viewButton != null) viewButton.onClick.RemoveListener(SwitchView);
        if (walkButton != null) walkButton.onClick.RemoveListener(Walk);
    }
}

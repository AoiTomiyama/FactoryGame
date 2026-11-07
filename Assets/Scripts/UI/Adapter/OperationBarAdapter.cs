using UnityEngine;
using UnityEngine.UI;

/// <summary>保存済み操作帯のボタンを既存入力へ接続する。</summary>
public sealed class OperationBarAdapter : MonoBehaviour
{
    [SerializeField] private Button rotateButton, viewButton;
    [SerializeField] private RectTransform bar;
    private PlayerCursorBehaviourAdapter _cursor;
    private CameraControllerAdapter _cameraController;
    public Button RotateButton => rotateButton;
    public Button ViewButton => viewButton;
    public RectTransform Bar => bar;
    public void Configure(PlayerCursorBehaviourAdapter cursor, CameraControllerAdapter cameraController)
    {
        rotateButton.onClick.RemoveListener(Rotate);
        viewButton.onClick.RemoveListener(SwitchView);
        _cursor = cursor; _cameraController = cameraController;
        rotateButton.onClick.AddListener(Rotate);
        viewButton.onClick.AddListener(SwitchView);
        viewButton.interactable = cameraController != null;
        foreach (var root in gameObject.scene.GetRootGameObjects())
        {
            var hud = root.GetComponentInChildren<HudLayoutAdapter>(true);
            if (hud == null) continue;
            bar.GetComponent<Image>().color = hud.IdleTabColor;
            hud.StyleButton(rotateButton); hud.StyleButton(viewButton);
            break;
        }
    }
    private void Rotate() => _cursor.RotatePlacement();
    private void SwitchView() => _cameraController?.SwitchCameraAngle();
    private void OnDestroy()
    {
        rotateButton.onClick.RemoveListener(Rotate);
        viewButton.onClick.RemoveListener(SwitchView);
    }
}

using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;

/// <summary>常時表示の操作案内と、既存入力処理へのボタンを所有する。</summary>
public sealed class OperationBarAdapter : MonoBehaviour
{
    private GameObject _canvas;
    private Font _font;
    public Button RotateButton { get; private set; }
    public Button ViewButton { get; private set; }
    public RectTransform Bar { get; private set; }

    public void Configure(PlayerCursorBehaviourAdapter cursor, CameraControllerAdapter cameraController)
    {
        _font = Font.CreateDynamicFontFromOSFont(new[] { "Yu Gothic", "Meiryo", "Arial" }, 16);
        _canvas = new GameObject("OperationCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        _canvas.transform.SetParent(transform, false);
        _canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.GetComponent<Canvas>().sortingOrder = 21;
        var scaler = _canvas.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280, 720);
        scaler.matchWidthOrHeight = 1;
        var panel = new GameObject("OperationBar", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(_canvas.transform, false);
        panel.GetComponent<Image>().color = new Color(0.07f, 0.09f, 0.12f, 0.96f);
        Bar = panel.GetComponent<RectTransform>();
        Bar.anchorMin = Vector2.zero; Bar.anchorMax = Vector2.right;
        Bar.pivot = new Vector2(0.5f, 0);
        Bar.offsetMin = new Vector2(12, 12); Bar.offsetMax = new Vector2(-12, 96);
        var hint = Label(panel.transform, "左ドラッグ：配置　右クリック：設備情報　中ドラッグ：移動　ホイール：拡大・縮小");
        Place(hint.rectTransform, new Vector2(12, 46), new Vector2(-12, 78), true);
        RotateButton = MakeButton(panel.transform, "設備回転 [R]", cursor.RotatePlacement, 12);
        ViewButton = MakeButton(panel.transform, "視点切替 [L]", cameraController != null ? cameraController.SwitchCameraAngle : () => { }, 192);
        ViewButton.interactable = cameraController != null;
    }

    private Button MakeButton(Transform parent, string title, UnityAction action, float x)
    {
        var go = new GameObject(title, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        go.GetComponent<Image>().color = new Color(0.22f, 0.34f, 0.43f);
        Place(go.GetComponent<RectTransform>(), new Vector2(x, 8), new Vector2(x + 168, 44), false);
        var button = go.GetComponent<Button>();
        button.onClick.AddListener(action);
        var text = Label(go.transform, title);
        text.alignment = TextAnchor.MiddleCenter;
        text.rectTransform.anchorMin = Vector2.zero;
        text.rectTransform.anchorMax = Vector2.one;
        text.rectTransform.offsetMin = text.rectTransform.offsetMax = Vector2.zero;
        return button;
    }

    private Text Label(Transform parent, string value)
    {
        var go = new GameObject("Label", typeof(RectTransform), typeof(Text));
        go.transform.SetParent(parent, false);
        var text = go.GetComponent<Text>();
        text.font = _font; text.fontSize = 16; text.color = Color.white;
        text.text = value; text.alignment = TextAnchor.MiddleLeft; text.raycastTarget = false;
        return text;
    }

    private static void Place(RectTransform rect, Vector2 min, Vector2 max, bool stretch)
    {
        rect.anchorMin = Vector2.zero; rect.anchorMax = stretch ? Vector2.right : Vector2.zero;
        rect.offsetMin = min; rect.offsetMax = max;
    }
    private void OnEnable() { if (_canvas != null) _canvas.SetActive(true); }
    private void OnDisable() { if (_canvas != null) _canvas.SetActive(false); }
    private void OnDestroy()
    {
        if (_canvas != null) Destroy(_canvas);
        if (_font != null) Destroy(_font);
    }
}

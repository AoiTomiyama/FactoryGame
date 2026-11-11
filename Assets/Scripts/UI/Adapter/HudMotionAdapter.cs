using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>保存済みUIの表示だけを動かす。入力状態とレイアウトの寸法は変更しない。</summary>
public sealed class HudMotionAdapter : MonoBehaviour, IPointerDownHandler, IPointerUpHandler,
    IPointerExitHandler, ISubmitHandler
{
    private Vector3 _scale;
    private Tween _motion, _fade;
    private Button _button;
    private CanvasGroup _visibility;
    private float _alpha;
    private bool _reveal;
    public bool IsAnimating => _motion != null && _motion.IsActive();

    public static HudMotionAdapter Bind(Component target)
    {
        var motion = target.GetComponent<HudMotionAdapter>();
        return motion != null ? motion : target.gameObject.AddComponent<HudMotionAdapter>();
    }
    public static void Reveal(Component target)
    {
        if (!Application.isPlaying) return;
        var motion = Bind(target);
        if (motion._reveal) return;
        motion._reveal = true;
        motion._visibility = target.GetComponent<CanvasGroup>();
        if (motion._visibility == null) motion._visibility = target.gameObject.AddComponent<CanvasGroup>();
        motion._alpha = motion._visibility.alpha;
        if (motion.isActiveAndEnabled) motion.PlayReveal();
    }
    private void Awake()
    {
        _scale = transform.localScale;
        _button = GetComponent<Button>();
    }
    private void OnEnable()
    {
        if (_reveal) PlayReveal();
    }
    private void PlayReveal()
    {
        ResetMotion();
        transform.localScale = _scale * .98f;
        _visibility.alpha = 0;
        _motion = transform.DOScale(_scale, .16f).SetEase(Ease.OutCubic).SetUpdate(true);
        _fade = _visibility.DOFade(_alpha, .16f).SetEase(Ease.OutCubic).SetUpdate(true);
    }
    public void Pulse()
    {
        if (!isActiveAndEnabled) return;
        _motion?.Kill();
        // 現在の大きさから戻し、連続通知でも拡大を累積させない。
        _motion = DOTween.Sequence()
            .Append(transform.DOScale(_scale * 1.035f, .07f).SetEase(Ease.OutQuad))
            .Append(transform.DOScale(_scale, .15f).SetEase(Ease.OutCubic))
            .SetUpdate(true);
    }
    public void OnPointerDown(PointerEventData data)
    {
        if (data.button != PointerEventData.InputButton.Left || _button == null || !_button.IsInteractable()) return;
        _motion?.Kill();
        _motion = transform.DOScale(_scale * .96f, .06f).SetEase(Ease.OutQuad).SetUpdate(true);
    }
    public void OnPointerUp(PointerEventData data) => Release();
    public void OnPointerExit(PointerEventData data) => Release();
    public void OnSubmit(BaseEventData data)
    {
        if (_button != null && _button.IsInteractable()) Pulse();
    }
    private void Release()
    {
        if (!isActiveAndEnabled || _button == null) return;
        _motion?.Kill();
        _motion = transform.DOScale(_scale, .12f).SetEase(Ease.OutCubic).SetUpdate(true);
    }
    private void ResetMotion()
    {
        _motion?.Kill(); _fade?.Kill();
        transform.localScale = _scale;
        if (_visibility != null) _visibility.alpha = _alpha;
    }
    // 非表示化を遅延させず、再表示時に押下や途中の透明度を持ち越さない。
    private void OnDisable() => ResetMotion();
    private void OnDestroy() => ResetMotion();
}

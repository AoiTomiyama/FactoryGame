using System.Collections.Generic;
using UnityEngine;

/// <summary>セーブ画面とプレイ中の HUD を切り替える。</summary>
public sealed class StartupHudAdapter : MonoBehaviour
{
    [SerializeField] private HudLayoutAdapter hud;
    [SerializeField] private OperationBarAdapter bar;
    [SerializeField] private CellPlacerAdapter placer;
    [SerializeField] private CellStatusViewAdapter statusView;

    public HudLayoutAdapter Layout => hud;
    internal SaveSlotMenuAdapter SaveSlotMenu => bar.SaveSlotMenu;

    public void BeginMenu()
    {
        ValidateBindings();
        placer.SetPlacementEnabled(false);
        statusView.SetStatusWindowActive(false);
        hud.gameObject.SetActive(false);
        bar.SetPlacementActive(false);
    }

    public void ValidateBindings()
    {
        var missing = new List<string>();
        if (hud == null) missing.Add(nameof(hud));
        if (bar == null) missing.Add(nameof(bar));
        if (placer == null) missing.Add(nameof(placer));
        if (statusView == null) missing.Add(nameof(statusView));
        if (hud != null && hud.Save == null) missing.Add("hud.Save");
        if (hud != null && hud.ReturnToSlots == null) missing.Add("hud.ReturnToSlots");
        if (bar != null && SaveSlotMenu == null) missing.Add("bar.SaveSlotMenu");
        if (missing.Count > 0)
            throw new MissingReferenceException($"{nameof(StartupHudAdapter)}: missing references [{string.Join(", ", missing)}] on '{name}' (Scene: '{gameObject.scene.path}').");
        SaveSlotMenu.ValidateBindings();
    }

    public void Finish() => hud.gameObject.SetActive(true);
}

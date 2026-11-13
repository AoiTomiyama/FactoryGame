using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>開始候補の表示と操作。ゲーム開始の構成は呼出側へ通知する。</summary>
public sealed class StartSiteSelectionAdapter : MonoBehaviour
{
    [SerializeField] private CameraControllerAdapter cameraController;
    [SerializeField] private HudLayoutAdapter hud;
    [SerializeField] private OperationBarAdapter bar;
    [SerializeField] private CellPlacerAdapter placer;
    [SerializeField] private CellStatusViewAdapter statusView;
    [SerializeField] private Material frameMaterial;
    private GameStartApplication _session;
    private CellFootprintDomain _footprint;
    private Action _confirm;
    private readonly List<GameObject> _roots = new();
    private readonly List<GameObject> _previews = new();
    private readonly List<LineRenderer> _frames = new();
    private GameObject _shipping;
    private bool _finished;
    public int SelectedIndex => _session?.SelectedIndex ?? 0;
    public int Count => _session?.Candidates.Count ?? 0;
    public bool CanChoose => _session != null && _session.CanChoose;

    public void ValidateBindings()
    {
        var missing = new List<string>();
        if (cameraController == null) missing.Add(nameof(cameraController));
        if (hud == null) missing.Add(nameof(hud));
        if (bar == null) missing.Add(nameof(bar));
        if (placer == null) missing.Add(nameof(placer));
        if (statusView == null) missing.Add(nameof(statusView));
        if (frameMaterial == null) missing.Add(nameof(frameMaterial));
        if (missing.Count > 0) ReportMissingReferences(missing);
        if (placer.PlaceholderPrefab(CellTypeDomain.Singularity) == null) missing.Add("placer.PlaceholderPrefab(Singularity)");
        if (placer.PlaceholderPrefab(CellTypeDomain.Shipping) == null) missing.Add("placer.PlaceholderPrefab(Shipping)");
        if (missing.Count > 0) ReportMissingReferences(missing);
    }
    private void ReportMissingReferences(List<string> missing)
        => throw new MissingReferenceException($"{nameof(StartSiteSelectionAdapter)}: missing references [{string.Join(", ", missing)}] on '{name}' (Scene: '{gameObject.scene.path}').");
    public void Begin(GameStartApplication session, CellFootprintDomain footprint, Action confirm)
    {
        ValidateBindings();
        _session = session; _footprint = footprint; _confirm = confirm;
        placer.SetPlacementEnabled(false);
        statusView.SetStatusWindowActive(false);
        hud.gameObject.SetActive(false);
        cameraController.SetStartSelection(true);
        foreach (var candidate in session.Candidates)
        {
            var root = new GameObject("StartSitePreview"); _roots.Add(root);
            root.transform.SetParent(transform, false);
            var frame = root.AddComponent<LineRenderer>();
            frame.sharedMaterial = frameMaterial;
            frame.useWorldSpace = true; frame.positionCount = 4; frame.loop = true; frame.widthMultiplier = .12f;
            var occupied = footprint.Coordinates(candidate.X, candidate.Z, 0).ToArray();
            var left = occupied.Min(p => p.x) - .5f; var right = occupied.Max(p => p.x) + .5f;
            var back = occupied.Min(p => p.z) - .5f; var front = occupied.Max(p => p.z) + .5f;
            frame.SetPositions(new[] { new Vector3(left,.55f,back), new Vector3(left,.55f,front),
                new Vector3(right,.55f,front), new Vector3(right,.55f,back) });
            _frames.Add(frame);
            var preview = Instantiate(placer.PlaceholderPrefab(CellTypeDomain.Singularity), new Vector3(candidate.X,.02f,candidate.Z), Quaternion.identity, root.transform);
            DisablePreviewInput(preview); _previews.Add(preview);
        }
        _shipping = Instantiate(placer.PlaceholderPrefab(CellTypeDomain.Shipping), transform);
        DisablePreviewInput(_shipping); _roots.Add(_shipping);
        bar.SetStartSelection(this);
        RefreshSelection();
    }
    private static void DisablePreviewInput(GameObject preview)
    {
        foreach (var collider in preview.GetComponentsInChildren<Collider>()) collider.enabled = false;
        foreach (var script in preview.GetComponentsInChildren<MonoBehaviour>()) script.enabled = false;
    }
    public bool SelectAt(Vector3 point)
    {
        if (!CanChoose) return false;
        for (var i = 0; i < Count; i++)
        {
            var candidate = _session.Candidates[i];
            if (_footprint.Coordinates(candidate.X,candidate.Z,0).Contains((Mathf.RoundToInt(point.x),Mathf.RoundToInt(point.z))) && _session.TrySelect(i))
            { RefreshSelection(); return true; }
        }
        return false;
    }
    public void Step(int delta)
    {
        if (CanChoose && _session.TrySelect((SelectedIndex + delta + Count) % Count)) RefreshSelection();
    }
    public void Confirm() { if (CanChoose) _confirm?.Invoke(); }
    public void RefreshSelection()
    {
        bar.RefreshStartAvailability();
        if (Count == 0)
        {
            _shipping.SetActive(false); bar.PlacementInfo.text = "開始できる候補がありません"; return;
        }
        for (var i = 0; i < _previews.Count; i++)
        {
            _previews[i].SetActive(i == SelectedIndex);
            var color = i == SelectedIndex ? hud.SelectedTabColor : hud.IdleTabColor;
            _frames[i].startColor = _frames[i].endColor = color;
            var properties = new MaterialPropertyBlock();
            properties.SetColor("_BaseColor",color); properties.SetColor("_Color",color); _frames[i].SetPropertyBlock(properties);
        }
        var selected = _session.Selected;
        _shipping.transform.position = new Vector3(selected.Shipping.x,.02f,selected.Shipping.z);
        bar.PlacementInfo.text = $"候補 {SelectedIndex + 1} / {Count}";
        cameraController.FocusOn(new Vector3(selected.X,0,selected.Z));
    }
    public void Finish()
    {
        if (_finished) return; _finished = true; _confirm = null;
        bar.SetStartSelection(null);
        cameraController.SetStartSelection(false);
        hud.gameObject.SetActive(true);
        DisposePreviews(); Destroy(this);
    }
    public void DisposePreviews()
    {
        _confirm = null;
        foreach (var root in _roots) if (root != null) { root.SetActive(false); Destroy(root); }
        _roots.Clear(); _previews.Clear(); _frames.Clear();
    }
    private void OnDestroy() => DisposePreviews();
}

using System.Collections.Generic;
using UnityEngine;

/// <summary>開始候補の地形プレビューと既存操作帯。確定後は通常のHUDへ操作を返す。</summary>
public sealed class StartSiteSelectionAdapter : MonoBehaviour
{
    private GridFieldGeneratorAdapter _generator;
    private CameraControllerAdapter _camera;
    private HudLayoutAdapter _hud;
    private OperationBarAdapter _bar;
    private readonly List<GameObject> _roots = new();
    private readonly List<GameObject> _previews = new();
    private readonly List<LineRenderer> _frames = new();
    private GameObject _shipping;
    private bool _finished;
    public int SelectedIndex => _generator.SelectedStartSite;
    public int Count => _generator.StartSites.Count;
    public void Begin(GridFieldGeneratorAdapter generator)
    {
        _generator=generator;
        _camera=FindAnyObjectByType<CameraControllerAdapter>();
        _hud=FindAnyObjectByType<HudLayoutAdapter>(FindObjectsInactive.Include);
        _bar=FindAnyObjectByType<OperationBarAdapter>(FindObjectsInactive.Include);
        var placer=FindAnyObjectByType<CellPlacerAdapter>();
        placer.SetPlacementEnabled(false);
        FindAnyObjectByType<CellStatusViewAdapter>()?.SetStatusWindowActive(false);
        // 開始前は購入用HUDを構成せず、保存済み操作帯の3ボタンを使う。
        _hud.gameObject.SetActive(false);
        _camera?.SetStartSelection(true);
        foreach(var anchor in generator.StartSites)
        {
            var root=new GameObject("StartSitePreview");_roots.Add(root);
            var frame=root.AddComponent<LineRenderer>();
            frame.sharedMaterial=generator.GridLineMaterial;
            frame.useWorldSpace=true;frame.positionCount=4;frame.loop=true;frame.widthMultiplier=.12f;
            frame.SetPositions(new[]{new Vector3(anchor.x-2.5f,.55f,anchor.y-2.5f),new Vector3(anchor.x-2.5f,.55f,anchor.y+2.5f),
                new Vector3(anchor.x+2.5f,.55f,anchor.y+2.5f),new Vector3(anchor.x+2.5f,.55f,anchor.y-2.5f)});
            _frames.Add(frame);
            var preview=Instantiate(placer.PlaceholderPrefab(CellTypeDomain.Singularity),new Vector3(anchor.x,.02f,anchor.y),Quaternion.identity,root.transform);
            DisablePreviewInput(preview);_previews.Add(preview);
        }
        _shipping=Instantiate(placer.PlaceholderPrefab(CellTypeDomain.Shipping));
        DisablePreviewInput(_shipping);_roots.Add(_shipping);
        _bar.SetStartSelection(this);
        RefreshSelection();
    }
    private static void DisablePreviewInput(GameObject preview)
    {
        foreach(var collider in preview.GetComponentsInChildren<Collider>()) collider.enabled=false;
        foreach(var script in preview.GetComponentsInChildren<MonoBehaviour>()) script.enabled=false;
    }
    public void Step(int delta) { if(Count>0) _generator.SelectStartSite((SelectedIndex+delta+Count)%Count); }
    public void Confirm() => _generator.ConfirmStartSite();
    public void RefreshSelection()
    {
        if(Count==0)
        {
            _shipping.SetActive(false);_bar.PlacementInfo.text="開始できる候補がありません";return;
        }
        for(var i=0;i<_previews.Count;i++)
        {
            _previews[i].SetActive(i==SelectedIndex);
            var color=i==SelectedIndex ? _hud.SelectedTabColor : _hud.IdleTabColor;
            _frames[i].startColor=_frames[i].endColor=color;
            var properties=new MaterialPropertyBlock();
            properties.SetColor("_BaseColor",color);properties.SetColor("_Color",color);_frames[i].SetPropertyBlock(properties);
        }
        var anchor=_generator.StartSites[SelectedIndex];
        var shipping=_generator.ShippingPreviewPosition(anchor);
        _shipping.transform.position=new Vector3(shipping.x,.02f,shipping.y);
        _bar.PlacementInfo.text=$"候補 {SelectedIndex+1} / {Count}";
        _camera?.FocusOn(new Vector3(anchor.x,0,anchor.y));
    }
    public void Finish()
    {
        if(_finished) return;_finished=true;
        _bar.SetStartSelection(null);
        _camera?.SetStartSelection(false);
        _hud.gameObject.SetActive(true);
        ClearPreviews();Destroy(this);
    }
    private void ClearPreviews()
    {
        foreach(var root in _roots) if(root!=null) {root.SetActive(false);Destroy(root);}
        _roots.Clear();
    }
    private void OnDestroy() => ClearPreviews();
}

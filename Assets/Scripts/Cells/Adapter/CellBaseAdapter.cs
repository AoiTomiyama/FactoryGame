using System;
using UnityEngine;

public abstract class CellBaseAdapter : MonoBehaviour, IResourceTransferEndpointApplication
{
    public object Identity => this;
    // インターフェース参照でも、破棄されたUnityオブジェクトを寿命終了として判定する。
    public bool IsAlive => this != null;
    public int XIndex { get; set; }
    public int ZIndex { get; set; }
    public GameObject CellModel { get; private set; }

    private void Awake()
    {
        CellModel = transform.GetChild(0).gameObject;
    }

    public virtual void InitializeSystem()
    {
    }
}

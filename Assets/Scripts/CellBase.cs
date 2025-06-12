using System;
using UnityEngine;

public abstract class CellBase : MonoBehaviour
{
    public int xIndex { get; set; }
    public int zIndex { get; set; }
    public GameObject cellModel { get; private set; }

    private void Awake()
    {
        cellModel = transform.GetChild(0).gameObject;
    }
}

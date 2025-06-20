using UnityEngine;

public abstract class CellBase : MonoBehaviour
{
    public int XIndex { get; set; }
    public int ZIndex { get; set; }
    public GameObject CellModel { get; private set; }

    protected virtual void Awake()
    {
        CellModel = transform.GetChild(0).gameObject;
    }
}

using UnityEngine;

public abstract class CellBase : MonoBehaviour
{
    public int XIndex { get; set; }
    public int ZIndex { get; set; }
    public GameObject CellModel { get; private set; }

    private void Awake()
    {
        CellModel = transform.GetChild(0).gameObject;
    }
}

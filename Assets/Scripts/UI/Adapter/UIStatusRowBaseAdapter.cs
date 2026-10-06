using TMPro;
using UnityEngine;

public abstract class UIStatusRowBaseAdapter : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI statusNameText;

    public virtual void RenderUIByData(UIElementDataBaseApplication data)
    {
        statusNameText.text = data.StatusName;
    }
}
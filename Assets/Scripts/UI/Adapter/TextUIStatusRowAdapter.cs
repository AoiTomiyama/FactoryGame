using TMPro;
using UnityEngine;

public class TextUIStatusRowAdapter : UIStatusRowBaseAdapter
{
    [SerializeField] private TextMeshProUGUI statusText;

    public override void RenderUIByData(UIElementDataBaseApplication data)
    {
        base.RenderUIByData(data);
        if (data is not TextElementDataApplication textData)
        {
            Debug.LogError("Invalid data type for TextStatsRowUI.");
            return;
        }

        statusText.text = textData.Text;
    }
}
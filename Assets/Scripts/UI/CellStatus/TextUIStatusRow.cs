using TMPro;
using UnityEngine;

public class TextUIStatusRow : UIStatusRowBase
{
    [SerializeField] private TextMeshProUGUI statusText;

    public override void RenderUIByData(UIElementDataBase data)
    {
        base.RenderUIByData(data);
        if (data is not TextElementData textData)
        {
            Debug.LogError("Invalid data type for TextStatsRowUI.");
            return;
        }

        statusText.text = textData.text;
    }
}
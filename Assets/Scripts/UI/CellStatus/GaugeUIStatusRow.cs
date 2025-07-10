using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class GaugeUIStatusRow : UIStatusRowBase
{
    [SerializeField] private TextMeshProUGUI gaugeText;
    [SerializeField] private Image gauge;

    protected TextMeshProUGUI GaugeText => gaugeText;

    protected Image Gauge => gauge;

    public override void RenderUIByData(UIElementDataBase data)
    {
        base.RenderUIByData(data);
        if (data is not GaugeElementData gaugeData)
        {
            Debug.LogError("Invalid data type for GaugeStatsRowUI.");
            return;
        }

        Gauge.fillAmount = gaugeData.Current / gaugeData.Max;
        GaugeText.text = $"{gaugeData.Current}/{gaugeData.Max}";
    }
}
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class GaugeUIStatusRowAdapter : UIStatusRowBaseAdapter
{
    [SerializeField] private TextMeshProUGUI gaugeText;
    [SerializeField] private Image gauge;

    protected TextMeshProUGUI GaugeText => gaugeText;

    protected Image Gauge => gauge;

    public override void RenderUIByData(UIElementDataBaseApplication data)
    {
        base.RenderUIByData(data);
        if (data is not GaugeElementDataApplication gaugeData)
        {
            Debug.LogError("Invalid data type for GaugeStatsRowUI.");
            return;
        }

        Gauge.fillAmount = gaugeData.Current / gaugeData.Max;
        GaugeText.text = gaugeData.GaugeText;
    }
}
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class StorageUIStatusRowAdapter : GaugeUIStatusRowAdapter
{
    [SerializeField] private TextMeshProUGUI resourceText;
    [SerializeField] private Image resourceIcon;
    [SerializeField] private ResourceDefinition resourceDatabase;

    public override void RenderUIByData(UIElementDataBaseApplication data)
    {
        base.RenderUIByData(data);
        if (data is not StorageElementDataApplication storageData)
        {
            Debug.LogError("Invalid data type for StorageStatsRowUI.");
            return;
        }

        Gauge.fillAmount = storageData.Current / storageData.Max;
        GaugeText.text = $"{storageData.Current}/{storageData.Max}";

        resourceIcon.enabled = storageData.ResourceTypeDomain != ResourceTypeDomain.None;
        if (storageData.ResourceTypeDomain == ResourceTypeDomain.None)
        {
            resourceText.text = "";
        }

        var info = resourceDatabase.GetInfo(storageData.ResourceTypeDomain);
        resourceIcon.sprite = info.Icon;
        resourceText.text = info.Name;
    }
}
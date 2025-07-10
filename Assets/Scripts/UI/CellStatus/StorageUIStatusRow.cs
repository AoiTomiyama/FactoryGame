using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class StorageUIStatusRow : GaugeUIStatusRow
{
    [SerializeField] private TextMeshProUGUI resourceText;
    [SerializeField] private Image resourceIcon;
    [SerializeField] private ResourceSO resourceDatabase;

    public override void RenderUIByData(UIElementDataBase data)
    {
        base.RenderUIByData(data);
        if (data is not StorageElementData storageData)
        {
            Debug.LogError("Invalid data type for StorageStatsRowUI.");
            return;
        }

        Gauge.fillAmount = storageData.Current / storageData.Max;
        GaugeText.text = $"{storageData.Current}/{storageData.Max}";

        resourceIcon.enabled = storageData.ResourceType != ResourceType.None;
        if (storageData.ResourceType == ResourceType.None)
        {
            resourceText.text = "";
        }

        var info = resourceDatabase.GetInfo(storageData.ResourceType);
        resourceIcon.sprite = info.Icon;
        resourceText.text = info.Name;
    }
}
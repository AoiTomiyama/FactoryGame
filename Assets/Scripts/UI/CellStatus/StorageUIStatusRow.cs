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
        if (Gauge == null || resourceIcon == null || resourceText == null)
        {
            Debug.LogError("Gauge Image, Resource Icon or Resource Text is not assigned.");
            return;
        }

        if (data is not StorageElementData storageData)
        {
            Debug.LogError("Invalid data type for StorageStatsRowUI.");
            return;
        }

        Gauge.fillAmount = storageData.current / storageData.max;
        GaugeText.text = $"{storageData.current}/{storageData.max}";

        resourceIcon.enabled = storageData.resourceType != ResourceType.None;
        if (storageData.resourceType == ResourceType.None)
        {
            resourceText.text = "";
        }

        var info = resourceDatabase.GetInfo(storageData.resourceType);
        resourceIcon.sprite = info.Icon;
        resourceText.text = info.Name;
    }
}
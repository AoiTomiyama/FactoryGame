using UnityEngine;
using UnityEngine.UI;

/// <summary>既存Providerの表示値を、共通HUDの書体・色・寸法で描画する。</summary>
public sealed class HudStatusRowAdapter : UIStatusRowBaseAdapter
{
    private Text _label, _value;
    private Image _icon, _gauge;
    private ResourceDefinition _resources;
    public void Configure(Font font, ResourceDefinition resources)
    {
        _resources = resources;
        var layout = gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.padding = new RectOffset(8, 8, 4, 6); layout.spacing = 8;
        layout.childControlHeight = true; layout.childForceExpandHeight = false;
        layout.childForceExpandWidth = false;
        var sizing = gameObject.AddComponent<LayoutElement>(); sizing.preferredHeight = 44;
        _label = HudUIAdapter.MakeText(font, transform, "", 16, 32);
        _label.GetComponent<LayoutElement>().preferredWidth = 100;
        var icon = new GameObject("ResourceIcon", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        icon.transform.SetParent(transform, false);
        _icon = icon.GetComponent<Image>(); _icon.preserveAspect = true; _icon.raycastTarget = false;
        icon.GetComponent<LayoutElement>().minWidth = 24; icon.GetComponent<LayoutElement>().preferredWidth = 24;
        _value = HudUIAdapter.MakeText(font, transform, "", 16, 32);
        _value.alignment = TextAnchor.MiddleRight;
        _value.GetComponent<LayoutElement>().flexibleWidth = 1;
        var gauge = new GameObject("Gauge", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        gauge.transform.SetParent(transform, false);
        gauge.GetComponent<LayoutElement>().ignoreLayout = true;
        _gauge = gauge.GetComponent<Image>(); _gauge.color = HudUIAdapter.Selected; _gauge.raycastTarget = false;
    }
    public override void RenderUIByData(UIElementDataBaseApplication data)
    {
        _label.text = data.StatusName;
        _icon.gameObject.SetActive(false); _gauge.gameObject.SetActive(false);
        switch (data)
        {
            case StorageElementDataApplication stock:
                var resource = _resources != null ? _resources.GetResourceByType(stock.ResourceTypeDomain) : default;
                _icon.sprite = resource.Icon;
                _icon.gameObject.SetActive(resource.Icon != null);
                _value.text = $"{resource.ResourceName} {stock.Current}/{stock.Max}";
                SetGauge(stock);
                break;
            case GaugeElementDataApplication gauge:
                _value.text = gauge.GaugeText.Replace(" sec", " 秒"); SetGauge(gauge); break;
            case TextElementDataApplication text:
                _value.text = text.Text; break;
        }
    }
    private void SetGauge(GaugeElementDataApplication data)
    {
        _gauge.gameObject.SetActive(true);
        var rect = _gauge.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = new Vector2(data.Max > 0 ? Mathf.Clamp01(data.Current / data.Max) : 0, 0);
        rect.offsetMin = new Vector2(8, 2); rect.offsetMax = new Vector2(8, 5);
    }
}

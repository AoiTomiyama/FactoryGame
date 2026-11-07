using UnityEngine;
using UnityEngine.UI;

/// <summary>既存Providerの表示値を、共通HUDの書体・色・寸法で描画する。</summary>
public sealed class HudStatusRowAdapter : UIStatusRowBaseAdapter
{
    [SerializeField] private Text _label, _value;
    [SerializeField] private Image _icon, _gauge;
    private ResourceDefinition _resources;
    public void Configure(Font font, ResourceDefinition resources)
    {
        _resources = resources;
        if (_label.font == null) _label.font = font;
        if (_value.font == null) _value.font = font;
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

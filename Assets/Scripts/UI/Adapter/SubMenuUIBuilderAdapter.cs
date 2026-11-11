using System;
using UnityEngine;

/// <summary>配置ボタンと同じ定義・選択処理をサブメニューへ渡す。</summary>
public class SubMenuUIBuilderAdapter : MonoBehaviour
{
    [SerializeField] private Color subMenuColor = new Color(0.18f, 0.28f, 0.35f);
    private CellSelectButtonUIAdapter _buttonPrefab;
    private CellPlacerAdapter _cellPlacer;
    private Font _font;
    private CellDatabaseDefinition _database;

    public void SetDatabase(CellSelectButtonUIAdapter button, CellPlacerAdapter placer,
        CellDatabaseDefinition database = null, Font font = null)
    {
        _buttonPrefab = button; _cellPlacer = placer; _database = database; _font = font;
    }

    public Action<bool> CreateSubMenuSystem(CellTypeDomain type)
    {
        Action<bool> toggle = null;
        if (_database == null) return toggle;
        foreach (var info in _database.GetAllCellInfos())
        {
            if (type == CellTypeDomain.ExtractorWood)
            {
                if (!ResourceCellRulesDomain.IsExtractor(info.CellTypeDomain)) continue;
            }
            else if (info.CellTypeDomain != type) continue;
            var selected = info.CellTypeDomain;
            var button = Instantiate(_buttonPrefab, transform);
            button.name = $"{selected}Choice";
            button.Set(SelectButtonBuilderAdapter.PlacementLabel(selected), () => _cellPlacer.SetSelectedCellType(selected));
            var hud = GetComponentInParent<HudLayoutAdapter>();
            button.SetColor(hud != null ? hud.IdleTabColor : subMenuColor);
            if (_font != null) button.ApplyStyle(_font);
            toggle += active => button.gameObject.SetActive(active);
            button.gameObject.SetActive(false);
        }
        return toggle;
    }
}

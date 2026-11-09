using System.Linq;
using UnityEngine;

public class SelectButtonBuilderAdapter : MonoBehaviour
{
    [SerializeField] private CellSelectButtonUIAdapter buttonPrefab;
    [SerializeField] private SubMenuUIBuilderAdapter subMenuUIBuilder;
    [SerializeField] private CellDatabaseDefinition cellDatabase;
    [SerializeField] private CellPlacerAdapter placer;
    [SerializeField] private Color defaultBackgroundColor = Color.white;
    [SerializeField] private Color openedSubMenuColor = Color.white;
    [SerializeField] private CellTypeDomain[] subMenuInfos;

    private Font _font;
    public static string PlacementLabel(CellTypeDomain type) => type switch
    {
        CellTypeDomain.Empty => "撤去", CellTypeDomain.ExtractorWood => "木材採取",
        CellTypeDomain.ExtractorStone => "石材採取", CellTypeDomain.Storage => "保管庫",
        CellTypeDomain.Crafter => "加工設備", CellTypeDomain.Conveyor => "搬送路",
        CellTypeDomain.ExportConveyor => "搬出路", CellTypeDomain.Crossing => "交差搬送路", _ => type.ToString()
    };
    public void Configure(Font font)
    {
        _font = font;
        var hud = GetComponentInParent<HudLayoutAdapter>();
        if (hud != null) { defaultBackgroundColor = hud.IdleTabColor; openedSubMenuColor = hud.SelectedTabColor; }
        subMenuUIBuilder.SetDatabase(buttonPrefab, placer, cellDatabase, _font);
    }

    private void Awake()
    {
        var hud = GetComponentInParent<HudLayoutAdapter>();
        _font = hud != null ? hud.Font : null;
        if (hud != null) { defaultBackgroundColor = hud.IdleTabColor; openedSubMenuColor = hud.SelectedTabColor; }
    }

    private void Start()
    {
        if (placer == null)
        {
            placer = FindAnyObjectByType<CellPlacerAdapter>();
        }

        if (subMenuUIBuilder != null)
        {
            subMenuUIBuilder.SetDatabase(buttonPrefab, placer, cellDatabase, _font);
        }
        GenerateButtons();
    }

    private void LateUpdate()
    {
        if (_font == null) return;
        var grid = GetComponent<UnityEngine.UI.GridLayoutGroup>();
        if (grid != null && grid.constraint == UnityEngine.UI.GridLayoutGroup.Constraint.FixedColumnCount)
        {
            var columns = Mathf.Max(1, grid.constraintCount);
            var width = ((RectTransform)transform).rect.width;
            var hud = GetComponentInParent<HudLayoutAdapter>();
            if (hud != null)
            {
                // Grid自身の最小幅を次の計算へ戻すと、狭めた枠より一覧が広いままになる。
                // 表示枠から祖先の内側余白を引き、実際に使える幅を基準にする。
                width = hud.Body.viewport.rect.width;
                for (var parent = transform.parent; parent != null && parent != hud.Body.viewport; parent = parent.parent)
                {
                    var layout = parent.GetComponent<UnityEngine.UI.LayoutGroup>();
                    if (layout != null) width -= layout.padding.horizontal;
                }
            }
            grid.cellSize = new Vector2(Mathf.Max(1, (width - grid.padding.horizontal - grid.spacing.x * (columns - 1)) / columns), grid.cellSize.y);
        }
    }

    private void GenerateButtons()
    {
        var list = cellDatabase.GetAllCellInfos();

        foreach (var cellInfo in list)
        {
            var type = cellInfo.CellTypeDomain;
            if (type == CellTypeDomain.None || type == CellTypeDomain.ResourceWood ||
                type == CellTypeDomain.ResourceStone || type == CellTypeDomain.ResourceIron || type == CellTypeDomain.Singularity) continue;
            if (_font != null && type == CellTypeDomain.ExtractorStone) continue;

            // 割り当てる情報のみを下層のラッパクラスに受け渡す
            // アイコンの設定は未定
            var buttonParam = Instantiate(buttonPrefab, transform);

            if (_font != null) buttonParam.ApplyStyle(_font);

            // 対応するサブメニューを探す
            if (subMenuUIBuilder != null && subMenuInfos != null && subMenuInfos.Any(subMenuType => subMenuType == type))
            {
                buttonParam.SubMenuActive = true;
                buttonParam.SetColor(defaultBackgroundColor);
                var onToggle = subMenuUIBuilder.CreateSubMenuSystem(type);
                onToggle += isActive => buttonParam.SubMenuActive = !isActive;
                onToggle += isActive => buttonParam.SetColor(isActive ? openedSubMenuColor : defaultBackgroundColor);
                buttonParam.name = $"{type}SubMenuButton";
                buttonParam.Set(type == CellTypeDomain.ExtractorWood ? "採取設備 ▾" : PlacementLabel(type), () => onToggle?.Invoke(buttonParam.SubMenuActive));
            }
            else
            {
                buttonParam.Set(PlacementLabel(type), () => placer.SetSelectedCellType(type));
                buttonParam.name = $"{type}Button";
                buttonParam.SetColor(defaultBackgroundColor);
            }
        }
    }
}

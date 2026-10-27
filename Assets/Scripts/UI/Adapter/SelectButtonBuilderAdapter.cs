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
    private bool _started;
    public static string PlacementLabel(CellTypeDomain type) => type switch
    {
        CellTypeDomain.Empty => "撤去", CellTypeDomain.ExtractorWood => "木材採取",
        CellTypeDomain.ExtractorStone => "石材採取", CellTypeDomain.Storage => "保管庫",
        CellTypeDomain.Crafter => "加工設備", CellTypeDomain.Conveyor => "搬送路",
        CellTypeDomain.ExportConveyor => "搬出路", CellTypeDomain.Crossing => "交差搬送路", _ => type.ToString()
    };
    public void AttachTo(Transform container, Font font)
    {
        var oldTab = transform.parent;
        transform.SetParent(container, false);
        if (oldTab != null) oldTab.gameObject.SetActive(false);
        gameObject.SetActive(true);
        _font = font;
        defaultBackgroundColor = new Color(0.18f, 0.28f, 0.35f);
        openedSubMenuColor = new Color(0.24f, 0.43f, 0.53f);
        subMenuInfos = new[] { CellTypeDomain.ExtractorWood };
        subMenuUIBuilder = GetComponent<SubMenuUIBuilderAdapter>() ?? gameObject.AddComponent<SubMenuUIBuilderAdapter>();
        if (_started)
        {
            foreach (Transform child in transform) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
            subMenuUIBuilder.SetDatabase(buttonPrefab, placer, cellDatabase, _font);
            GenerateButtons();
        }
        var grid = GetComponent<UnityEngine.UI.GridLayoutGroup>();
        if (grid != null)
        {
            grid.cellSize = new Vector2(174, 40);
            grid.spacing = new Vector2(8, 8);
            grid.constraint = UnityEngine.UI.GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 2;
            grid.padding = new RectOffset();
        }
        var image = GetComponent<UnityEngine.UI.Image>();
        if (image != null) image.enabled = false;
        var size = GetComponent<UnityEngine.UI.ContentSizeFitter>() ?? gameObject.AddComponent<UnityEngine.UI.ContentSizeFitter>();
        size.verticalFit = UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;
        foreach (var button in GetComponentsInChildren<CellSelectButtonUIAdapter>(true)) button.ApplyStyle(font);
    }

    private void Start()
    {
        _started = true;
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
        if (grid != null) grid.cellSize = new Vector2(Mathf.Max(1, (((RectTransform)transform).rect.width - grid.spacing.x) / 2), 40);
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
                buttonParam.Set(null, type == CellTypeDomain.ExtractorWood ? "採取設備 ▾" : PlacementLabel(type), () => onToggle?.Invoke(buttonParam.SubMenuActive));
            }
            else
            {
                buttonParam.Set(null, PlacementLabel(type), () => placer.SetSelectedCellType(type));
                buttonParam.name = $"{type}Button";
                buttonParam.SetColor(defaultBackgroundColor);
            }
        }
    }
}
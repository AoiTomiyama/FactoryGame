using System.Linq;
using UnityEngine;

public class SelectButtonBuilder : MonoBehaviour
{
    [SerializeField] private CellSelectButtonUI buttonPrefab;
    [SerializeField] private SubMenuUIBuilder subMenuUIBuilder;
    [SerializeField] private CellDatabaseSO cellDatabase;
    [SerializeField] private CellPlacer placer;
    [SerializeField] private Color defaultBackgroundColor = Color.white;
    [SerializeField] private Color openedSubMenuColor = Color.white;
    [SerializeField] private CellType[] subMenuInfos;

    private void Start()
    {
        if (placer == null)
        {
            placer = FindAnyObjectByType<CellPlacer>();
        }

        if (subMenuUIBuilder != null)
        {
            subMenuUIBuilder.SetDatabase(buttonPrefab, placer);
        }
        GenerateButtons();
    }

    private void GenerateButtons()
    {
        var list = cellDatabase.GetAllCellInfos();

        foreach (var cellInfo in list)
        {
            var type = cellInfo.CellType;
            if (type == CellType.None) continue; // Noneはスキップ

            // 割り当てる情報のみを下層のラッパクラスに受け渡す
            // アイコンの設定は未定
            var buttonParam = Instantiate(buttonPrefab, transform);

            // 対応するサブメニューを探す
            if (subMenuInfos.Any(subMenuType => subMenuType == type))
            {
                buttonParam.SubMenuActive = true;
                buttonParam.SetColor(defaultBackgroundColor);
                var onToggle = subMenuUIBuilder.CreateSubMenuSystem(type);
                onToggle += isActive => buttonParam.SubMenuActive = !isActive;
                onToggle += isActive => buttonParam.SetColor(isActive ? openedSubMenuColor : defaultBackgroundColor);
                buttonParam.name = $"{type}SubMenuButton";
                buttonParam.Set(null, $"{type}", () => onToggle?.Invoke(buttonParam.SubMenuActive));
            }
            else
            {
                buttonParam.Set(null, cellInfo.CellName, () => placer.SetSelectedCellType(type));
                buttonParam.name = $"{type}Button";
                buttonParam.SetColor(defaultBackgroundColor);
            }
        }
    }
}
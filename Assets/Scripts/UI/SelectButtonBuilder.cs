using System;
using System.Linq;
using UnityEngine;

public class SelectButtonBuilder : MonoBehaviour
{
    [SerializeField] private CellSelectButtonUI buttonPrefab;
    [SerializeField] private CellDatabaseSO cellDatabase;
    [SerializeField] private PlayerCursorBehaviour playerCursor;
    [SerializeField] private PipeColorMapping pipeColorMapping;
    [SerializeField] private Color defaultBackgroundColor = Color.white;
    [SerializeField] private Color openedSubMenuColor = Color.white;
    [SerializeField] private Color subMenuColor = Color.white;
    [SerializeField] private CellType[] subMenuInfos;

    private void Start()
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
                var onToggle = CreateSubMenuSystem(type);
                onToggle += isActive => buttonParam.SubMenuActive = !isActive;
                onToggle += isActive => buttonParam.SetColor(isActive ? openedSubMenuColor : defaultBackgroundColor);
                buttonParam.name = $"{type}SubMenuButton";
                buttonParam.Set(null, $"{type}", () => onToggle?.Invoke(buttonParam.SubMenuActive));
            }
            else
            {
                buttonParam.Set(null, cellInfo.CellName, () => playerCursor.SetSelectedCellType(type));
                buttonParam.name = $"{type}Button";
                buttonParam.SetColor(defaultBackgroundColor);
            }
        }
    }

    private Action<bool> CreateSubMenuSystem(CellType type)
    {
        if (type != CellType.ItemPipe) return null;

        // ItemPipeの場合、パイプの色を選択するサブメニューを作成
        var colors = (PipeColorEnum[])Enum.GetValues(typeof(PipeColorEnum));
        Action<bool> onToggle = null;
        foreach (var color in colors)
        {
            var subButton = Instantiate(buttonPrefab, transform);
            subButton.SetColor(subMenuColor);
            subButton.Set(null, $"{type}-{color}", () =>
            {
                SubMenuButtonClick(type, color);
            });
            onToggle += isActive => subButton.gameObject.SetActive(isActive);
            subButton.gameObject.SetActive(false);
        }
        return onToggle;
    }
    
    private void SubMenuButtonClick(CellType type, PipeColorEnum color)
    {
        playerCursor.SetSelectedCellType(type);
        if (color == PipeColorEnum.Default) return;
        playerCursor.UpdateCellData((cellBase, placeholder) =>
        {
            if (cellBase is not ItemPipeCell itemPipeCell)
            {
                return (cellBase, placeholder);
            }

            itemPipeCell.SetPipeColor(color);
            foreach (var placeholderRenderer in placeholder.GetComponentsInChildren<Renderer>())
            {
                placeholderRenderer.material = pipeColorMapping.GetPipeMaterial(color);
            }

            return (itemPipeCell, placeholder);
        });
    }
}
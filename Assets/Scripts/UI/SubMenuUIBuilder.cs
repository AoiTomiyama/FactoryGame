using System;
using UnityEngine;

public class SubMenuUIBuilder : MonoBehaviour
{
    [SerializeField] private CellSelectButtonUI buttonPrefab;
    [SerializeField] private PipeColorMapping pipeColorMapping;
    [SerializeField] private CellPlacer placer;
    [SerializeField] private Color subMenuColor = Color.white;

    public Action<bool> CreateSubMenuSystem(CellType type)
    {
        Action<bool> onToggle = null;
        switch (type)
        {
            case CellType.ItemPipe:
                // ItemPipeの場合、パイプの色を選択するサブメニューを作成
                var colors = (PipeColorEnum[])Enum.GetValues(typeof(PipeColorEnum));
                foreach (var color in colors)
                {
                    var subButton = Instantiate(buttonPrefab, transform);
                    subButton.SetColor(subMenuColor);
                    subButton.Set(null, $"{color}", () =>
                    {
                        placer.SetSelectedCellType(type);
                        if (color == PipeColorEnum.Default) return;
                        placer.UpdateCellData((cellBase, placeholder) =>
                        {
                            if (cellBase is not ItemPipeCell itemPipeCell)
                            {
                                return (cellBase, placeholder);
                            }

                            itemPipeCell.SetPipeColor(color);
                            if (placeholder.TryGetComponent<PlaceholderCell>(out var placeholderCell))
                            {
                                var material = pipeColorMapping.GetPipeMaterial(color);
                                placeholderCell.SetMaterial(material);
                            }


                            return (itemPipeCell, placeholder);
                        });
                    });
                    onToggle += isActive => subButton.gameObject.SetActive(isActive);
                    subButton.gameObject.SetActive(false);
                }

                break;
            // 他のCellTypeに対するサブメニューが必要な場合はここに追加
            default:
                Debug.LogWarning($"サブメニューが未実装のCellType: {type}");
                break;
        }

        return onToggle;
    }
}
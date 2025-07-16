using System;
using UnityEngine;

public class CellSelectSubMenuUI : MonoBehaviour
{
    [SerializeField] private CellSelectButtonUI button;
    [SerializeField] private PipeColorMapping pipeColorMapping;

    public void Set(CellType type, PlayerCursorBehaviour cursor)
    {
        if (type != CellType.ItemPipe) return;
        var colors = (PipeColorEnum[])Enum.GetValues(typeof(PipeColorEnum));
        foreach (var color in colors)
        {
            var buttonInstance = Instantiate(button, transform, false);
            buttonInstance.Set(null, $"{type}-{color}", () =>
            {
                cursor.SetSelectedCellType(type);
                cursor.UpdateCellData((cellBase, placeholder) =>
                {
                    if (color == PipeColorEnum.Default ||
                        cellBase is not ItemPipeCell itemPipeCell)
                    {
                        return (cellBase, placeholder);
                    }

                    itemPipeCell.SetPipeColor(color);
                    foreach (var placeholderRenderer in placeholder.GetComponentsInChildren<Renderer>())
                    {
                        placeholderRenderer.material = pipeColorMapping.GetPipeColor(color);
                    }

                    return (itemPipeCell, placeholder);
                });
            });
        }
    }
}
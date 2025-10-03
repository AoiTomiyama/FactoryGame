using System;
using UnityEngine;

public class SubMenuUIBuilder : MonoBehaviour
{
    [SerializeField] private Color subMenuColor = Color.white;

    private CellSelectButtonUI _buttonPrefab;
    private CellPlacer _cellPlacer;

    public void SetDatabase(CellSelectButtonUI button, CellPlacer placer)
    {
        _buttonPrefab = button;
        _cellPlacer = placer;
    }

    public Action<bool> CreateSubMenuSystem(CellType type)
    {
        Action<bool> onToggle = null;
        switch (type)
        {
            // TODO: サブメニューの実装
            // case CellType.ItemPipe:
            //     // ItemPipeの場合、パイプの色を選択するサブメニューを作成
            //     var colors = (PipeColorEnum[])Enum.GetValues(typeof(PipeColorEnum));
            //     foreach (var color in colors)
            //     {
            //         var subButton = Instantiate(_buttonPrefab, transform);
            //         subButton.SetColor(subMenuColor);
            //         subButton.Set(null, $"{color}", () =>
            //         {
            //             _cellPlacer.SetSelectedCellType(type);
            //             if (color == PipeColorEnum.Default) return;
            //             _cellPlacer.UpdateCellData((cellBase, placeholder) => (cellBase, placeholder));
            //         });
            //         onToggle += isActive => subButton.gameObject.SetActive(isActive);
            //         subButton.gameObject.SetActive(false);
            //     }
            //
            //     break;
            // 他のCellTypeに対するサブメニューが必要な場合はここに追加
            default:
                Debug.LogWarning($"サブメニューが未実装のCellType: {type}");
                break;
        }

        return onToggle;
    }
}
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class UIRaycasterAdapter : MonoBehaviour
{
    public bool IsPointerOverUI(Vector2 mousePosition)
    {
        var eventData = new PointerEventData(EventSystem.current)
        {
            position = mousePosition
        };
        var results = new List<RaycastResult>();
        // 実行時に追加した経済パネルも含め、全Canvasの入力をフィールドから遮る。
        EventSystem.current.RaycastAll(eventData, results);
        return results.Count > 0;
    }
}

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class UIRaycaster : MonoBehaviour
{
    private GraphicRaycaster _graphicRaycaster;

    private void Start()
    {
        _graphicRaycaster = GetComponent<GraphicRaycaster>();
    }

    public bool IsPointerOverUI(Vector2 mousePosition)
    {
        var eventData = new PointerEventData(EventSystem.current)
        {
            position = mousePosition
        };
        var results = new List<RaycastResult>();
        _graphicRaycaster.Raycast(eventData, results);
        return results.Count > 0;
    }
}

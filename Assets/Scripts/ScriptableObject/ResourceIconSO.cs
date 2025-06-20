using UnityEngine;

[CreateAssetMenu(fileName = "ResourceIconSO", menuName = "Scriptable Objects/ResourceIconSO")]
public class ResourceIconSO : ScriptableObject
{
    [System.Serializable]
    private struct ResourceIcon
    {
        public ResourceType resourceType;
        public Sprite icon;
    }
    [SerializeField] private ResourceIcon[] resourceIcons;
    public Sprite GetIcon(ResourceType resourceType)
    {
        foreach (var resourceIcon in resourceIcons)
        {
            if (resourceIcon.resourceType == resourceType)
            {
                return resourceIcon.icon;
            }
        }
        Debug.LogWarning($"Resource icon not found for type: {resourceType}", this);
        return null; // or a default icon
    }
}

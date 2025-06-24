using UnityEngine;

[CreateAssetMenu(fileName = "ResourceIconSO", menuName = "Scriptable Objects/ResourceIconSO")]
public class ResourceSO : ScriptableObject
{
    [System.Serializable]
    private struct ResourceIcon
    {
        public ResourceType resourceType;
        public GameObject prefab;
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
    public GameObject GetPrefab(ResourceType resourceType)
    {
        foreach (var resourceIcon in resourceIcons)
        {
            if (resourceIcon.resourceType == resourceType)
            {
                return resourceIcon.prefab;
            }
        }
        Debug.LogWarning($"Resource prefab not found for type: {resourceType}", this);
        return null; // or a default prefab
    }
}

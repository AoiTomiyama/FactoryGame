using System.Linq;
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
    [SerializeField] private ResourceIcon[] resourceInfos;
    
    /// <summary>
    /// リソースタイプに応じたSpriteを取得する。
    /// </summary>
    /// <param name="resourceType">指定のタイプ</param>
    /// <returns>一致したSprite</returns>
    public Sprite GetIcon(ResourceType resourceType) => 
        resourceInfos.FirstOrDefault(info => info.resourceType == resourceType).icon;

    /// <summary>
    /// リソースタイプに応じたPrefabを取得する。
    /// </summary>
    /// <param name="resourceType">指定のタイプ</param>
    /// <returns>一致したPrefab</returns>
    public GameObject GetPrefab(ResourceType resourceType) => 
        resourceInfos.FirstOrDefault(info => info.resourceType == resourceType).prefab;
}

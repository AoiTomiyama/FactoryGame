using System;
using System.Linq;
using UnityEngine;

[CreateAssetMenu(fileName = "ResourceIconSO", menuName = "Scriptable Objects/ResourceIconSO")]
public class ResourceSO : ScriptableObject
{
    [Serializable]
    public struct ResourceInfo
    {
        public ResourceType resourceType;
        public GameObject prefab;
        public Sprite icon;
    }

    [SerializeField] private ResourceInfo[] resourceInfos;

    /// <summary>
    /// リソースタイプに応じたSpriteを取得する。
    /// </summary>
    /// <param name="resourceType">指定のタイプ</param>
    /// <returns>一致したSprite</returns>
    public Sprite GetIcon(ResourceType resourceType) =>
        resourceInfos.FirstOrDefault(info => info.resourceType == resourceType).icon;

    public ResourceInfo[] GetAllInfos() => (ResourceInfo[])resourceInfos.Clone();
}
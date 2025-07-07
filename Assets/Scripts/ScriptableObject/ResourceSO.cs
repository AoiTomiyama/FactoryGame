using System;
using System.Linq;
using UnityEngine;

[CreateAssetMenu(fileName = "ResourceSO", menuName = "Scriptable Objects/ResourceIconSO")]
public class ResourceSO : ScriptableObject
{
    [Serializable]
    public struct ResourceInfo
    {
        [SerializeField] string name;
        [SerializeField] private ResourceType resourceType;
        [SerializeField] private GameObject prefab;
        [SerializeField] private Sprite icon;

        public string Name => name;

        public ResourceType ResourceType => resourceType;

        public GameObject Prefab => prefab;

        public Sprite Icon => icon;
    }

    [SerializeField] private ResourceInfo[] resourceInfos;

    /// <summary>
    /// データベース内から指定されたリソースタイプの情報を取得します。
    /// </summary>
    /// <param name="resourceType">指定されたリソースタイプ</param>
    public ResourceInfo GetInfo(ResourceType resourceType) =>
        resourceInfos.FirstOrDefault(info => info.ResourceType == resourceType);

    /// <summary>
    /// データベースの全てのリソース情報を取得します。
    /// </summary>
    public ResourceInfo[] GetAllInfos() => (ResourceInfo[])resourceInfos.Clone();
}
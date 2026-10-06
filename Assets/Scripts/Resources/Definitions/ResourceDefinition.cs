using System;
using System.Linq;
using UnityEngine;

[CreateAssetMenu(fileName = "ResourceDefinition", menuName = "Scriptable Objects/ResourceIconSO")]
public class ResourceDefinition : ScriptableObject
{
    [Serializable]
    public struct ResourceInfo
    {
        [SerializeField] string name;
        [SerializeField] private ResourceTypeDomain resourceType;
        [SerializeField] private GameObject prefab;
        [SerializeField] private Sprite icon;

        public string Name => name;

        public ResourceTypeDomain ResourceTypeDomain => resourceType;

        public GameObject Prefab => prefab;

        public Sprite Icon => icon;
    }

    [SerializeField] private ResourceInfo[] resourceInfos;

    /// <summary>
    /// データベース内から指定されたリソースタイプの情報を取得します。
    /// </summary>
    /// <param name="resourceType">指定されたリソースタイプ</param>
    public ResourceInfo GetInfo(ResourceTypeDomain resourceType) =>
        resourceInfos.FirstOrDefault(info => info.ResourceTypeDomain == resourceType);

    /// <summary>
    /// データベースの全てのリソース情報を取得します。
    /// </summary>
    public ResourceInfo[] GetAllInfos() => (ResourceInfo[])resourceInfos.Clone();
}
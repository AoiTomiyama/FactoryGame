using System;
using System.Linq;
using UnityEngine;

[CreateAssetMenu(fileName = "ResourceDefinition", menuName = "Scriptable Objects/ResourceIconSO")]
public class ResourceDefinition : ScriptableObject
{
    [Serializable]
    public struct Resource
    {
        [SerializeField] private string resourceName;
        [SerializeField] private ResourceTypeDomain resourceType;
        [SerializeField] private GameObject prefab;
        [SerializeField] private Sprite icon;

        public string ResourceName => resourceName;

        public ResourceTypeDomain ResourceTypeDomain => resourceType;

        public GameObject Prefab => prefab;

        public Sprite Icon => icon;
    }

    [SerializeField]
    [Tooltip("リソースの情報を登録する配列")]
    private Resource[] initializedResourceArr;

    /// <summary>
    /// データベース内から指定されたリソースタイプの情報を取得します。
    /// </summary>
    /// <param name="resourceType">指定されたリソースタイプ</param>
    public Resource GetResourceByType(ResourceTypeDomain resourceType) =>
        initializedResourceArr.FirstOrDefault(info => info.ResourceTypeDomain == resourceType);

    /// <summary>
    /// データベースの全てのリソース情報を取得します。
    /// </summary>
    public Resource[] GetAllResources() => (Resource[])initializedResourceArr.Clone();
}
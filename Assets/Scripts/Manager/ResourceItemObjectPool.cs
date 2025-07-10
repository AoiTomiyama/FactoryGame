using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

public sealed class ResourceItemObjectPool : SingletonMonoBehaviour<ResourceItemObjectPool>
{
    [SerializeField] private ResourceSO resourceDatabase;
    [SerializeField] private int defaultPoolCapacity = 100;
    [SerializeField] private int maxPoolCapacity = 500;

    private Dictionary<ResourceType, ObjectPool<GameObject>> _pool;
    private bool _isInitialized;

    private void Start()
    {
        InitializePool();
    }

    private void OnDestroy()
    {
        ClearPool();
    }

    private void InitializePool()
    {
        if (_isInitialized) return;
        _pool = new();

        if (resourceDatabase == null)
        {
#if UNITY_EDITOR
            Debug.LogError("resourceDatabaseが設定されていません。");
#endif
            return;
        }

        var infos = resourceDatabase.GetAllInfos();
        if (infos == null)
        {
#if UNITY_EDITOR
            Debug.LogError("resourceDatabase.GetAllInfos()がnullを返しました。");
#endif
            return;
        }

        foreach (var info in infos)
        {
            var prefab = info.Prefab;
            var type = info.ResourceType;
            if (prefab == null)
            {
#if UNITY_EDITOR
                Debug.LogWarning($"{type} のPrefabがnullです。");
#endif
                continue;
            }

            if (_pool.ContainsKey(type))
            {
#if UNITY_EDITOR
                Debug.LogWarning($"{type} は既にプールに登録されています。");
#endif
                continue;
            }

            _pool[type] = new(
                createFunc: () => Instantiate(prefab, transform),
                actionOnGet: obj => obj.SetActive(true),
                actionOnRelease: obj => obj.SetActive(false),
                actionOnDestroy: Destroy,
                collectionCheck: true,
                defaultCapacity: defaultPoolCapacity,
                maxSize: maxPoolCapacity
            );
        }

        _isInitialized = true;
    }

    private void ClearPool()
    {
        if (_pool == null) return;
        foreach (var pool in _pool.Values)
        {
            pool.Clear();
        }

        _pool.Clear();
        _isInitialized = false;
    }

    public GameObject GetPrefab(ResourceType resourceType)
    {
        if (!_isInitialized) InitializePool();
        if (_pool != null && _pool.ContainsKey(resourceType)) return _pool[resourceType].Get();

#if UNITY_EDITOR
        Debug.LogError($"{resourceType} のプールが存在しません。");
#endif
        return null;
    }

    public void Return(ResourceType type, GameObject obj)
    {
        if (!_isInitialized) InitializePool();
        if (_pool == null || !_pool.ContainsKey(type))
        {
#if UNITY_EDITOR
            Debug.LogError($"{type} のプールが存在しません。");
#endif
            Destroy(obj);
            return;
        }

        // 既にプールに戻されている場合は無視
        if (!obj.activeInHierarchy)
        {
#if UNITY_EDITOR
            Debug.LogWarning("このオブジェクトは既にプールに戻されています。");
#endif
            return;
        }

        _pool[type].Release(obj);
    }
}
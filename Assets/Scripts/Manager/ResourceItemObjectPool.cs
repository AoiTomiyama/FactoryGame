using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.Pool;

public sealed class ResourceItemObjectPool : SingletonMonoBehaviour<ResourceItemObjectPool>
{
    [SerializeField] private ResourceSO resourceDatabase;
    [SerializeField] private int defaultPoolCapacity = 100;
    [SerializeField] private int maxPoolCapacity = 500;
    [SerializeField] private float transferSecond = 0.2f;

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
            if (type == ResourceType.None) continue;
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

    private GameObject GetPrefab(ResourceType resourceType)
    {
        if (!_isInitialized) InitializePool();
        if (_pool != null && _pool.ContainsKey(resourceType)) return _pool[resourceType].Get();

#if UNITY_EDITOR
        Debug.LogError($"{resourceType} のプールが存在しません。");
#endif
        return null;
    }

    private void Return(ResourceType type, GameObject obj)
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

    private readonly Dictionary<int, RentedObjectInfo> _rentedObjects = new();

    private struct RentedObjectInfo
    {
        public readonly GameObject Prefab;
        public readonly ResourceType Type;
        public readonly int Amount;

        public RentedObjectInfo(GameObject prefab, ResourceType type, int amount)
        {
            Prefab = prefab;
            Type = type;
            Amount = amount;
        }
    }

    /// <summary>
    /// IDで指定されたオブジェクトを、fromからtoにかけて線形アニメーションさせる。
    /// </summary>
    /// <param name="token">キャンセル用のトークン</param>
    /// <param name="from">アニメーションの始点</param>
    /// <param name="to">アニメーションの終点</param>
    /// <param name="id">アニメーションの対象</param>
    public async UniTask Transfer(CancellationToken token, Vector3 from, Vector3 to, int id)
    {
        if (!_rentedObjects.TryGetValue(id, out var info))
        {
#if UNITY_EDITOR
            Debug.LogError($"ID {id} の保存済みリソースは存在しません。");
#endif
            return;
        }

        info.Prefab.transform.position = from;
        var tween = info.Prefab.transform
            .DOMove(to, transferSecond)
            .SetEase(Ease.Linear);

        await tween.ToUniTask(cancellationToken: token);
    }

    /// <summary>
    /// リソースを保存し、対応したIDを返します。
    /// </summary>
    /// <param name="type">リソースタイプ</param>
    /// <param name="amount">リソースの量</param>
    /// <returns>再度受け取るためのキーとなるID</returns>
    public int CreateId(ResourceType type, int amount)
    {
        var prefab = GetPrefab(type);
        var id = prefab.GetInstanceID();

        var textMesh = prefab.GetComponentInChildren<TextMeshPro>();
        if (textMesh != null)
        {
            // Textが存在する場合、予約量を表示
            textMesh.text = amount.ToString();
        }

        _rentedObjects[id] = new(prefab, type, amount);
        return id;
    }

    /// <summary>
    /// 指定したIDのリソース情報を取得します。
    /// </summary> 
    /// <param name="id"> 取得するリソースのID </param>
    ///  <returns> ResourceType型のリソースタイプと、int型のリソース量のタプル。 </returns>
    public (ResourceType type, int amount) TakeById(int id)
    {
        if (_rentedObjects.TryGetValue(id, out var info))
        {
            return (info.Type, info.Amount);
        }

#if UNITY_EDITOR
        Debug.LogError($"ID {id} の保存済みリソースは存在しません。");
#endif
        return (ResourceType.None, 0);
    }

    /// <summary>
    /// 指定したIDのリソースをプールへ戻し、ID情報を削除します。
    /// </summary>
    /// <param name="id"> 破棄するリソースのID </param>
    public void DisposeId(int id)
    {
        if (!_rentedObjects.TryGetValue(id, out var info))
        {
#if UNITY_EDITOR
            Debug.LogError($"ID {id} の保存済みリソースは存在しません。");
#endif
            return;
        }

        Return(info.Type, info.Prefab);
        _rentedObjects.Remove(id);
    }
}
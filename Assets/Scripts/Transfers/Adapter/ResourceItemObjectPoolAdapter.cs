using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.Pool;

public sealed class ResourceItemObjectPoolAdapter : SingletonMonoBehaviourAdapter<ResourceItemObjectPoolAdapter>, IResourceTransferPresentationApplication
{
    public ResourceTransitStoreApplication Resources { get; } = new();
    [SerializeField] private ResourceDefinition resourceDatabase;
    [SerializeField] private int defaultPoolCapacity = 100;
    [SerializeField] private int maxPoolCapacity = 500;
    [SerializeField] private float transferSecond = 0.2f;

    private Dictionary<ResourceTypeDomain, ObjectPool<GameObject>> _pool;
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

        var resources = resourceDatabase.GetAllResources();
        if (resources == null)
        {
#if UNITY_EDITOR
            Debug.LogError("resourceDatabaseにはリソース情報が登録されていません。");
#endif
            return;
        }

        foreach (var resource in resources)
        {
            var prefab = resource.Prefab;
            var type = resource.ResourceTypeDomain;
            if (type == ResourceTypeDomain.None) continue;
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
        foreach (var id in new List<int>(_rentedObjects.Keys)) ReleaseVisual(id);
        Resources.Clear();
        if (_pool == null) return;
        foreach (var pool in _pool.Values)
        {
            pool.Clear();
        }

        _pool.Clear();
        _isInitialized = false;
    }

    private GameObject GetPrefab(ResourceTypeDomain resourceType)
    {
        if (!_isInitialized) InitializePool();
        if (_pool != null && _pool.ContainsKey(resourceType)) return _pool[resourceType].Get();

#if UNITY_EDITOR
        Debug.LogError($"{resourceType} のプールが存在しません。");
#endif
        return null;
    }

    private void Return(ResourceTypeDomain type, GameObject obj)
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
        if (!obj.activeSelf)
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
        public readonly ResourceTypeDomain Type;
        public readonly ResourceItemAnimationAdapter Animation;

        public RentedObjectInfo(GameObject prefab, ResourceTypeDomain type)
        {
            Prefab = prefab;
            Type = type;
            Animation = new ResourceItemAnimationAdapter();
        }
    }

    /// <summary>
    /// IDで指定されたオブジェクトを、fromからtoにかけて線形アニメーションさせる。
    /// </summary>
    /// <param name="token">キャンセル用のトークン</param>
    /// <param name="from">アニメーションの始点</param>
    /// <param name="to">アニメーションの終点</param>
    /// <param name="id">アニメーションの対象</param>
    public UniTask<ResourceAnimationResultApplication> Transfer(CancellationToken token, Vector3 from, Vector3 to, int id)
    {
        if (token.IsCancellationRequested) return UniTask.FromResult(ResourceAnimationResultApplication.Cancelled);
        if (!_rentedObjects.TryGetValue(id, out var rentedObject))
            return UniTask.FromResult(ResourceAnimationResultApplication.MissingVisual);
        return rentedObject.Animation.MoveAsync(rentedObject.Prefab, token, from, to, transferSecond);
    }

    public void SetPosition(int id, Vector3 position)
    {
        if (_rentedObjects.TryGetValue(id, out var rentedObject) && rentedObject.Prefab != null)
            rentedObject.Prefab.transform.position = position;
    }

    /// <summary>
    /// リソースを保存し、対応したIDを返します。
    /// </summary>
    /// <param name="type">リソースタイプ</param>
    /// <param name="amount">リソースの量</param>
    /// <returns>再度受け取るためのキーとなるID</returns>
    public int CreateIdFromResourceData(ResourceTypeDomain type, int amount)
    {
        var id = Resources.Create(type, amount);
        // 表示が用意できなくても搬出済みの資源データは保持し、演出結果で失敗を伝える。
        TryRestoreVisual(id);
        return id;
    }

    /// <summary>保持中の資源データから表示を再作成する。資源IDと数量は維持する。</summary>
    public bool TryRestoreVisual(int id)
    {
        if (!Resources.TryGet(id, out var data)) return false;
        if (_rentedObjects.TryGetValue(id, out var existing) && existing.Prefab != null) return true;
        ReleaseVisual(id);
        var prefab = GetPrefab(data.Type);
        if (prefab == null) return false;

        var textMesh = prefab.GetComponentInChildren<TextMeshPro>();
        if (textMesh != null)
        {
            // Textが存在する場合、予約量を表示
            textMesh.text = data.Amount.ToString();
        }

        _rentedObjects[id] = new(prefab, data.Type);
        return true;
    }

    /// <summary>
    /// 指定したIDのリソース情報を取得します。
    /// </summary> 
    /// <param name="id"> 取得するリソースのID </param>
    ///  <returns> ResourceType型のリソースタイプと、int型のリソース量のタプル。 </returns>
    public (ResourceTypeDomain type, int amount) TakeResourceDataById(int id)
    {
        if (Resources.TryGet(id, out var info))
        {
            return (info.Type, info.Amount);
        }

#if UNITY_EDITOR
        Debug.LogError($"ID {id} の保存済みリソースは存在しません。");
#endif
        return (ResourceTypeDomain.None, 0);
    }

    /// <summary>
    /// 指定したIDのリソースをプールへ戻し、ID情報を削除します。
    /// </summary>
    /// <param name="id"> 破棄するリソースのID </param>
    public void DisposeId(int id)
    {
        // 資源所有者が搬送終了・削除を決定したときだけ、データも終了する。
        Resources.Remove(id);
        ReleaseVisual(id);
    }

    /// <summary>表示だけを一度返却する。搬送資源のデータと数量は変更しない。</summary>
    public bool ReleaseVisual(int id)
    {
        if (!_rentedObjects.TryGetValue(id, out var info))
            return false;

        // 取消の継続が即時に実行されても、この表示を二度返さないよう先に登録を外す。
        _rentedObjects.Remove(id);
        info.Animation.Cancel();
        if (info.Prefab != null) Return(info.Type, info.Prefab);
        return true;
    }
}

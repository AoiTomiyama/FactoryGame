using UnityEngine;

public abstract class SingletonMonoBehaviour<T> : MonoBehaviour where T : SingletonMonoBehaviour<T>
{
    private static T _instance;

    public static T Instance
    {
        get
        {
            if (_instance != null) return _instance;
            _instance = FindAnyObjectByType<T>();

            if (_instance != null) return _instance;
#if UNITY_EDITOR
            Debug.LogError($"{typeof(T)}がシーンに存在しません。");
#endif
            return null;
        }
    }

    protected virtual void Awake()
    {
        if (GetType() != typeof(T))
        {
            Debug.LogError($"Singletonの型が一致しません。期待される型: {typeof(T)}, 実際の型: {GetType()}");
            return;
        }
        if (_instance == null)
        {
            _instance = (T)this;
        }
        else
        {
            Debug.LogWarning($"{nameof(T)}のインスタンスが複数存在します。最初のインスタンスを保持します。");
            Destroy(gameObject);
        }
    }
}

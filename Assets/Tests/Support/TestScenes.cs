#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>検証のSceneだけを参照し、Editorで開いている作業中のSceneとSingletonを保持する。</summary>
public static class TestScenes
{
    public static Scene CurrentScene => _override.IsValid() ? _override : SceneManager.GetActiveScene();
    private static Scene _override;
    public static bool CaptureImages;
    public static GameObject NewObject(string name, params Type[] components)
    {
        if (!EditorSceneManager.IsPreviewScene(CurrentScene)) return new GameObject(name, components);
        var gameObject = UnityEditor.EditorUtility.CreateGameObjectWithHideFlags(name, HideFlags.HideAndDontSave, components);
        SceneManager.MoveGameObjectToScene(gameObject, CurrentScene); gameObject.hideFlags = HideFlags.None;
        return gameObject;
    }
    private static T MoveClone<T>(T item) where T : Object
    {
        var gameObject = item is GameObject root ? root : (item as Component)?.gameObject;
        if (gameObject != null && gameObject.scene != CurrentScene && EditorSceneManager.IsPreviewScene(CurrentScene))
            SceneManager.MoveGameObjectToScene(gameObject.transform.root.gameObject, CurrentScene);
        return item;
    }
    public static T Clone<T>(T item) where T : Object => MoveClone(Object.Instantiate(item));
    public static T Clone<T>(T item, Transform parent) where T : Object => MoveClone(Object.Instantiate(item, parent));
    public static T Clone<T>(T item, Transform parent, bool worldPositionStays) where T : Object => MoveClone(Object.Instantiate(item, parent, worldPositionStays));
    public static T Clone<T>(T item, Vector3 position, Quaternion rotation) where T : Object => MoveClone(Object.Instantiate(item, position, rotation));
    public static T Clone<T>(T item, Vector3 position, Quaternion rotation, Transform parent) where T : Object => MoveClone(Object.Instantiate(item, position, rotation, parent));
    public static T Find<T>(FindObjectsInactive inactive = FindObjectsInactive.Exclude) where T : Object => All<T>(inactive).FirstOrDefault();
    public static T[] All<T>(FindObjectsInactive inactive = FindObjectsInactive.Exclude) where T : Object
    {
        var scene = CurrentScene;
        if (EditorSceneManager.IsPreviewScene(scene))
            return scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Component>(inactive == FindObjectsInactive.Include))
                .OfType<T>().ToArray();
        return Object.FindObjectsByType<T>(inactive, FindObjectsSortMode.None).Where(item =>
            item is Component component && component.gameObject.scene == scene || item is GameObject gameObject && gameObject.scene == scene).ToArray();
    }
    public static void UseGrid(GridFieldDatabaseAdapter grid) => SetSingleton(typeof(GridFieldDatabaseAdapter), grid);
    private static FieldInfo Singleton(Type type) => typeof(SingletonMonoBehaviourAdapter<>).MakeGenericType(type).GetField("_instance", BindingFlags.NonPublic | BindingFlags.Static);
    private static void SetSingleton(Type type, object value) => Singleton(type).SetValue(null, value);

    public sealed class EditorScope : IDisposable
    {
        private readonly Scene _previous, _empty, _preview;
        private readonly Dictionary<Type, object> _singletons = new();
        public EditorScope(bool mainScene = false)
        {
            _previous = SceneManager.GetActiveScene();
            foreach (var type in new[] { typeof(GridFieldDatabaseAdapter), typeof(ResourceItemObjectPoolAdapter), typeof(CellStatusViewAdapter) })
            { _singletons[type] = Singleton(type).GetValue(null); SetSingleton(type, null); }
            _empty = EditorSceneManager.NewPreviewScene();
            if (mainScene) _preview = EditorSceneManager.OpenPreviewScene("Assets/Scenes/MainScene.unity");
            _override = _preview.IsValid() ? _preview : _empty;
        }
        public void Dispose()
        {
            _override = default;
            if (_preview.IsValid()) EditorSceneManager.ClosePreviewScene(_preview);
            if (_previous.IsValid() && _previous.isLoaded) SceneManager.SetActiveScene(_previous);
            if (_empty.IsValid() && _empty.isLoaded) EditorSceneManager.ClosePreviewScene(_empty);
            foreach (var pair in _singletons) SetSingleton(pair.Key, pair.Value);
            CaptureImages = false;
        }
    }
}
#endif

#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Rendering;

/// <summary>DevelopmentプレイヤーのCLI検証。通常起動には介入せず、実UIイベントと配置経路を通す。</summary>
public sealed class CellDefinitionVerifierAdapter : MonoBehaviour
{
    private const BindingFlags Fields = BindingFlags.Instance | BindingFlags.NonPublic;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void StartVerification()
    {
        if (!Environment.GetCommandLineArgs().Contains("--verify-cell-definitions")) return;
        new GameObject("CellDefinitionVerification").AddComponent<CellDefinitionVerifierAdapter>();
    }

    private void Start() => StartCoroutine(Guard(Verify()));

    private IEnumerator Guard(IEnumerator verification)
    {
        while (true)
        {
            object next;
            try
            {
                if (!verification.MoveNext()) break;
                next = verification.Current;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                Application.Quit(1);
                yield break;
            }
            yield return next;
        }
        Debug.Log("Cell definition PLAYER checks passed: cold serialized definitions, generated button, click selection, placeholder, pointer placement, rendered capture.");
        Application.Quit(0);
    }

    private IEnumerator Verify()
    {
        // 通常のMainScene.StartとUI生成を待つ。辞書を手動Validateして起動不具合を隠さない。
        yield return null;
        yield return null;
        var placer = FindAnyObjectByType<CellPlacerAdapter>();
        Require(placer != null, "scene placer exists");
        foreach (var database in (CellDatabaseDefinition[])Get(placer, "cellDatabaseArr"))
        {
            var expected = (CellInfoDefinition[])Get(database, "cellPairingInfos");
            Require(database.GetAllCellInfos().Count == expected.Select(info => info.CellTypeDomain).Distinct().Count(), "cold player definition count");
            foreach (var info in expected)
                Require(database.TryGetCellInfo(info.CellTypeDomain, out var actual) && actual.Equals(info), "cold player prefab references");
        }

        var button = FindObjectsByType<CellSelectButtonUIAdapter>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .FirstOrDefault(candidate => candidate.name == "StorageButton");
        Require(button != null, "actual generated StorageButton exists");
        ((Button)Get(button, "button")).onClick.Invoke();
        Require(Get(placer, "_cachedCell") is StorageCellAdapter, "real button callback selects Storage");
        Require((GameObject)Get(placer, "_placeholderCell") != null, "selection creates placeholder");

        var grid = GridFieldDatabaseAdapter.Instance;
        EmptyCellAdapter empty = null;
        for (var x = 0; grid.IsWithinBounds(x, 0) && empty == null; x++)
            for (var z = 0; grid.IsWithinBounds(x, z) && empty == null; z++)
                empty = grid.GetCell(x, z) as EmptyCellAdapter;
        Require(empty != null, "empty placement target exists");
        var targetX = empty.XIndex;
        var targetZ = empty.ZIndex;
        placer.SelectGrid(empty.gameObject);
        placer.PointerBegin();
        placer.PointerDrag();
        placer.PointerEnd();
        yield return null;
        Require(grid.GetCell(targetX, targetZ) is StorageCellAdapter placed && placed.gameObject.activeInHierarchy,
            "pointer placement creates active Storage in grid");
        Require(((CellBaseAdapter)Get(placer, "_selectedCell")) == grid.GetCell(targetX, targetZ), "selection follows placed cell");
        yield return null;
        var screenshot = Environment.GetCommandLineArgs().FirstOrDefault(arg => arg.StartsWith("--cell-verification-image="));
        Require(screenshot != null, "capture path supplied");
        CaptureRenderedScene(screenshot.Substring("--cell-verification-image=".Length));
        Debug.Log($"Cell definition PLAYER placed Storage at ({targetX}, {targetZ}); image={screenshot}");
    }

    private static void CaptureRenderedScene(string path)
    {
        // Hiddenウィンドウのswapchainは黒になることがあるため、同じシーンのCameraを明示描画する。
        var camera = Camera.main;
        Require(camera != null, "scene camera exists");
        var target = RenderTexture.GetTemporary(1280, 720, 24, RenderTextureFormat.ARGB32);
        var previous = RenderTexture.active;
        var texture = new Texture2D(1280, 720, TextureFormat.RGB24, false);
        try
        {
            var request = new RenderPipeline.StandardRequest { destination = target };
            Require(RenderPipeline.SupportsRenderRequest(camera, request), "pipeline supports offscreen scene capture");
            Canvas.ForceUpdateCanvases();
            RenderPipeline.SubmitRenderRequest(camera, request);
            RenderTexture.active = target;
            texture.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
            texture.Apply();
            var pixels = texture.GetPixels32();
            Require(pixels.Any(pixel => pixel.r > 8 || pixel.g > 8 || pixel.b > 8)
                && pixels.Any(pixel => !pixel.Equals(pixels[0])), "capture contains nonuniform rendered scene");
            File.WriteAllBytes(path, texture.EncodeToPNG());
        }
        finally
        {
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(target);
            Destroy(texture);
        }
    }

    private static object Get(object target, string name) => target.GetType().GetField(name, Fields).GetValue(target);
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("Cell definition PLAYER check failed: " + message);
    }
}
#endif

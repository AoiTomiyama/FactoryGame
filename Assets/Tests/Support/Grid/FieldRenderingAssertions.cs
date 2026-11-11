#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Cysharp.Threading.Tasks;
using UnityEditor;
using UnityEditor.SceneManagement;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

/// <summary>保存済みフィールドを同じカメラで比較する。設定の切替は実行時の複製だけへ適用する。</summary>
public static class FieldRenderingAssertions
{
    private static string Output => "docs/verification/field-rendering" + (Application.isBatchMode ? "/batch" : "");
    private static int _checks;
    private static readonly MethodInfo GetStats = typeof(GPUResidentDrawer).GetMethod("GetDebugStats", BindingFlags.Static | BindingFlags.NonPublic);
    [Serializable] private sealed class Result
    {
        public string unity, gpu, api;
        public int width = 1280, height = 720, checks;
        public string timingScope = "Editor batch frames; no FPS claim for standalone player. GPU debug counters collected separately from timing.";
        public List<Sample> samples = new();
        public List<ImageDifference> images = new();
    }
    [Serializable] private sealed class Sample
    {
        public string pose, mode;
        public int pass, frames, drawCalls, triangles, cpuVisibleInstances, gpuVisibleInstances, cpuVisiblePrimitives, gpuVisiblePrimitives;
        public double medianFrameMs, p95FrameMs, gpuFrameMs;
        public float ambient00;
    }
    [Serializable] private sealed class ImageDifference
    {
        public string name;
        public double changedFraction, meanChannelError;
    }


    public static async UniTask Verify()
    {
        UniversalRenderPipelineAsset original = null, variant = null;
        RenderTexture target = null;

        var stripping = UnityEditor.Rendering.EditorGraphicsSettings.batchRendererGroupShaderStrippingMode;
        SetStripping(UnityEditor.Rendering.BatchRendererGroupStrippingMode.KeepAll);
        var result = new Result { unity = Application.unityVersion, gpu = SystemInfo.graphicsDeviceName, api = SystemInfo.graphicsDeviceType.ToString() };
        try
        {
            _checks = 0; AudioListener.volume = 0;
            result.timingScope = Application.isBatchMode ? "Editor batch with explicit camera render; not standalone FPS" : "Editor Game View normal camera rendering; not standalone FPS";
            await UniTask.DelayFrame(12);
            original = (UniversalRenderPipelineAsset)QualitySettings.renderPipeline;
            using var drawCalls = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count", 1);
            using var triangles = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Triangles Count", 1);
            Require(original.gpuResidentDrawerMode == GPUResidentDrawerMode.Disabled && !original.gpuResidentDrawerEnableOcclusionCullingInCameras,
                "saved default retains original rendering after comparison");
            Require(original.IsGPUResidentDrawerSupportedBySRP(out var message, out _), "supported pipeline: " + message);
            Require(SystemInfo.supportsComputeShaders, "compute shaders supported");
            Require(UnityEditor.Rendering.EditorGraphicsSettings.batchRendererGroupShaderStrippingMode
                == UnityEditor.Rendering.BatchRendererGroupStrippingMode.KeepAll, "BRG shader variants survive player build");
            var camera = Camera.main;
            var position = camera.transform.position; var rotation = camera.transform.rotation; var size = camera.orthographicSize;
            var controller = TestScenes.Find<CameraControllerAdapter>();
            controller.enabled = false;
            camera.GetComponent<Unity.Cinemachine.CinemachineBrain>().enabled = false;
            Time.timeScale = 0; QualitySettings.vSyncCount = 0; Application.targetFrameRate = -1;
            target = new RenderTexture(result.width, result.height, 24) { name = "FieldRenderingVerification" };
            target.Create(); camera.targetTexture = target; camera.aspect = (float)result.width / result.height;
            // バッチEditorには可視Game Viewがないため、描画を明示して実GPU経路を検証する。
            camera.enabled = !Application.isBatchMode;
            await RenderFrames(camera, 12);
            Require(!GPUResidentDrawer.IsInstanceOcclusionCullingEnabled(), "saved default keeps GPU occlusion disabled");
            var walk = new FirstPersonWalkAdapter(GridFieldDatabaseAdapter.Instance, 1.6f, 3, .12f);
            Require(walk.TryStart(new Vector3(50, 0, 50)), "saved field has a walking entry");
            var poses = new[]
            {
                (name: "overview", position, rotation, ortho: true, size),
                (name: "wide", position: new Vector3(50, 48, 15), rotation: Quaternion.Euler(55, 0, 0), ortho: true, size: 35f),
                (name: "walking", position: walk.Position, rotation: walk.Rotation, ortho: false, size),
                (name: "field-edge", position: new Vector3(50, 2.1f, 3), rotation: Quaternion.identity, ortho: false, size)
            };
            foreach (var pose in poses)
            {
                camera.transform.SetPositionAndRotation(pose.position, pose.rotation);
                camera.orthographic = pose.ortho; camera.orthographicSize = pose.size; camera.fieldOfView = 65;
                camera.nearClipPlane = .05f;
                Color32[] baseline = null, instanced = null;
                // 順序を反転して2回測り、初回シェーダー準備や順序の影響を確認できるようにする。
                for (var pass = 0; pass < 2; pass++)
                foreach (var mode in pass == 0 ? new[] { 0, 1, 2 } : new[] { 2, 1, 0 })
                {
                    if (variant != null) { QualitySettings.renderPipeline = original; Object.Destroy(variant); }
                    variant = TestScenes.Clone(original);
                    variant.name = "FieldRenderingComparison";
                    variant.gpuResidentDrawerMode = mode == 0 ? GPUResidentDrawerMode.Disabled : GPUResidentDrawerMode.InstancedDrawing;
                    variant.gpuResidentDrawerEnableOcclusionCullingInCameras = mode == 2;
                    QualitySettings.renderPipeline = variant;
                    await RenderFrames(camera, 45);
                    Require(GPUResidentDrawer.IsInstanceOcclusionCullingEnabled() == (mode == 2), "runtime GPU mode " + mode);
                    SetDebugStats(false);
                    var times = new List<double>(); var gpuTimes = new List<double>();
                    var timings = new FrameTiming[1];
                    var before = Time.realtimeSinceStartupAsDouble;
                    for (var frame = 0; frame < 60; frame++)
                    {
                        FrameTimingManager.CaptureFrameTimings();
                        if (Application.isBatchMode) camera.Render();
                        await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate);
                        var now = Time.realtimeSinceStartupAsDouble; times.Add((now - before) * 1000); before = now;
                        if (FrameTimingManager.GetLatestTimings(1, timings) > 0 && timings[0].gpuFrameTime > 0) gpuTimes.Add(timings[0].gpuFrameTime);
                    }
                    var sample = new Sample { pose = pose.name, mode = mode == 0 ? "original" : mode == 1 ? "instanced" : "occlusion", pass = pass,
                        frames = times.Count, medianFrameMs = Percentile(times, .5), p95FrameMs = Percentile(times, .95),
                        gpuFrameMs = gpuTimes.Count == 0 ? 0 : Percentile(gpuTimes, .5), drawCalls = (int)drawCalls.LastValue, triangles = (int)triangles.LastValue,
                        ambient00 = RenderSettings.ambientProbe[0, 0] };
                    SetDebugStats(true);
                    await RenderFrames(camera, 30);
                    ReadViewStats(camera, sample);
                    if (mode != 0) Require(sample.cpuVisibleInstances > 0, "GPU drawer processes visible scene meshes " + pose.name);
                    if (mode == 2) Require(sample.gpuVisibleInstances <= sample.cpuVisibleInstances, "GPU visibility is conservative " + pose.name);
                    result.samples.Add(sample);
                    if (pass == 0)
                    {
                        var pixels = Capture(target, pose.name + "-" + sample.mode);
                        if (mode == 0) baseline = pixels;
                        if (mode == 1) { instanced = pixels; result.images.Add(Compare(pose.name + "-original-to-instanced", baseline, pixels)); }
                        if (mode == 2)
                        {
                            var diff = Compare(pose.name + "-occlusion", instanced, pixels); result.images.Add(diff);
                            Require(diff.changedFraction < .01, "occlusion preserves visible image " + pose.name + " difference=" + diff.changedFraction);
                        }
                    }
                    else if (mode == 0)
                        result.images.Add(Compare(pose.name + "-original-repeat", baseline, Capture(target, pose.name + "-original-repeat")));
                    Debug.Log("Field rendering sample: " + JsonUtility.ToJson(sample));
                }
            }
            Require(result.samples.Where(s => s.mode == "occlusion").Any(s => s.gpuVisibleInstances < s.cpuVisibleInstances), "GPU actually removes occluded instances");
            await VerifyDynamic(camera, target, variant, result);
            result.checks = _checks;
            Directory.CreateDirectory(Output); File.WriteAllText(Output + "/comparison.json", JsonUtility.ToJson(result, true));
            Debug.Log($"Field rendering checks passed: {_checks} assertions; 3 modes, 4 poses, reversed order, dynamic meshes and pixel comparison.");

        }
        catch (Exception exception)
        {
            Directory.CreateDirectory(Output); File.WriteAllText(Output + "/comparison.json", JsonUtility.ToJson(result, true));
            throw;
        }
        finally
        {
            Time.timeScale = 1;
            if (Camera.main != null) Camera.main.targetTexture = null;
            if (original != null) QualitySettings.renderPipeline = original;
            if (variant != null) Object.Destroy(variant);
            if (target != null) { target.Release(); Object.Destroy(target); }
            SetStripping(stripping);
        }
    }
    private static async UniTask VerifyDynamic(Camera camera, RenderTexture target, UniversalRenderPipelineAsset pipeline, Result result)
    {
        pipeline.gpuResidentDrawerMode = GPUResidentDrawerMode.InstancedDrawing;
        camera.transform.SetPositionAndRotation(new Vector3(50, 2.1f, 3), Quaternion.identity); camera.orthographic = false;
        var placer = TestScenes.Find<CellPlacerAdapter>();
        var databases = (CellDatabaseDefinition[])typeof(CellPlacerAdapter).GetField("cellDatabaseArr", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(placer);
        var prefab = databases.SelectMany(db => db.GetAllCellInfos()).Single(info => info.CellTypeDomain == CellTypeDomain.Storage).FieldCellPrefab.gameObject;
        var cell = TestScenes.Clone(prefab, new Vector3(50, 0, 7), Quaternion.identity);
        // ゲーム状態へ登録せず、実際の設備PrefabのRenderer登録・移動・破棄を同じ画面で比較する。
        try
        {
            Color32[] previous = null;
            foreach (var state in new[] { "placed", "moved", "removed" })
            {
                if (state == "moved") cell.transform.position += Vector3.right * 2;
                if (state == "removed") { Object.Destroy(cell); await UniTask.DelayFrame(1); }
                pipeline.gpuResidentDrawerEnableOcclusionCullingInCameras = false;
                await RenderFrames(camera, 30);
                var reference = Capture(target, "dynamic-" + state + "-off");
                if (previous != null) Require(Compare("dynamic-visible-change", previous, reference).changedFraction > .0001, "mesh change is visible " + state);
                previous = reference;
                pipeline.gpuResidentDrawerEnableOcclusionCullingInCameras = true;
                await RenderFrames(camera, 30);
                Require(GPUResidentDrawer.IsInstanceOcclusionCullingEnabled(), "dynamic occlusion active");
                var diff = Compare("dynamic-" + state, reference, Capture(target, "dynamic-" + state + "-on"));
                result.images.Add(diff); Require(diff.changedFraction < .01, "dynamic image preserved " + state);
            }
        }
        finally { if (cell != null) Object.Destroy(cell); }
    }
    private static object DebugStats() => GetStats.Invoke(null, null);
    private static void SetStripping(UnityEditor.Rendering.BatchRendererGroupStrippingMode mode)
    {
        var settings = new SerializedObject(GraphicsSettings.GetGraphicsSettings());
        settings.FindProperty("m_BrgStripping").intValue = (int)mode;
        settings.ApplyModifiedPropertiesWithoutUndo();
    }
    private static async UniTask RenderFrames(Camera camera, int count)
    {
        for (var i = 0; i < count; i++)
        { if (Application.isBatchMode) camera.Render(); await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate); }
    }
    private static void SetDebugStats(bool enabled)
    {
        var stats = DebugStats(); if (stats != null) stats.GetType().GetField("enabled").SetValue(stats, enabled);
    }
    private static void ReadViewStats(Camera camera, Sample sample)
    {
        var stats = DebugStats(); if (stats == null) return;
        var list = stats.GetType().GetField("instanceCullerStats").GetValue(stats);
        var type = list.GetType(); var count = (int)type.GetProperty("Length").GetValue(list);
        for (var i = 0; i < count; i++)
        {
            var row = type.GetProperty("Item").GetValue(list, new object[] { i }); var rowType = row.GetType();
            int Read(string name) => (int)rowType.GetField(name).GetValue(row);
            if (Read("viewInstanceID") != camera.GetInstanceID() || rowType.GetField("viewType").GetValue(row).ToString() != "Camera") continue;
            sample.cpuVisibleInstances = Read("visibleInstancesOnCPU"); sample.gpuVisibleInstances = Read("visibleInstancesOnGPU");
            sample.cpuVisiblePrimitives = Read("visiblePrimitivesOnCPU"); sample.gpuVisiblePrimitives = Read("visiblePrimitivesOnGPU");
            return;
        }
    }
    private static Color32[] Capture(RenderTexture target, string name)
    {
        var previous = RenderTexture.active; RenderTexture.active = target;
        var image = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
        image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); image.Apply();
        var pixels = image.GetPixels32(); Directory.CreateDirectory(Output);
        File.WriteAllBytes(Output + "/" + name + ".png", image.EncodeToPNG());
        Object.Destroy(image); RenderTexture.active = previous; return pixels;
    }
    private static ImageDifference Compare(string name, Color32[] a, Color32[] b)
    {
        var changed = 0; long error = 0;
        for (var i = 0; i < a.Length; i++)
        {
            var r = Math.Abs(a[i].r - b[i].r); var g = Math.Abs(a[i].g - b[i].g); var blue = Math.Abs(a[i].b - b[i].b);
            if (Math.Max(r, Math.Max(g, blue)) > 8) changed++;
            error += r + g + blue;
        }
        return new ImageDifference { name = name, changedFraction = (double)changed / a.Length, meanChannelError = (double)error / (a.Length * 3) };
    }
    private static double Percentile(List<double> values, double percentile)
    { values.Sort(); return values[(int)((values.Count - 1) * percentile)]; }
    private static void Require(bool condition, string message)
    { if (!condition) throw new InvalidOperationException("Field rendering check failed: " + message); _checks++; }
}

#endif

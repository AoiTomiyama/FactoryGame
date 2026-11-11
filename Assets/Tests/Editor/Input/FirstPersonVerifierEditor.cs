using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Cysharp.Threading.Tasks;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;

/// <summary>保存済みMainSceneで、一人称の実描画カメラ・入力・HUDと俯瞰復元を検証する。</summary>
[InitializeOnLoad]
public static class FirstPersonVerifierEditor
{
    private const string SessionKey = "FactoryGame.FirstPersonVerification";
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static int _checks;
    static FirstPersonVerifierEditor() => EditorApplication.playModeStateChanged += state =>
    {
        if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(SessionKey, false)) return;
        SessionState.EraseBool(SessionKey); Verify().Forget();
    };
    public static void Run()
    {
        EditorUtility.audioMasterMute = true;
        EditorSceneManager.OpenScene("Assets/Scenes/MainScene.unity");
        SessionState.SetBool(SessionKey, true); EditorApplication.isPlaying = true;
    }
    private static async UniTaskVoid Verify()
    {
        Keyboard keyboard = null; Mouse mouse = null;
        InputDevice[] previousDevices = null;
        var previousBackground = InputSystem.settings.backgroundBehavior;
        var previousEditorInput = InputSystem.settings.editorInputBehaviorInPlayMode;
        try
        {
            AudioListener.volume = 0; _checks = 0;
            // バッチにはGame Viewフォーカスがないため、検証用入力だけをプレイヤー更新へ送る。
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            await UniTask.DelayFrame(5);
            var controller = UnityEngine.Object.FindAnyObjectByType<CameraControllerAdapter>();
            var view = UnityEngine.Object.FindAnyObjectByType<CellStatusViewAdapter>();
            var panel = UnityEngine.Object.FindAnyObjectByType<EconomyPanelAdapter>();
            var hud = UnityEngine.Object.FindAnyObjectByType<HudLayoutAdapter>();
            var placer = UnityEngine.Object.FindAnyObjectByType<CellPlacerAdapter>();
            var operation = UnityEngine.Object.FindAnyObjectByType<OperationBarAdapter>();
            var camera = Camera.main;
            var cine = (CinemachineCamera)typeof(CameraControllerAdapter).GetField("virtualCamera", Private).GetValue(controller);
            var body = cine.GetCinemachineComponent(CinemachineCore.Stage.Body);
            var grid = GridFieldDatabaseAdapter.Instance;
            var wallet = UnityEngine.Object.FindAnyObjectByType<GridFieldGeneratorAdapter>().Wallet;
            Require(body != null && body.enabled, "saved camera has an active Body component");
            Debug.Log("First person saved Body: " + body.GetType().Name);
            previousDevices = InputSystem.devices.Where(d => d.enabled && d is Keyboard or Mouse).ToArray();
            foreach (var device in previousDevices) InputSystem.DisableDevice(device);
            keyboard = InputSystem.AddDevice<Keyboard>(); mouse = InputSystem.AddDevice<Mouse>();
            keyboard.MakeCurrent(); mouse.MakeCurrent();
            panel.ShowPage(0); await UniTask.DelayFrame(16);
            var selected = grid.GetCell(50, 50); view.UpdateUIStatusWindow(selected);
            var marker = (Transform)typeof(CellStatusViewAdapter).GetField("selectionMarker", Private).GetValue(view);
            var overviewPosition = camera.transform.position; var overviewRotation = camera.transform.rotation;
            var orthographicSize = camera.orthographicSize;
            var bodyEnabled = body.enabled; var lens = cine.Lens; var cursorLock = Cursor.lockState; var cursorVisible = Cursor.visible;
            operation.WalkButton.onClick.Invoke();
            Require(controller.IsTransitioning && Vector3.Distance(camera.transform.position, overviewPosition) < .01f, "entry begins at the rendered overview pose");
            await UniTask.Delay(150, ignoreTimeScale:true);
            Require(controller.IsTransitioning && Vector3.Distance(camera.transform.position, overviewPosition) > .1f, "entry passes through intermediate positions");
            await WaitForMode(controller, true);
            await UniTask.DelayFrame(3);
            Require(controller.IsFirstPerson, "button enters first person");
            var walk = (FirstPersonWalkAdapter)typeof(CameraControllerAdapter).GetField("_walk", Private).GetValue(controller);
            Debug.Log("First person actual=" + camera.transform.position + " expected=" + walk.Position + " bodyEnabled=" + body.enabled);
            Require(Vector3.Distance(camera.transform.position, walk.Position) < .01f, "rendered camera reaches walking eye position");
            Require(Quaternion.Angle(camera.transform.rotation, walk.Rotation) < .1f, "rendered camera follows walking orientation");
            Require(!body.enabled && !camera.orthographic && Mathf.Abs(camera.fieldOfView - 65) < .1f, "actual Body suspended and perspective enabled");
            Require(Mathf.Abs(camera.transform.position.y - 2.1f) < .01f && walk.CanStandAt(camera.transform.position), "entry is above a clear cell floor");
            Require(!placer.PlacementEnabled && placer.PreviewCount == 0 && !operation.Bar.gameObject.activeInHierarchy, "placement and its lower bar hidden");
            Require(!hud.Navigation.gameObject.activeInHierarchy && !hud.Body.gameObject.activeInHierarchy && !hud.Close.gameObject.activeInHierarchy, "left UI fully hidden");
            Require(view.GetComponent<CanvasGroup>().alpha == 0 && !view.GetComponent<CanvasGroup>().blocksRaycasts, "right UI hidden and noninteractive");
            Require(panel.SummaryRect.gameObject.activeInHierarchy && operation.WalkingHelp.gameObject.activeInHierarchy, "only wallet and movement instructions remain");
            var before = camera.transform.rotation;
            InputSystem.QueueStateEvent(mouse, new MouseState { delta = new Vector2(40, -20) }); await UniTask.DelayFrame(2);
            Debug.Log("Mouse check: before="+before.eulerAngles+" camera="+camera.transform.eulerAngles+" walk="+walk.Rotation.eulerAngles+" current="+Mouse.current.deviceId+" fake="+mouse.deviceId+" enabled="+mouse.enabled);
            Require(Quaternion.Angle(before, camera.transform.rotation) > 1 && Quaternion.Angle(camera.transform.rotation, walk.Rotation) < .1f, "mouse look drives rendered camera");
            var positionBeforeMove = camera.transform.position;
            await Press(keyboard, Key.W);
            Require(Vector3.Distance(positionBeforeMove, camera.transform.position) > .001f && Vector3.Distance(camera.transform.position, walk.Position) < .01f,
                "WASD input moves the rendered camera");
            Require(!marker.gameObject.activeInHierarchy, "world selection marker hidden during first person");
            var balance = wallet.Current.Balance;
            var cursor = UnityEngine.Object.FindAnyObjectByType<PlayerCursorBehaviourAdapter>();
            var placementRotation = cursor.transform.rotation;
            cursor.RotatePlacement(); placer.PointerBegin(); placer.PointerDrag(); placer.PointerEnd();
            Require(wallet.Current.Balance == balance && Quaternion.Angle(placementRotation, cursor.transform.rotation) < .01f, "first person cannot place or rotate equipment");
            foreach (var resolution in new[] { new Vector2(1600,900), new Vector2(1366,768) })
            {
                var canvas = operation.GetComponent<Canvas>(); var root = canvas.GetComponent<RectTransform>();
                var previousMode = canvas.renderMode; var previousSize = root.sizeDelta; var scaler = canvas.GetComponent<CanvasScaler>();
                var previousScaling = scaler.enabled; canvas.renderMode = RenderMode.WorldSpace; scaler.enabled = false;
                root.sizeDelta = new Vector2(resolution.x * 720 / resolution.y, 720); Canvas.ForceUpdateCanvases();
                foreach (var text in operation.WalkingHelp.GetComponentsInChildren<Text>())
                    Require(text.preferredHeight <= text.rectTransform.rect.height + 1 && text.preferredWidth <= text.rectTransform.rect.width + 1, "movement guide fits " + resolution);
                canvas.renderMode = previousMode; root.sizeDelta = previousSize; scaler.enabled = previousScaling;
            }
            await Press(keyboard, Key.Escape);
            Require(controller.IsTransitioning && !placer.PlacementEnabled, "exit transition keeps placement blocked");
            await WaitForMode(controller, false);
            Require(!controller.IsFirstPerson && body.enabled == bodyEnabled && camera.orthographic && Mathf.Abs(camera.orthographicSize - orthographicSize) < .01f, "Escape restores projection and Body");
            Require(Vector3.Distance(camera.transform.position, overviewPosition) < .05f && Quaternion.Angle(camera.transform.rotation, overviewRotation) < .1f, "overview pose restored");
            Require(Cursor.lockState == cursorLock && Cursor.visible == cursorVisible && cine.Lens.ModeOverride == lens.ModeOverride, "cursor and lens override restored");
            Require(view.SelectedCell == selected && marker.gameObject.activeInHierarchy && view.GetComponent<CanvasGroup>().alpha == 1, "selected equipment and right UI restored");
            Require(placer.PlacementEnabled && operation.Bar.gameObject.activeInHierarchy && !operation.WalkingHelp.gameObject.activeInHierarchy && hud.Navigation.gameObject.activeInHierarchy, "placement HUD restored");
            for (var page = 0; page < 3; page++)
            {
                panel.ShowPage(page); var expanded = page != 1; panel.SetExpanded(expanded);
                await Press(keyboard, Key.F); await WaitForMode(controller, true); Require(controller.IsFirstPerson, "F enters from each page");
                wallet.TryReserveIncome(1, out var income); income.TryCommit();
                Require(panel.BalanceText.Contains(wallet.Current.Balance.ToString()), "wallet keeps updating during first person");
                await Press(keyboard, Key.F); await WaitForMode(controller, false); Require(!controller.IsFirstPerson && panel.SelectedPage == page && panel.IsExpanded == expanded, "F restores page and expansion");
            }
            controller.SetFirstPerson(true); await UniTask.DelayFrame(3);
            controller.SetFirstPerson(false); await UniTask.DelayFrame(3); controller.SetFirstPerson(true);
            await WaitForMode(controller,true); Require(controller.IsFirstPerson, "mid-transition reversal completes without losing the overview snapshot");
            controller.SetFirstPerson(false); await WaitForMode(controller,false);
            controller.SetFirstPerson(true); controller.enabled = false;
            Require(!controller.IsFirstPerson && body.enabled == bodyEnabled && Cursor.lockState == cursorLock, "disable restores camera and cursor"); controller.enabled = true;
            controller.SetFirstPerson(true); typeof(CameraControllerAdapter).GetMethod("OnApplicationFocus", Private).Invoke(controller,new object[]{false});
            Require(!controller.IsFirstPerson, "focus loss leaves walking mode");
            VerifyWalking(grid);
            Debug.Log("First person checks passed: " + _checks + " assertions; actual saved Body, rendered pose, perspective, mouse, F/Esc, movement, collision, UI/input blocking, wallet, restore, focus/disable, selected equipment restore, bidirectional eased transition and PC2 fit.");
            EditorApplication.Exit(0);
        }
        catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
        finally
        {
            if (keyboard != null) InputSystem.RemoveDevice(keyboard); if (mouse != null) InputSystem.RemoveDevice(mouse);
            if (previousDevices != null) foreach (var device in previousDevices) if (device.added) InputSystem.EnableDevice(device);
            InputSystem.settings.backgroundBehavior = previousBackground;
            InputSystem.settings.editorInputBehaviorInPlayMode = previousEditorInput;
        }
    }
    private static async UniTask WaitForMode(CameraControllerAdapter controller, bool active)
    {
        await UniTask.WaitUntil(() => !controller.IsTransitioning && controller.IsFirstPerson == active).Timeout(TimeSpan.FromSeconds(3));
        await UniTask.DelayFrame(2);
    }
    private static async UniTask Press(Keyboard keyboard, Key key)
    {
        keyboard.MakeCurrent(); InputSystem.QueueStateEvent(keyboard, new KeyboardState(key)); await UniTask.DelayFrame(2);
        InputSystem.QueueStateEvent(keyboard, new KeyboardState()); await UniTask.DelayFrame(2);
    }
    private static void VerifyWalking(GridFieldDatabaseAdapter grid)
    {
        for (var x=10;x<=14;x++) for(var z=10;z<=15;z++) Install(grid,"Empty",x,z);
        Install(grid,"Storage",12,14);
        var walk = new FirstPersonWalkAdapter(grid,1.6f,3,.12f); Require(walk.TryStart(new Vector3(12,0,12)),"walk fixture starts");
        var start=walk.Position; walk.Step(Vector2.up,Vector2.zero,false,.05f);
        var speed=Vector3.Distance(start,walk.Position);
        var fast=new FirstPersonWalkAdapter(grid,1.6f,3,.12f); fast.TryStart(new Vector3(12,0,12)); fast.Step(Vector2.up,Vector2.zero,true,.05f);
        Require(Mathf.Abs(Vector3.Distance(start,fast.Position)-speed*2)<.001f,"Shift doubles speed");
        var diagonal=new FirstPersonWalkAdapter(grid,1.6f,3,.12f); diagonal.TryStart(new Vector3(12,0,12)); diagonal.Step(Vector2.one,Vector2.zero,false,.05f);
        Require(Mathf.Abs(Vector3.Distance(start,diagonal.Position)-speed)<.001f,"diagonal speed normalized");
        for(var i=0;i<40;i++) walk.Step(Vector2.up,Vector2.zero,false,.05f);
        Require(walk.Position.z<13.31f && walk.CanStandAt(walk.Position),"equipment blocks movement");
        Require(!walk.CanStandAt(new Vector3(-1,2.1f,0)) && !walk.CanStandAt(new Vector3(50,2.1f,50)),"boundary and 5x5 singularity blocked");
        walk.Step(Vector2.zero,new Vector2(0,100000),false,.05f);
        Require(Mathf.Abs((walk.Rotation*Vector3.forward).y)<.99f && Mathf.Abs(walk.Position.y-2.1f)<.001f,"look pitch clamped and eye stays above floor");
    }
    private static void Install(GridFieldDatabaseAdapter grid,string name,int x,int z)
    {
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Field/General/F_"+name+"Cell.prefab");
        var cell=UnityEngine.Object.Instantiate(prefab,new Vector3(x,0,z),Quaternion.identity,grid.transform).GetComponent<CellBaseAdapter>();
        var previous=grid.CaptureCells(cell.Footprint.Coordinates(x,z,0)); Require(grid.TryReplaceCells(new[]{cell},previous),"fixture occupancy");
        foreach(var old in previous.Select(e=>e.Owner).Distinct()) { if(old is ConnectableCellBaseAdapter c)c.OnDisconnect(); old.gameObject.SetActive(false); UnityEngine.Object.Destroy(old.gameObject); }
        cell.InitializeSystem();
    }
    private static void Require(bool value,string label) { _checks++; if(!value)throw new InvalidOperationException("First person: "+label); }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.Rendering;

namespace Wattshift.Editor
{
    /// <summary>Runs real Play mode checks and captures the real Game view; never generates substitute art.</summary>
    [InitializeOnLoad]
    public static class GateOneEvidence
    {
        const string Key = "Wattshift.G1Evidence";
        const string Folder = "docs/evidence/g1/";
        static int stage;
        static double next;
        static int lastFrame;
        static Vector3 cameraBefore;
        static CityBuilding target;
        static Mouse mouse;
        static Keyboard keyboard;
        static bool ownMouse, ownKeyboard;
        static bool pendingPlay;
        static double playAt;
        static Touchscreen touch;
        static readonly List<float> frames = new List<float>();
        static readonly List<string> checks = new List<string>();
        static readonly List<string> errors = new List<string>();
        static readonly List<string> editorErrors = new List<string>();
        static GateOneEvidence()
        {
            EditorApplication.update += Update;
            Application.logMessageReceived += (message, stack, type) =>
            {
                if (!SessionState.GetBool(Key, false) || (type != LogType.Error && type != LogType.Exception && type != LogType.Assert)) return;
                if (stack.Contains("UnityEditor.Search.SearchDatabase")) editorErrors.Add(message + "\n" + stack);
                else errors.Add(message + "\n" + stack);
            };
        }

        [MenuItem("WATTSHIFT/Capture and verify G1")]
        public static void Start()
        {
            Directory.CreateDirectory(Folder);
            ShaderUtil.allowAsyncCompilation = false;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            EditorSceneManager.OpenScene(CitySliceBuilder.ScenePath);
            SetGameSize(1920, 1080);
            SessionState.SetBool(Key, true);
            SessionState.SetInt(Key + "Stage", 0);
            // Unity's default Search index must initialize in Edit mode before Play is requested.
            pendingPlay = true;
            playAt = EditorApplication.timeSinceStartup + 5;
        }

        public static void BuildAndStart()
        {
            CitySliceBuilder.Build();
            Start();
        }

        static void Update()
        {
            if (pendingPlay && EditorApplication.timeSinceStartup >= playAt && !EditorApplication.isCompiling)
            {
                pendingPlay = false;
                EditorApplication.EnterPlaymode();
            }
            if (!SessionState.GetBool(Key, false) || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
            try { Tick(); }
            catch (Exception ex)
            {
                File.WriteAllText(Folder + "capture-failure.txt", ex.ToString());
                SessionState.SetBool(Key, false); Debug.LogException(ex); EditorApplication.Exit(1);
            }
        }

        static void Tick()
        {
            if (stage == 0)
            {
                next = EditorApplication.timeSinceStartup + 12;
                new GameObject("Editor-only gate evidence sampler").AddComponent<GateRuntimeProbe>();
                stage = 1;
                return;
            }
            if (Time.frameCount != lastFrame)
            {
                lastFrame = Time.frameCount;
                if (stage == 1 && Time.unscaledDeltaTime > 0 && EditorApplication.timeSinceStartup > next - 6) frames.Add(Time.unscaledDeltaTime);
            }
            if (EditorApplication.timeSinceStartup < next) return;
            if (stage == 1 && GateRuntimeProbe.Frames.Count < 180) return;
            var overlay = CityOverlay.Instance;
            Camera camera = Camera.main;
            if (overlay == null || camera == null) throw new InvalidOperationException("Runtime UI/camera missing");
            switch (stage)
            {
                case 1:
                    var buildings = UnityEngine.Object.FindObjectsByType<CityBuilding>(FindObjectsSortMode.None);
                    Check("eight selectable buildings", buildings.Length == 8);
                    Check("all building colliders valid", buildings.All(b => b.GetComponent<Collider>() != null));
                    var renderers = UnityEngine.Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None);
                    Check("all scene materials use supported shaders and palette textures", renderers.All(r => r.sharedMaterials.All(m => m != null && m.shader.isSupported && (m.mainTexture != null || m.name.Contains("grass") || m.name.Contains("edge") || m.name.Contains("pavement")))));
                    Check("URP active", GraphicsSettings.currentRenderPipeline != null);
                    Check("directional shadows enabled", RenderSettings.sun != null && RenderSettings.sun.shadows != LightShadows.None);
                    var pipeline = UnityEngine.Rendering.Universal.UniversalRenderPipeline.asset;
                    File.WriteAllText(Folder + "render-diagnostics.txt", "pipeline=" + pipeline.name + " shadowSupport=" + pipeline.supportsMainLightShadows + " shadowDistance=" + pipeline.shadowDistance + "\nShadowMap=" + Shader.GetGlobalTexture("_MainLightShadowmapTexture") + "\nMainLightPosition=" + Shader.GetGlobalVector("_MainLightPosition") + "\nMainLightColor=" + Shader.GetGlobalVector("_MainLightColor") + "\ncasters=" + renderers.Count(r => r.shadowCastingMode != ShadowCastingMode.Off) + "\n");
                    checks.Add("INFO: runtime viewport " + GateRuntimeProbe.Width + "x" + GateRuntimeProbe.Height + " camera aspect=" + GateRuntimeProbe.Aspect + " ortho=" + GateRuntimeProbe.OrthographicSize);
                    ScreenCapture.CaptureScreenshot(Path.GetFullPath(Folder + "01-city-1920x1080.png"));
                    frames.Clear(); frames.AddRange(GateRuntimeProbe.Frames); GateRuntimeProbe.Frames.Clear();
                    target = buildings.First(b => b.Essential);
                    mouse = Mouse.current; if (mouse == null) { mouse = InputSystem.AddDevice<Mouse>(); ownMouse = true; }
                    keyboard = Keyboard.current; if (keyboard == null) { keyboard = InputSystem.AddDevice<Keyboard>(); ownKeyboard = true; }
                    stage++; next = EditorApplication.timeSinceStartup + 2;
                    break;
                case 2:
                    Vector2 p = camera.WorldToScreenPoint(target.GetComponent<Collider>().bounds.center);
                    Check("clinic click point outside UI", !overlay.IsPointerOverPanel(p));
                    InputSystem.QueueStateEvent(mouse, new MouseState { position = p, buttons = 1 });
                    stage++; next = EditorApplication.timeSinceStartup + .3;
                    break;
                case 3:
                    InputSystem.QueueStateEvent(mouse, new MouseState { position = camera.WorldToScreenPoint(target.GetComponent<Collider>().bounds.center) });
                    stage++; next = EditorApplication.timeSinceStartup + 3;
                    break;
                case 4:
                    Check("raycast mouse click selects protected clinic", overlay.SelectedBuilding == target);
                    ScreenCapture.CaptureScreenshot(Path.GetFullPath(Folder + "02-clinic-selection-1920x1080.png"));
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(UnityEngine.InputSystem.Key.D));
                    cameraBefore = camera.transform.position;
                    stage++; next = EditorApplication.timeSinceStartup + .5;
                    break;
                case 5:
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                    Check("keyboard camera pan moves camera", Vector3.Distance(cameraBefore, camera.transform.position) > .1f);
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(UnityEngine.InputSystem.Key.Escape));
                    stage++; next = EditorApplication.timeSinceStartup + .4;
                    break;
                case 6:
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                    Check("Escape closes selection", overlay.SelectedBuilding == null);
                    camera.GetComponent<IsoCameraController>().ResetView();
                    SetGameSize(900, 1200);
                    stage++; next = EditorApplication.timeSinceStartup + 3;
                    break;
                case 7:
                    checks.Add("INFO: portrait runtime viewport " + GateRuntimeProbe.Width + "x" + GateRuntimeProbe.Height + " camera aspect=" + GateRuntimeProbe.Aspect + " ortho=" + GateRuntimeProbe.OrthographicSize);
                    touch = InputSystem.AddDevice<Touchscreen>("G1 touch verification");
                    InputSystem.QueueStateEvent(touch, new TouchState { touchId = 1, phase = UnityEngine.InputSystem.TouchPhase.Began, position = camera.WorldToScreenPoint(target.GetComponent<Collider>().bounds.center) });
                    stage++; next = EditorApplication.timeSinceStartup + .3;
                    break;
                case 8:
                    InputSystem.QueueStateEvent(touch, new TouchState { touchId = 1, phase = UnityEngine.InputSystem.TouchPhase.Ended, position = camera.WorldToScreenPoint(target.GetComponent<Collider>().bounds.center) });
                    stage++; next = EditorApplication.timeSinceStartup + 1;
                    break;
                case 9:
                    Check("simulated touch tap selects protected clinic", overlay.SelectedBuilding == target);
                    ScreenCapture.CaptureScreenshot(Path.GetFullPath(Folder + "03-portrait-900x1200.png"));
                    stage++; next = EditorApplication.timeSinceStartup + 2;
                    break;
                case 10:
                    Check("landscape PNG is 1920x1080", PngSize(Folder + "01-city-1920x1080.png") == new Vector2Int(1920, 1080));
                    Check("selection PNG is 1920x1080", PngSize(Folder + "02-clinic-selection-1920x1080.png") == new Vector2Int(1920, 1080));
                    Check("portrait PNG is 900x1200", PngSize(Folder + "03-portrait-900x1200.png") == new Vector2Int(900, 1200));
                    Check("no runtime errors or exceptions", errors.Count == 0);
                    Check("no Editor startup indexing errors", editorErrors.Count == 0);
                    File.WriteAllLines(Folder + "playmode-checks.txt", checks.Concat(errors.Select(e => "ERROR: " + e)));
                    File.WriteAllLines(Folder + "editor-startup-errors.txt", editorErrors);
                    frames.Sort();
                    File.WriteAllText(Folder + "editor-frame-sample.txt", "Actual Editor Play mode sample on " + SystemInfo.deviceModel + " / " + SystemInfo.graphicsDeviceName + "\nViewport: 1920x1080\nSamples: " + frames.Count + "\nMedian frame ms: " + (frames.Count == 0 ? 0 : frames[frames.Count / 2] * 1000) + "\nP95 frame ms: " + (frames.Count == 0 ? 0 : frames[(int)((frames.Count - 1) * .95f)] * 1000) + "\nIncludes Editor overhead; not browser/device performance acceptance.\n");
                    if (ownMouse) InputSystem.RemoveDevice(mouse);
                    if (ownKeyboard) InputSystem.RemoveDevice(keyboard);
                    InputSystem.RemoveDevice(touch);
                    SetGameSize(1920, 1080);
                    SessionState.SetBool(Key, false);
                    Debug.Log("WATTSHIFT_G1_EVIDENCE_DONE " + string.Join("; ", checks));
                    EditorApplication.Exit(checks.Any(s => s.StartsWith("FAIL")) ? 1 : 0);
                    break;
            }
        }

        static void Check(string name, bool value) { checks.Add((value ? "PASS: " : "FAIL: ") + name); }

        static Vector2Int PngSize(string path)
        {
            byte[] data = File.ReadAllBytes(path);
            int w = (data[16] << 24) | (data[17] << 16) | (data[18] << 8) | data[19];
            int h = (data[20] << 24) | (data[21] << 16) | (data[22] << 8) | data[23];
            return new Vector2Int(w, h);
        }

        static void SetGameSize(int width, int height)
        {
            Assembly assembly = typeof(UnityEditor.Editor).Assembly;
            Type sizesType = assembly.GetType("UnityEditor.GameViewSizes");
            Type singleton = typeof(ScriptableSingleton<>).MakeGenericType(sizesType);
            object sizes = singleton.GetProperty("instance", BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy).GetValue(null);
            Type groupType = assembly.GetType("UnityEditor.GameViewSizeGroupType");
            object group = sizesType.GetMethod("GetGroup").Invoke(sizes, new[] { Enum.Parse(groupType, "Standalone") });
            Type sizeType = assembly.GetType("UnityEditor.GameViewSize");
            Type sizeEnum = assembly.GetType("UnityEditor.GameViewSizeType");
            object size = Activator.CreateInstance(sizeType, new[] { Enum.Parse(sizeEnum, "FixedResolution"), (object)width, height, "WATTSHIFT " + width + "×" + height });
            group.GetType().GetMethod("AddCustomSize").Invoke(group, new[] { size });
            int index = (int)group.GetType().GetMethod("GetTotalCount").Invoke(group, null) - 1;
            Type gameType = assembly.GetType("UnityEditor.GameView");
            EditorWindow game = EditorWindow.GetWindow(gameType);
            gameType.GetProperty("selectedSizeIndex", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).SetValue(game, index);
            game.Show(); game.Focus(); game.Repaint();
        }
    }
}

using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Wattshift.Tests
{
    public sealed class CameraControlsTests
    {
        Camera camera;
        IsoCameraController rig;
        CityOverlay ui;
        Mouse mouse;
        Keyboard keyboard;
        Touchscreen touch;
        string saveDirectory;
        InputSettings.EditorInputBehaviorInPlayMode previousInputMode;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            SetViewport();
            saveDirectory=System.IO.Path.Combine(System.IO.Path.GetTempPath(),"WattshiftControls-"+System.Guid.NewGuid().ToString("N"));
            GameSession.EditorTestSaveDirectory=saveDirectory;
            previousInputMode = InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            SceneManager.LoadScene("CitySlice");
            yield return null;
            if (GameSession.Instance != null) GameSession.Instance.BeginMission(0);
            mouse = InputSystem.AddDevice<Mouse>("G11 test mouse");
            keyboard = InputSystem.AddDevice<Keyboard>("G11 test keyboard");
            touch = InputSystem.AddDevice<Touchscreen>("G11 test touch");
            camera = Camera.main; rig = camera.GetComponent<IsoCameraController>(); ui = CityOverlay.Instance;
            yield return new WaitForSecondsRealtime(.6f);
            rig.ResetView(); ui.Select(null);
            yield return null;
        }

        internal static void SetViewport(int width = 1920, int height = 1080)
        {
            var assembly = typeof(UnityEditor.Editor).Assembly;
            var sizesType = assembly.GetType("UnityEditor.GameViewSizes");
            var singleton = typeof(UnityEditor.ScriptableSingleton<>).MakeGenericType(sizesType);
            object sizes = singleton.GetProperty("instance", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.FlattenHierarchy).GetValue(null);
            object group = sizesType.GetMethod("GetGroup").Invoke(sizes, new[] { System.Enum.Parse(assembly.GetType("UnityEditor.GameViewSizeGroupType"), "Standalone") });
            object size = System.Activator.CreateInstance(assembly.GetType("UnityEditor.GameViewSize"), new[] { System.Enum.Parse(assembly.GetType("UnityEditor.GameViewSizeType"), "FixedResolution"), (object)width, height, "WATTSHIFT controls test" });
            group.GetType().GetMethod("AddCustomSize").Invoke(group, new[] { size });
            int index = (int)group.GetType().GetMethod("GetTotalCount").Invoke(group, null) - 1;
            var type = assembly.GetType("UnityEditor.GameView");
            var view = UnityEditor.EditorWindow.GetWindow(type);
            type.GetProperty("selectedSizeIndex", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic).SetValue(view, index);
            view.Show(); view.Focus();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if(mouse!=null)InputSystem.RemoveDevice(mouse);if(keyboard!=null)InputSystem.RemoveDevice(keyboard);if(touch!=null)InputSystem.RemoveDevice(touch);
            InputSystem.settings.editorInputBehaviorInPlayMode = previousInputMode;
            if(GameSession.Instance!=null)GameSession.Instance.enabled=false;
            GameSession.EditorTestSaveDirectory=null;
            if(System.IO.Directory.Exists(saveDirectory))System.IO.Directory.Delete(saveDirectory,true);
            yield return null;
        }

        Vector2 World => new Vector2(Screen.width * .52f, Screen.height * .48f);
        Vector2 Panel => new Vector2(70, Screen.height - 65);
        IEnumerator MouseEvent(Vector2 p, ushort buttons = 0, float scroll = 0)
        {
            InputSystem.QueueStateEvent(mouse, new MouseState { position = p, buttons = buttons, scroll = new Vector2(0, scroll) });
            yield return null; yield return null;
        }
        IEnumerator TouchEvent(int id, UnityEngine.InputSystem.TouchPhase phase, Vector2 p)
        {
            InputSystem.QueueStateEvent(touch, new TouchState { touchId = id, phase = phase, position = p });
            yield return null; yield return null;
        }

        [UnityTest]
        public IEnumerator WheelZoomIsSmoothBoundedAndBlockedByUI()
        {
            Assert.IsTrue(ui.IsPointerOverPanel(Panel));
            float original = camera.orthographicSize;
            yield return MouseEvent(Panel, 0, 1);
            Assert.That(rig.TargetOrthographicSize, Is.EqualTo(original).Within(.001f));
            yield return MouseEvent(World, 0, 1);
            float target = rig.TargetOrthographicSize;
            Assert.Less(target, original);
            Assert.That(target, Is.EqualTo(original * Mathf.Exp(-120 * rig.ZoomSensitivity)).Within(.01f), "A real normalized tick must provide useful zoom");
            Assert.That(camera.orthographicSize, Is.InRange(target, original));
            yield return new WaitForSecondsRealtime(.4f);
            Assert.That(camera.orthographicSize, Is.EqualTo(target).Within(.15f));
            for (int i = 0; i < 8; i++) yield return MouseEvent(World, 0, 1200);
            Assert.GreaterOrEqual(rig.TargetOrthographicSize, rig.MinimumSize - .001f);
            for (int i = 0; i < 8; i++) yield return MouseEvent(World, 0, -1200);
            Assert.LessOrEqual(rig.TargetOrthographicSize, rig.MaximumSize + .001f);
        }

        [UnityTest]
        public IEnumerator RightDragAndKeyboardRespectGroundBoundsAndUIOwnership()
        {
            Vector3 origin = rig.TargetFocusPoint;
            yield return MouseEvent(Panel, 2);
            yield return MouseEvent(Panel + new Vector2(100, -100), 2);
            yield return MouseEvent(World, 2);
            Assert.That(Vector3.Distance(origin, rig.TargetFocusPoint), Is.LessThan(.001f), "UI-origin drag must not move world");
            yield return MouseEvent(World);
            yield return MouseEvent(World, 2);
            yield return MouseEvent(World + new Vector2(70, 50), 2);
            Assert.Greater(Vector3.Distance(origin, rig.TargetFocusPoint), .05f);
            yield return MouseEvent(World + new Vector2(70, 50));
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W, Key.D));
            yield return new WaitForSecondsRealtime(2);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return null;
            Assert.LessOrEqual(Mathf.Abs(rig.TargetFocusPoint.x), Mathf.Min(rig.CityHalfExtent.x, rig.PanHalfExtent.x) + .001f);
            Assert.LessOrEqual(Mathf.Abs(rig.TargetFocusPoint.z), Mathf.Min(rig.CityHalfExtent.y, rig.PanHalfExtent.y) + .001f);
            Assert.That(rig.TargetFocusPoint.y, Is.EqualTo(0).Within(.001f));
        }

        [UnityTest]
        public IEnumerator LeftClickSelectsButUIAndDragDoNotSelect()
        {
            CityBuilding clinic = System.Array.Find(Object.FindObjectsByType<CityBuilding>(FindObjectsSortMode.None), b => b.SimulationId=="clinic");
            Vector2 point = camera.WorldToScreenPoint(clinic.GetComponent<Collider>().bounds.center);
            Assert.IsFalse(ui.IsPointerOverPanel(point));
            yield return MouseEvent(point, 1); yield return MouseEvent(point);
            Assert.AreEqual(clinic, ui.SelectedBuilding);
            yield return MouseEvent(Panel, 1); yield return MouseEvent(Panel);
            Assert.AreEqual(clinic, ui.SelectedBuilding, "UI click must not clear/select world");
            ui.Select(null);
            Vector3 beforeDrag = rig.TargetFocusPoint;
            yield return MouseEvent(point, 1);
            yield return MouseEvent(point + new Vector2(70, 0), 1);
            Assert.Greater(Vector3.Distance(beforeDrag, rig.TargetFocusPoint), .02f, "Trackpad primary drag pans the world");
            yield return MouseEvent(point, 1); yield return MouseEvent(point);
            Assert.IsNull(ui.SelectedBuilding, "Drag out and return is not a click");
        }

        [UnityTest]
        public IEnumerator KeyboardZoomAndHomeRestoreInitialView()
        {
            float initial = rig.TargetOrthographicSize;
            Vector3 initialFocus = rig.TargetFocusPoint;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Equals, Key.D));
            yield return new WaitForSecondsRealtime(.3f);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return null;
            Assert.Less(rig.TargetOrthographicSize, initial);
            Assert.Greater(Vector3.Distance(initialFocus, rig.TargetFocusPoint), .01f);
            float zoomed = rig.TargetOrthographicSize;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Minus));
            yield return new WaitForSecondsRealtime(.15f);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return null;
            Assert.Greater(rig.TargetOrthographicSize, zoomed);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Home)); yield return null; yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return null;
            Assert.That(rig.TargetOrthographicSize, Is.EqualTo(initial).Within(.001f));
            Assert.That(Vector3.Distance(initialFocus, rig.TargetFocusPoint), Is.LessThan(.001f));
        }

        [UnityTest]
        public IEnumerator TouchPanPinchAndTransitionsDoNotJumpOrSelect()
        {
            Vector3 origin = rig.TargetFocusPoint;
            yield return TouchEvent(1, UnityEngine.InputSystem.TouchPhase.Began, World);
            yield return TouchEvent(1, UnityEngine.InputSystem.TouchPhase.Moved, World + new Vector2(60, 0));
            Assert.Greater(Vector3.Distance(origin, rig.TargetFocusPoint), .03f);
            yield return TouchEvent(1, UnityEngine.InputSystem.TouchPhase.Ended, World + new Vector2(60, 0));
            Assert.IsNull(ui.SelectedBuilding);
            rig.ResetView();
            float before = rig.TargetOrthographicSize;
            Vector2 p = World;
            yield return TouchEvent(2, UnityEngine.InputSystem.TouchPhase.Began, p - new Vector2(80, 0));
            yield return TouchEvent(3, UnityEngine.InputSystem.TouchPhase.Began, p + new Vector2(80, 0));
            Assert.That(rig.TargetOrthographicSize, Is.EqualTo(before).Within(.001f), "Adding finger must not zoom");
            yield return TouchEvent(3, UnityEngine.InputSystem.TouchPhase.Moved, p + new Vector2(160, 0));
            Assert.Less(rig.TargetOrthographicSize, before);
            float pinchSize = rig.TargetOrthographicSize;
            yield return TouchEvent(3, UnityEngine.InputSystem.TouchPhase.Ended, p + new Vector2(160, 0));
            Assert.That(rig.TargetOrthographicSize, Is.EqualTo(pinchSize).Within(.001f));
            yield return TouchEvent(2, UnityEngine.InputSystem.TouchPhase.Ended, p - new Vector2(80, 0));
            Assert.IsNull(ui.SelectedBuilding, "Pinch release is not a building tap");
            origin = rig.TargetFocusPoint;
            yield return TouchEvent(4, UnityEngine.InputSystem.TouchPhase.Began, Panel);
            yield return TouchEvent(4, UnityEngine.InputSystem.TouchPhase.Moved, World);
            yield return TouchEvent(4, UnityEngine.InputSystem.TouchPhase.Ended, World);
            Assert.That(Vector3.Distance(origin, rig.TargetFocusPoint), Is.LessThan(.001f));
        }
    }
}

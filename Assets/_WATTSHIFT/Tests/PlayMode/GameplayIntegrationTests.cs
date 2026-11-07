using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Wattshift.Tests
{
    public sealed class GameplayIntegrationTests
    {
        GameSession session;
        GameplayHud hud;
        CityOverlay overlay;
        Mouse mouse;
        static string Evidence
        {
            get
            {
                string[] args = System.Environment.GetCommandLineArgs();
                int index = System.Array.IndexOf(args, "-wattshiftEvidenceOutput");
                string directory = index >= 0 && index + 1 < args.Length ? args[index + 1]
                    : Path.GetFullPath(Path.Combine(Application.dataPath, "../docs/evidence/g2"));
                return directory + Path.DirectorySeparatorChar;
            }
        }

        [UnitySetUp]
        public IEnumerator Setup()
        {
            CameraControlsTests.SetViewport();
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            SceneManager.LoadScene("CitySlice");
            yield return null; yield return null;
            session = GameSession.Instance; hud = GameplayHud.Instance; overlay = CityOverlay.Instance;
            Assert.IsNotNull(session); Assert.IsTrue(session.Ready); Assert.IsNotNull(hud);
            mouse = InputSystem.AddDevice<Mouse>("G2 UI mouse");
            session.ResetMission();
            yield return new WaitForSecondsRealtime(.5f);
        }
        [UnityTearDown] public IEnumerator Teardown() { InputSystem.RemoveDevice(mouse); yield return null; }

        IEnumerator Click(Button button)
        {
            Assert.IsNotNull(button); Assert.IsTrue(button.enabledInHierarchy);
            if (button.GetFirstAncestorOfType<ScrollView>() is ScrollView scroll) scroll.ScrollTo(button);
            yield return null; yield return null;
            Vector2 center = button.worldBound.center;
            float scale = Screen.width / button.panel.visualTree.worldBound.width;
            Vector2 p = new Vector2(center.x * scale, Screen.height - center.y * scale);
            Assert.IsTrue(overlay.IsPointerOverPanel(p), "Gameplay UI must reject world input");
            InputSystem.QueueStateEvent(mouse, new MouseState { position = p, buttons = 1 });
            yield return new WaitForSecondsRealtime(.08f);
            InputSystem.QueueStateEvent(mouse, new MouseState { position = p });
            yield return new WaitForSecondsRealtime(.2f);
        }

        IEnumerator Capture(string name)
        {
            Directory.CreateDirectory(Evidence);
            string path = Evidence + name;
            if (File.Exists(path)) File.Delete(path);
            yield return new WaitForSecondsRealtime(.5f);
            ScreenCapture.CaptureScreenshot(path);
            yield return new WaitForSecondsRealtime(1);
            Assert.IsTrue(File.Exists(path), "Actual Unity Game view screenshot must be written");
        }

        [UnityTest]
        public IEnumerator FirstMissionCanBeWonThroughActualUIAndRestoredAfterSceneReload()
        {
            Assert.That(session.Snapshot.BaselineEnergyKWh, Is.EqualTo(152.52).Within(.0001));
            Assert.That(session.Snapshot.CurrentPowerKW, Is.EqualTo(26.02).Within(.0001));
            Assert.AreEqual(650, session.Snapshot.Budget);
            CityBuilding school = Object.FindObjectsByType<CityBuilding>(FindObjectsSortMode.None).Single(b => b.SimulationId == "school");
            overlay.Select(school); yield return null;
            var windows = school.GetComponent<BuildingEnergyView>().WindowRenderer;
            Assert.IsTrue(windows.enabled);
            yield return Capture("01-mission-school-before.png");
            yield return Click(hud.CompleteButton);
            Assert.IsFalse(session.Snapshot.Completed); Assert.AreEqual(650, session.Snapshot.Budget);
            Assert.IsNotEmpty(session.LastMessage);
            yield return Click(hud.UpgradeRoot.Q<Button>("upgrade-school-lights"));
            Assert.IsTrue(session.Simulation.IsPurchased("school", "school-lights"));
            Assert.AreEqual(470, session.Snapshot.Budget);
            Assert.That(session.Snapshot.CurrentEnergyKWh, Is.EqualTo(132.52).Within(.0001));
            Assert.IsFalse(windows.enabled, "After-hours schedule must switch real source window feedback off");
            Assert.IsFalse(hud.UpgradeRoot.Q<Button>("upgrade-school-lights").enabledInHierarchy);
            yield return Click(hud.UpgradeRoot.Q<Button>("upgrade-school-computers"));
            Assert.That(session.Snapshot.CurrentEnergyKWh, Is.EqualTo(102.52).Within(.0001));
            Assert.That(session.Snapshot.SavedEnergyKWh, Is.EqualTo(50).Within(.0001));
            Assert.That(session.Snapshot.CurrentPowerKW, Is.EqualTo(16.02).Within(.0001));
            Assert.AreEqual(210, session.Snapshot.Budget); Assert.AreEqual(100, session.Snapshot.Comfort);
            Assert.IsTrue(hud.Root.Q<Label>("energy-totals").text.Contains("102"));
            yield return Click(hud.SaveButton);
            yield return Click(hud.CompleteButton);
            Assert.IsTrue(session.Snapshot.Completed);
            yield return Capture("02-mission-complete.png");
            SceneManager.LoadScene("CitySlice"); yield return null; yield return null;
            session = GameSession.Instance; hud = GameplayHud.Instance; overlay = CityOverlay.Instance;
            Assert.IsTrue(session.Snapshot.Completed, "Auto-load must restore completed mission after scene reload");
            Assert.AreEqual(210, session.Snapshot.Budget);
            Assert.That(session.Snapshot.SavedEnergyKWh, Is.EqualTo(50).Within(.0001));
            CameraControlsTests.SetViewport(900, 1200);
            yield return new WaitForSecondsRealtime(.6f);
            school = Object.FindObjectsByType<CityBuilding>(FindObjectsSortMode.None).Single(b => b.SimulationId == "school");
            overlay.Select(school);
            yield return Capture("03-mission-portrait.png");
            CameraControlsTests.SetViewport();
        }

        [UnityTest]
        public IEnumerator RejectedActionsPreserveActualCityAndShowReasons()
        {
            string baseline = JsonUtility.ToJson(session.Simulation.ExportState());
            foreach (var item in new[] { new[] { "clinic", "clinic-off" }, new[] { "school", "school-dark" }, new[] { "school", "school-expensive" } })
            {
                session.Purchase(item[0], item[1]);
                Assert.AreEqual(baseline, JsonUtility.ToJson(session.Simulation.ExportState()));
                Assert.IsNotEmpty(session.LastMessage);
                yield return null;
                Assert.AreEqual(session.LastMessage, hud.Root.Q<Label>("action-message").text);
            }
            session.Purchase("school", "school-lights");
            string after = JsonUtility.ToJson(session.Simulation.ExportState());
            session.Purchase("school", "school-lights");
            Assert.AreEqual(after, JsonUtility.ToJson(session.Simulation.ExportState()));
            Assert.AreEqual(470, session.Snapshot.Budget);
            Assert.IsTrue(session.Snapshot.SafetySatisfied);
            Assert.AreEqual(100, session.Snapshot.Comfort);
            // A service-only mutation deliberately differs from the saved session state.
            Assert.IsTrue(session.Simulation.TryPurchase("school", "school-computers").Success);
            Assert.AreEqual(210, session.Snapshot.Budget);
            yield return Click(hud.LoadButton);
            Assert.AreEqual(470, session.Snapshot.Budget);
            Assert.IsFalse(session.Simulation.IsPurchased("school", "school-computers"));
            yield return Click(hud.ResetButton);
            Assert.AreEqual(650, session.Snapshot.Budget);
            Assert.IsFalse(session.Simulation.IsPurchased("school", "school-lights"));
        }

        [UnityTest]
        public IEnumerator MissionAndUpgradePanelsRemainUsableAcrossWideFourThreeAndPortraitViews()
        {
            CityBuilding school = Object.FindObjectsByType<CityBuilding>(FindObjectsSortMode.None).Single(b => b.SimulationId == "school");
            overlay.Select(school);
            foreach (Vector2Int size in new[] { new Vector2Int(2560, 1080), new Vector2Int(1200, 900), new Vector2Int(900, 1200) })
            {
                CameraControlsTests.SetViewport(size.x, size.y);
                yield return new WaitForSecondsRealtime(.4f);
                Rect root = hud.Root.panel.visualTree.worldBound;
                Rect mission = hud.Root.Q("mission-card").worldBound;
                Rect context = overlay.ContextRoot.worldBound;
                Assert.Greater(mission.width, 100); Assert.Greater(context.height, 100);
                Assert.GreaterOrEqual(mission.yMin, root.yMin);
                Assert.LessOrEqual(mission.yMax, root.yMax + 1);
                Assert.LessOrEqual(context.yMax, root.yMax + 1);
                Assert.IsFalse(mission.Overlaps(context), "Mission and building panels overlap at " + size);
                Assert.IsTrue(hud.CompleteButton.enabledInHierarchy);
                Assert.GreaterOrEqual(hud.CompleteButton.worldBound.height * Screen.width / root.width, 43);
                if (size.x == 1200) yield return Capture("04-mission-4x3.png");
            }
            CameraControlsTests.SetViewport();
        }
    }
}

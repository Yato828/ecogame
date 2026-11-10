using System;
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
    public sealed class G5MenuUxTests
    {
        GameSession session;
        GameplayHud hud;
        CityOverlay overlay;
        Mouse mouse;
        Keyboard keyboard;
        string saves;
        readonly System.Collections.Generic.List<InputDevice> hardware = new System.Collections.Generic.List<InputDevice>();

        [UnitySetUp] public IEnumerator Setup()
        {
            CameraControlsTests.SetViewport();
            UnityEditor.ShaderUtil.allowAsyncCompilation = false;
            saves = Path.Combine(Path.GetTempPath(), "WattshiftG5UI-" + Guid.NewGuid().ToString("N"));
            GameSession.EditorTestSaveDirectory = saves;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            GameSession.EditorLegacyGameplay = true;
            SceneManager.LoadScene("CitySlice"); yield return null; yield return null;
            session = GameSession.Instance; session.enabled = false;
            hud = GameplayHud.Instance; overlay = CityOverlay.Instance;
            mouse = InputSystem.AddDevice<Mouse>("G5 menu verification");
            keyboard = InputSystem.AddDevice<Keyboard>("G5 menu keyboard");
            foreach (var device in InputSystem.devices.ToArray())
                if (device != mouse && device != keyboard && device.enabled) { hardware.Add(device); InputSystem.DisableDevice(device); }
            mouse.MakeCurrent(); keyboard.MakeCurrent(); yield return new WaitForSecondsRealtime(.25f);
        }

        [UnityTearDown] public IEnumerator Teardown()
        {
            foreach (var device in hardware) if (device.added) InputSystem.EnableDevice(device);
            hardware.Clear();
            if (mouse != null) InputSystem.RemoveDevice(mouse);
            if (keyboard != null) InputSystem.RemoveDevice(keyboard);
            GameSession.EditorTestSaveDirectory = null; GameSession.EditorLegacyGameplay = false; CameraControlsTests.SetViewport();
            if (Directory.Exists(saves)) Directory.Delete(saves, true);
            yield return null;
        }

        IEnumerator Click(Button button)
        {
            Assert.IsNotNull(button);Assert.IsTrue(button.enabledInHierarchy,button.name);mouse.MakeCurrent();
            yield return new WaitForSecondsRealtime(.22f);
            var scroll=button.GetFirstAncestorOfType<ScrollView>();scroll?.ScrollTo(button);yield return null;yield return null;
            scroll?.ScrollTo(button);yield return null;yield return null;
            float scale=Screen.width/button.panel.visualTree.worldBound.width; Vector2 point=default;
            for(int i=0;i<2;i++){point=new Vector2(button.worldBound.center.x*scale,Screen.height-button.worldBound.center.y*scale);InputSystem.QueueStateEvent(mouse,new MouseState{position=point});yield return null;}
            point=new Vector2(button.worldBound.center.x*scale,Screen.height-button.worldBound.center.y*scale);
            Assert.Greater(button.worldBound.width,0,button.name+" must be visible");
            var picked=button.panel.Pick(button.worldBound.center);
            Assert.IsTrue(picked==button||button.Contains(picked),button.name+" center picked "+picked?.name+" bounds="+button.worldBound);
            InputSystem.QueueStateEvent(mouse,new MouseState{position=point,buttons=1});yield return new WaitForSecondsRealtime(.08f);
            InputSystem.QueueStateEvent(mouse,new MouseState{position=point});yield return new WaitForSecondsRealtime(.15f);
        }
        IEnumerator Escape()
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Escape)); yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return null;
        }

        IEnumerator Capture(string name)
        {
            string directory = Path.GetFullPath(Path.Combine(Application.dataPath, "../docs/evidence/g5"));
            Directory.CreateDirectory(directory); string path = Path.Combine(directory, name + ".png");
            if (File.Exists(path)) File.Delete(path);
            yield return new WaitForSecondsRealtime(.2f); ScreenCapture.CaptureScreenshot(path);
            yield return new WaitForSecondsRealtime(.4f); Assert.IsTrue(File.Exists(path), path);
        }

        [UnityTest] public IEnumerator HelpHasAccessibleBackAndBlocksBackgroundFromMenuAndPlay()
        {
            yield return Click(hud.Root.Q<Button>("help-menu"));
            Assert.IsTrue(hud.IsHelpOpen); Assert.IsTrue(hud.HasBlockingModal);
            Assert.IsFalse(hud.Root.Q<Button>("start-mission-0").enabledInHierarchy);
            yield return Click(hud.Root.Q<Button>("help-close")); Assert.IsFalse(hud.IsHelpOpen);
            yield return Click(hud.Root.Q<Button>("start-mission-0")); session.SkipTutorial();
            yield return Click(hud.Root.Q<Button>("mission-details"));
            yield return Click(hud.Root.Q<Button>("help-playing"));
            Assert.IsTrue(session.Paused); Assert.IsTrue(hud.HasBlockingModal);
            Assert.IsFalse(hud.Root.Q<Button>("complete-mission").enabledInHierarchy);
            var selected = overlay.SelectedBuilding;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Tab)); yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return null;
            Assert.AreEqual(selected, overlay.SelectedBuilding);
            Assert.IsTrue(hud.IsMarkerObstructed(new Rect(0, 0, 40, 40)));
            yield return Escape(); Assert.IsFalse(hud.IsHelpOpen); Assert.IsTrue(session.Paused);
        }

        [UnityTest] public IEnumerator DemoUsesRealDecisionsAndReturnsToUnchangedCampaignSave()
        {
            session.BeginMission(0); session.OpenProblem("school");
            session.Purchase("school", "school-lights"); session.Purchase("school", "school-computers");
            session.FinishShiftEarly(); session.ReturnToMenu(); session.Save();
            string path = session.CurrentSavePath;
            byte[] original = File.ReadAllBytes(path); int stars = session.TotalStars; int score = session.GetBestScore(0);
            yield return Click(hud.Root.Q<Button>("demo-menu"));
            Assert.IsTrue(session.IsDemo); Assert.AreEqual(GamePhase.Playing, session.Phase);
            Assert.AreEqual(0, session.TotalStars); Assert.AreEqual(0, session.Rating.ResolvedProblems);
            Assert.AreEqual(DisplayStyle.Flex, hud.Root.Q<Label>("demo-banner").resolvedStyle.display);
            session.OpenProblem("school"); hud.ShowSolutions(); yield return null;
            yield return Click(hud.UpgradeRoot.Q<Button>("upgrade-school-lights"));
            Assert.AreEqual(1, session.Rating.ResolvedProblems);
            yield return Click(hud.Root.Q<Button>("complete-mission"));
            Assert.Greater(session.Score, 0); Assert.AreEqual(1, session.Stars);
            CollectionAssert.AreEqual(original, File.ReadAllBytes(path));
            yield return Click(hud.Root.Q<Button>("return-menu-result"));
            Assert.IsFalse(session.IsDemo); Assert.AreEqual(GamePhase.Menu, session.Phase);
            Assert.AreEqual(stars, session.TotalStars); Assert.AreEqual(score, session.GetBestScore(0));
            CollectionAssert.AreEqual(original, File.ReadAllBytes(path));
        }

        [UnityTest] public IEnumerator PhoneScalesAndOrientationKeepControlsInsideScrollableCards()
        {
            session.BeginMission(0); session.SkipTutorial();
            foreach (var size in new[] { new Vector2Int(390, 844), new Vector2Int(844, 390) })
            {
                CameraControlsTests.SetViewport(size.x, size.y); yield return new WaitForSecondsRealtime(.2f);
                foreach (int percent in new[] { 90, 100, 115, 130 })
                {
                    UserExperienceSettings.ScalePercent = percent; overlay.Select(null); yield return null; yield return null;
                    var card = hud.Root.Q("mission-card"); float factor = Screen.width / hud.Root.panel.visualTree.worldBound.width;
                    Assert.That(card.worldBound.xMin * factor, Is.GreaterThanOrEqualTo(0));
                    Assert.That(card.worldBound.xMax * factor, Is.LessThanOrEqualTo(Screen.width + 1));
                    foreach (string id in new[] { "scanner-toggle", "pause-toggle", "complete-mission", "speed-1", "speed-2", "speed-4", "camera-zoom-in", "camera-zoom-out" })
                    {
                        var button = hud.Root.Q<Button>(id);
                        Assert.GreaterOrEqual(button.worldBound.height * factor, 43.5f, id);
                        Assert.That(button.worldBound.xMin, Is.GreaterThanOrEqualTo(card.worldBound.xMin), id);
                        Assert.That(button.worldBound.xMax, Is.LessThanOrEqualTo(card.worldBound.xMax + 1), id);
                    }
                    if (percent == 130) yield return Capture(size.x == 390 ? "ui-phone-upperbar-130" : "ui-landscape-upperbar-130");
                    session.OpenProblem("school"); hud.ShowSolutions(); yield return new WaitForSecondsRealtime(.2f);
                    Rect bounds = overlay.ContextScreenBounds;
                    Assert.That(bounds.xMin, Is.GreaterThanOrEqualTo(-1)); Assert.That(bounds.xMax, Is.LessThanOrEqualTo(Screen.width + 1));
                    Assert.That(bounds.yMin, Is.GreaterThanOrEqualTo(-1)); Assert.That(bounds.yMax, Is.LessThanOrEqualTo(Screen.height + 1));
                    Assert.Greater(hud.UpgradeRoot.worldBound.height, 45);
                    yield return Click(hud.UpgradeRoot.Q<Button>("upgrade-school-led"));
                    Assert.IsTrue(session.Simulation.IsPurchased("school", "school-led"));
                    if (percent == 130) yield return Capture(size.x == 390 ? "ui-phone-solutions-130" : "ui-landscape-solutions-130");
                    session.ResetMission(); session.SkipTutorial();
                }
            }
        }
    }
}

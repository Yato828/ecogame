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
    public sealed class CityTaskPlayTests
    {
        GameSession session; GameplayHud hud; CityOverlay overlay; Mouse mouse; string saves;
        readonly System.Collections.Generic.List<InputDevice> hardware = new System.Collections.Generic.List<InputDevice>();
        [UnitySetUp] public IEnumerator Setup()
        {
            GameSession.EditorLegacyGameplay = false;
            CameraControlsTests.SetViewport(); UnityEditor.ShaderUtil.allowAsyncCompilation = false;
            saves = Path.Combine(Path.GetTempPath(), "WattshiftRescue-" + Guid.NewGuid().ToString("N"));
            GameSession.EditorTestSaveDirectory = saves;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            SceneManager.LoadScene("CitySlice"); yield return null; yield return null;
            session = GameSession.Instance; session.enabled = false; hud = GameplayHud.Instance; overlay = CityOverlay.Instance;
            mouse = InputSystem.AddDevice<Mouse>("City tasks verification");
            foreach (var device in InputSystem.devices.ToArray()) if (device != mouse && device.enabled) { hardware.Add(device); InputSystem.DisableDevice(device); }
            mouse.MakeCurrent(); yield return new WaitForSecondsRealtime(.3f);
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            foreach (var device in hardware) if (device.added) InputSystem.EnableDevice(device);
            hardware.Clear(); if (mouse != null) InputSystem.RemoveDevice(mouse);
            GameSession.EditorTestSaveDirectory = null; GameSession.EditorLegacyGameplay = false;
            CameraControlsTests.SetViewport(); if (Directory.Exists(saves)) Directory.Delete(saves, true); yield return null;
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
        IEnumerator Capture(string name)
        {
            string directory = Path.GetFullPath(Path.Combine(Application.dataPath, "../docs/evidence/rescue"));
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, name + ".png");
            yield return new WaitForSecondsRealtime(.2f); ScreenCapture.CaptureScreenshot(path);
            yield return new WaitForSecondsRealtime(.4f); Assert.IsTrue(File.Exists(path));
        }
        [UnityTest] public IEnumerator FirstSchoolChoiceExplainsMistakeAppliesRealUpgradeAndRestoresProgress()
        {
            Assert.IsTrue(session.AdventureMode); Assert.IsTrue(session.TaskWelcome);
            yield return Capture("welcome");
            yield return Click(hud.Root.Q<Button>("task-start"));
            Assert.AreEqual("school-light", session.ActiveTask.Id); Assert.IsTrue(session.Paused);
            Assert.IsFalse(hud.Root.Q("mission-scroll").resolvedStyle.display == DisplayStyle.Flex);
            session.FocusTask(); yield return null;
            Assert.AreEqual("school", overlay.SelectedBuilding.SimulationId);
            yield return Click(hud.UpgradeRoot.Q<Button>("task-choice-1"));
            Assert.IsTrue(session.LastActionFailed); Assert.IsFalse(session.TaskSolved);
            Assert.IsNotEmpty(session.TaskFeedback); Assert.Contains(session.ActiveTask.Id, session.Tasks.Errors);
            yield return Click(hud.UpgradeRoot.Q<Button>("task-choice-0"));
            Assert.IsTrue(session.TaskSolved); Assert.IsTrue(session.Tasks.TutorialDone);
            Assert.IsTrue(session.Simulation.IsPurchased("school", "school-lights"));
            Assert.IsTrue(session.Snapshot.SafetySatisfied);
            Assert.AreEqual(ProblemMarkerState.Resolved, ProblemMarkers.Instance.GetMarkerState("school"));
            var windows = UnityEngine.Object.FindObjectsByType<BuildingEnergyView>(FindObjectsSortMode.None).First(v => v.BuildingId == "school");
            Assert.IsFalse(windows.WindowRenderer.enabled, "Existing source window effect follows actual scheduled-off light");
            yield return Capture("school-reward");
            session.ChooseTask(0); Assert.AreEqual(1, session.Tasks.Completed.Length);
            yield return Click(hud.UpgradeRoot.Q<Button>("task-next"));
            Assert.AreEqual("school-pc", session.ActiveTask.Id); Assert.IsTrue(session.FindingSource);
            double hour = session.GameHour; session.Tick(1000); Assert.AreEqual(hour, session.GameHour);
            SceneManager.LoadScene("CitySlice"); yield return null; yield return null;
            session = GameSession.Instance; session.enabled = false;
            Assert.AreEqual(1, session.Tasks.Completed.Length); Assert.IsTrue(session.Tasks.TutorialDone);
            Assert.Contains("school-light", session.Tasks.Errors);
            session.BeginAdventure(); Assert.AreEqual("school-pc", session.ActiveTask.Id);
            Assert.IsTrue(session.Simulation.IsPurchased("school", "school-lights"));
        }
        [UnityTest] public IEnumerator AllChaptersAndFinalUseOneTemplateAndKeepCampaignDuringTutorialReplay()
        {
            session.BeginAdventure(true);
            foreach (var task in session.TaskCampaign.Tasks)
            {
                Assert.AreEqual(task.Id, session.ActiveTask.Id);
                if (session.FindingSource)
                {
                    session.ChooseTask((task.SourceBest + 1) % 3); Assert.IsTrue(session.FindingSource);
                    session.ChooseTask(task.SourceBest); Assert.IsFalse(session.FindingSource);
                }
                session.ChooseTask((task.Best + 1) % 3); Assert.IsFalse(session.TaskSolved);
                session.ChooseTask(task.Best); Assert.IsTrue(session.TaskSolved, task.Id + session.TaskFeedback);
                Assert.IsTrue(session.Snapshot.SafetySatisfied, task.Id);
                session.NextTask(); yield return null;
            }
            Assert.AreEqual(GamePhase.Results, session.Phase); Assert.AreEqual(17, session.Tasks.Completed.Length);
            Assert.AreEqual(4, session.TaskStars); Assert.IsNotEmpty(session.TaskTips);
            foreach (var task in session.TaskCampaign.Tasks.Where(t => !string.IsNullOrEmpty(t.UpgradeId)))
                Assert.IsTrue(session.Simulation.IsPurchased(task.BuildingId, task.UpgradeId), task.Id);
            yield return Capture("campaign-complete");
            byte[] original = File.ReadAllBytes(session.CurrentSavePath);
            session.RepeatTaskTutorial(); Assert.AreEqual(0, session.Tasks.Completed.Length);
            session.ChooseTask(session.ActiveTask.Best); session.NextTask();
            Assert.AreEqual(GamePhase.Menu, session.Phase); Assert.AreEqual(17, session.Tasks.Completed.Length);
            // A replay restores the exact ordinary state and writes it through the same serializer.
            CollectionAssert.AreEqual(original, File.ReadAllBytes(session.CurrentSavePath));
        }
        [UnityTest] public IEnumerator PhoneOrientationsAndFourScalesKeepChoicesReachable()
        {
            session.BeginAdventure(true);
            foreach (var size in new[] { new Vector2Int(390,844), new Vector2Int(844,390) })
            foreach (int percent in new[] {90,100,115,130})
            {
                CameraControlsTests.SetViewport(size.x,size.y); UserExperienceSettings.ScalePercent=percent;
                session.FocusTask(); yield return new WaitForSecondsRealtime(.3f);
                var choice = hud.UpgradeRoot.Q<Button>("task-choice-0"); Assert.IsNotNull(choice);
                var scroll = choice.GetFirstAncestorOfType<ScrollView>(); scroll.ScrollTo(choice); yield return null; yield return null;
                Rect bounds = overlay.ContextScreenBounds;
                Assert.That(bounds.xMin,Is.GreaterThanOrEqualTo(-1)); Assert.That(bounds.xMax,Is.LessThanOrEqualTo(Screen.width+1));
                Assert.That(bounds.yMin,Is.GreaterThanOrEqualTo(-1)); Assert.That(bounds.yMax,Is.LessThanOrEqualTo(Screen.height+1));
                float factor = Screen.width / choice.panel.visualTree.worldBound.width;
                Assert.GreaterOrEqual(choice.worldBound.height * factor,43.99f);
                if (percent==130) yield return Capture(size.x==390 ? "phone-task-130" : "landscape-task-130");
            }
            yield return Click(hud.UpgradeRoot.Q<Button>("task-choice-0")); Assert.IsTrue(session.TaskSolved);
        }
    }
}

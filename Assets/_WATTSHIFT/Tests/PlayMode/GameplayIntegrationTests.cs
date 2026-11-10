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
using Wattshift.Presentation;

namespace Wattshift.Tests
{
    public sealed class GameplayIntegrationTests
    {
        GameSession session; GameplayHud hud; CityOverlay overlay; Mouse mouse; string saves;
        readonly System.Collections.Generic.List<InputDevice> isolatedHardware = new System.Collections.Generic.List<InputDevice>();
        static string Evidence => Path.GetFullPath(Path.Combine(Application.dataPath,"../docs/evidence/g4"));
        [UnitySetUp] public IEnumerator Setup()
        {
            CameraControlsTests.SetViewport(); UnityEditor.ShaderUtil.allowAsyncCompilation=false;
            saves=Path.Combine(Path.GetTempPath(),"WattshiftG4-"+Guid.NewGuid().ToString("N"));GameSession.EditorTestSaveDirectory=saves;
            InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            GameSession.EditorLegacyGameplay = true;
            SceneManager.LoadScene("CitySlice");yield return null;yield return null;
            session=GameSession.Instance;session.enabled=false;hud=GameplayHud.Instance;overlay=CityOverlay.Instance;
            mouse=InputSystem.AddDevice<Mouse>("G4 UX verification");
            foreach(var device in InputSystem.devices.ToArray())if(device!=mouse && device.enabled){isolatedHardware.Add(device);InputSystem.DisableDevice(device);}
            mouse.MakeCurrent();yield return new WaitForSecondsRealtime(.3f);
            Assert.IsTrue(session.Mission.UseDecisionRating); Assert.IsNotNull(ProblemMarkers.Instance);
        }
        [UnityTearDown] public IEnumerator Teardown()
        {
            foreach(var device in isolatedHardware)if(device.added)InputSystem.EnableDevice(device);isolatedHardware.Clear();
            if(mouse!=null)InputSystem.RemoveDevice(mouse);GameSession.EditorTestSaveDirectory=null;GameSession.EditorLegacyGameplay=false;
            CameraControlsTests.SetViewport();if(Directory.Exists(saves))Directory.Delete(saves,true);yield return null;
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
            Directory.CreateDirectory(Evidence);string path=Path.Combine(Evidence,name+".png");
            if(File.Exists(path))File.Delete(path);yield return new WaitForSecondsRealtime(.4f);ScreenCapture.CaptureScreenshot(path);
            yield return new WaitForSecondsRealtime(.4f);Assert.IsTrue(File.Exists(path));
        }
        void FullSchool()
        {
            session.BeginMission(0);session.OpenProblem("school");session.Purchase("school","school-lights");session.Purchase("school","school-computers");session.FinishShiftEarly();Assert.IsTrue(session.Success);Assert.AreEqual(3,session.Stars);
        }
        [UnityTest] public IEnumerator NewPlayerFollowsMarkerAndEarnsPartialStarWithoutWaiting()
        {
            yield return Capture("01-menu");yield return Click(hud.Root.Q<Button>("start-mission-0"));
            Assert.IsTrue(session.TutorialActive);Assert.IsFalse(session.ScannerEnabled);Assert.That(session.GameHour,Is.EqualTo(17));
            yield return new WaitForSecondsRealtime(.3f);
            Assert.AreEqual(ProblemMarkerState.Problem,ProblemMarkers.Instance.GetMarkerState("school"));
            yield return Capture("02-visible-problem-marker");
            yield return Click(ProblemMarkers.Instance.Root.Q<Button>("problem-marker-school"));
            Assert.IsTrue(session.IsInvestigated("school"));Assert.AreEqual("school",overlay.SelectedBuilding.SimulationId);
            float compactWidth=overlay.ContextRoot.worldBound.width;
            yield return Click(overlay.ContextRoot.Q<Button>("context-details"));
            Assert.IsTrue(overlay.ContextExpanded);Assert.Greater(overlay.ContextRoot.worldBound.width,compactWidth+80);
            yield return Capture("03-expanded-school");
            yield return Click(hud.UpgradeRoot.Q<Button>("upgrade-school-lights"));
            Assert.AreEqual(1,session.Rating.ResolvedProblems);Assert.AreEqual(ProblemMarkerState.Problem,ProblemMarkers.Instance.GetMarkerState("school"));
            session.SetScanner(true);Assert.AreEqual(ScannerHighlightState.Problem,overlay.SelectedBuilding.GetComponent<SourceBuildingHighlight>().ScannerState);
            yield return Click(hud.Root.Q<Button>("complete-mission"));
            Assert.IsTrue(session.Success);Assert.AreEqual(1,session.Stars);Assert.IsTrue(session.IsMissionUnlocked(1));
            Assert.That(session.Score,Is.InRange(25,100));Assert.AreEqual(1,session.Rating.ResolvedProblems);Assert.AreEqual(2,session.Rating.TotalProblems);
            Assert.Greater(session.Rating.EnergySavedKWh,0);Assert.IsFalse(session.TutorialActive);
            yield return Capture("04-partial-result");
            var breakdown=hud.Root.Q<Foldout>("rating-breakdown");Assert.IsNotNull(breakdown);breakdown.value=true;yield return null;
            yield return Capture("04-partial-result-breakdown");
            yield return Click(hud.Root.Q<Button>("next-mission"));Assert.AreEqual(1,session.MissionIndex);Assert.AreEqual(GamePhase.Playing,session.Phase);
        }
        [UnityTest] public IEnumerator FullRatingAndReplayKeepIndependentRecordsAndTutorialPreference()
        {
            session.BeginMission(0);session.OpenProblem("school");hud.ShowSolutions();yield return null;
            yield return Click(hud.UpgradeRoot.Q<Button>("upgrade-school-led"));Assert.IsTrue(session.Simulation.IsPurchased("school","school-led"),session.LastMessage);
            yield return Click(hud.UpgradeRoot.Q<Button>("upgrade-school-computers"));Assert.IsTrue(session.Simulation.IsPurchased("school","school-computers"),session.LastMessage);
            Assert.AreEqual(ProblemMarkerState.Resolved,ProblemMarkers.Instance.GetMarkerState("school"));
            session.SetScanner(true);Assert.AreEqual(ScannerHighlightState.Resolved,overlay.SelectedBuilding.GetComponent<SourceBuildingHighlight>().ScannerState);
            yield return Capture("05-resolved-marker");session.FinishShiftEarly();Assert.AreEqual(3,session.Stars);Assert.That(session.Score,Is.InRange(85,100));
            var rating=session.Rating;Assert.That(rating.ProblemPoints+rating.EnergyPoints+rating.SafetyPoints+rating.BudgetPoints,Is.EqualTo(session.Score).Within(.51));
            yield return Capture("06-three-star-result");int best=session.GetBestScore(0);double saving=session.GetBestSavingPercent(0);int reward=session.CampaignCredits;
            session.BeginMission(0);Assert.IsFalse(session.TutorialActive);session.FinishShiftEarly();Assert.AreEqual(0,session.Stars);
            Assert.AreEqual(best,session.GetBestScore(0));Assert.AreEqual(saving,session.GetBestSavingPercent(0));Assert.AreEqual(reward,session.CampaignCredits);
            session.RepeatTutorial();Assert.IsTrue(session.TutorialActive);session.SkipTutorial();Assert.IsFalse(session.TutorialActive);
            session.Load();Assert.IsFalse(session.LastActionFailed);Assert.IsTrue(session.IsMissionUnlocked(1));Assert.AreEqual(3,session.TotalStars);
            SceneManager.LoadScene("CitySlice");yield return null;yield return null;session=GameSession.Instance;session.enabled=false;
            Assert.AreEqual(best,session.GetBestScore(0));Assert.AreEqual(3,session.GetBestStars(0));Assert.AreEqual(saving,session.GetBestSavingPercent(0));
        }
        [UnityTest] public IEnumerator ThreeMissionsAllowDifferentPartialSolutionsAndProtectCriticalSystems()
        {
            FullSchool();session.BeginMission(1);session.OpenProblem("crossings");
            Assert.AreEqual(ProblemMarkerState.Urgent,ProblemMarkers.Instance.GetMarkerState("crossings"));
            session.Purchase("crossings","crossings-off");Assert.IsTrue(session.LastActionFailed);Assert.AreEqual(380,session.Snapshot.Budget);
            session.Purchase("crossings","crossings-led");session.FinishShiftEarly();Assert.AreEqual(1,session.Stars);Assert.IsTrue(session.IsMissionUnlocked(2));
            session.BeginMission(2);session.OpenProblem("clinic");session.Purchase("clinic","clinic-off");Assert.IsTrue(session.LastActionFailed);
            Assert.AreEqual(550,session.Snapshot.Budget);session.Purchase("clinic","clinic-task-off");Assert.IsTrue(session.LastActionFailed);
            session.Purchase("clinic","clinic-aux");session.Purchase("clinic","clinic-admin");session.FinishShiftEarly();Assert.AreEqual(2,session.Stars);
            session.BeginMission(2);session.OpenProblem("clinic");session.Purchase("clinic","clinic-aux");session.Purchase("clinic","clinic-admin");session.Purchase("clinic","clinic-led");
            session.SetSpeed(4);session.Tick(60);Assert.That(session.GameHour,Is.EqualTo(21).Within(.0001));
            var clinic=session.Snapshot.Buildings.Single(b=>b.BuildingId=="clinic");Assert.That(clinic.Devices.Single(d=>d.Id=="medical").CurrentPowerKW,Is.EqualTo(8));
            Assert.IsTrue(session.Snapshot.SafetySatisfied);Assert.That(clinic.CurrentPowerKW,Is.EqualTo(10).Within(.0001));
            session.FinishShiftEarly();Assert.AreEqual(3,session.Stars);Assert.AreEqual(7,session.TotalStars);
            yield return Capture("07-clinic-results");session.Save();session.Load();Assert.AreEqual(7,session.TotalStars);
        }
        [UnityTest] public IEnumerator TimeControlsPreserveEnergyEventsAndNeverResolveUnaddressedProblems()
        {
            session.BeginMission(0);session.SetSpeed(1);session.Tick(60);double energy=session.Snapshot.AccumulatedEnergyKWh;
            string[] events=session.TriggeredEventIds;
            session.ResetMission();yield return Click(hud.Root.Q<Button>("speed-4"));session.Tick(15);
            Assert.That(session.GameHour,Is.EqualTo(18).Within(.0001));Assert.That(session.Snapshot.AccumulatedEnergyKWh,Is.EqualTo(energy).Within(.0001));CollectionAssert.AreEquivalent(events,session.TriggeredEventIds);
            session.TogglePause();session.Tick(30);Assert.That(session.GameHour,Is.EqualTo(18).Within(.0001));session.TogglePause();
            session.SetSpeed(2);session.Tick(30);Assert.That(session.GameHour,Is.EqualTo(19).Within(.0001));
            session.FinishShiftEarly();Assert.AreEqual(0,session.Stars);Assert.AreEqual(0,session.Rating.ResolvedProblems);Assert.IsFalse(session.IsMissionUnlocked(1));
            session.BeginMission(0);session.OpenProblem("school");session.Purchase("school","school-lights");session.Purchase("school","school-lights");Assert.IsTrue(session.LastActionFailed);
            session.Purchase("school","school-expensive");Assert.IsTrue(session.LastActionFailed);Assert.AreEqual(470,session.Snapshot.Budget);
        }
        [UnityTest] public IEnumerator CardsAndScaleRemainInSafeAreaAtFiveAspectRatios()
        {
            session.BeginMission(0);session.SkipTutorial();session.OpenProblem("school");
            foreach(var size in new[]{new Vector2Int(1920,1080),new Vector2Int(2560,1080),new Vector2Int(1200,900),new Vector2Int(900,1200),new Vector2Int(390,844)})
            {
                CameraControlsTests.SetViewport(size.x,size.y);yield return new WaitForSecondsRealtime(.2f);
                foreach(int scale in new[]{90,100,115,130})
                {
                    UserExperienceSettings.ScalePercent=scale;hud.ShowSolutions();yield return new WaitForSecondsRealtime(.12f);
                    Rect bounds=overlay.ContextScreenBounds;Assert.Greater(bounds.width,150);Assert.Greater(bounds.height,150);
                    Assert.That(bounds.xMin,Is.GreaterThanOrEqualTo(-2));Assert.That(bounds.xMax,Is.LessThanOrEqualTo(Screen.width+2));
                    Assert.That(bounds.yMin,Is.GreaterThanOrEqualTo(-2));Assert.That(bounds.yMax,Is.LessThanOrEqualTo(Screen.height+2));
                    var close=overlay.ContextRoot.Query<Button>().ToList().First(b=>b.text=="×");
                    float factor=Screen.width/close.panel.visualTree.worldBound.width;Assert.GreaterOrEqual(close.worldBound.height*factor,43.5f);
                    Assert.IsTrue(overlay.IsPointerOverPanel(bounds.center));
                    if(scale==130&&(size.x==390||size.x==1200))yield return Capture(size.x==390?"08-mobile-expanded-130":"09-four-three-expanded-130");
                    overlay.SetExpandedCard(false);yield return null;
                }
            }
            CameraControlsTests.SetViewport();UserExperienceSettings.ScalePercent=115;
            foreach(string id in new[]{"clinic","school","home-a","school","clinic","school"}){session.OpenProblem(id);yield return null;}
            session.Save();SceneManager.LoadScene("CitySlice");yield return null;yield return null;
            Assert.AreEqual(115,UserExperienceSettings.ScalePercent);GameSession.Instance.enabled=false;GameSession.Instance.Load();Assert.IsFalse(GameSession.Instance.LastActionFailed);
        }
        [UnityTest] public IEnumerator SettingsBlockPointerKeyboardAndPreserveQualityAndVolume()
        {
            session.BeginMission(0);session.SkipTutorial();yield return Click(hud.Root.Q<Button>("mission-details"));
            yield return Click(hud.Root.Q<Button>("settings-playing"));Assert.IsTrue(session.Paused);Assert.IsTrue(hud.HasBlockingModal);
            Assert.IsFalse(hud.Root.Q<Button>("pause-toggle").enabledInHierarchy);
            yield return Click(hud.Root.Q<Button>("quality-0"));Assert.AreEqual(0,QualityController.Instance.Tier);
            hud.Root.Q<Slider>("audio-volume").value=.35f;yield return Click(hud.Root.Q<Button>("audio-mute"));Assert.IsTrue(CampaignAudio.Instance.Muted);
            yield return Click(hud.Root.Q<Button>("ui-scale-115"));Assert.AreEqual(115,UserExperienceSettings.ScalePercent);
            yield return Click(hud.Root.Q<Button>("quality-2"));yield return Click(hud.Root.Q<Button>("settings-close"));Assert.IsTrue(session.Paused);
            session.Save();SceneManager.LoadScene("CitySlice");yield return null;yield return null;
            Assert.That(CampaignAudio.Instance.Volume,Is.EqualTo(.35f).Within(.001));Assert.IsTrue(CampaignAudio.Instance.Muted);Assert.AreEqual(2,QualityController.Instance.Tier);
            session=GameSession.Instance;session.enabled=false;session.Load();Assert.IsTrue(session.Paused);
            File.WriteAllText(Path.Combine(saves,"wattshift-campaign-v4.json"),"{broken");session.Load();Assert.IsTrue(session.LastActionFailed);
        }
        [UnityTest] public IEnumerator WorldMarkersFollowCameraAndDoNotSelectObjectsBehindUI()
        {
            session.BeginMission(0);session.SkipTutorial();yield return null;yield return null;
            var marker=ProblemMarkers.Instance.Root.Q<Button>("problem-marker-school");Assert.AreEqual(DisplayStyle.Flex,marker.resolvedStyle.display);
            Vector2 before=marker.worldBound.center;var rig=Camera.main.GetComponent<IsoCameraController>();rig.ZoomBy(.8f);rig.FocusOn(new Vector3(1,0,1));
            yield return new WaitForSecondsRealtime(.4f);Assert.Greater(Vector2.Distance(before,marker.worldBound.center),2);
            Assert.AreEqual(ProblemMarkerState.Problem,ProblemMarkers.Instance.GetMarkerState("school"));
            yield return Click(marker);Assert.AreEqual("school",overlay.SelectedBuilding.SimulationId);
            Assert.IsFalse(session.ScannerEnabled);Assert.IsTrue(session.IsInvestigated("school"));
            yield return Click(overlay.ContextRoot.Q<Button>("context-details"));
            Assert.AreEqual("school",overlay.SelectedBuilding.SimulationId);Assert.IsTrue(overlay.ContextExpanded);
            float size=rig.TargetOrthographicSize;yield return Click(hud.Root.Q<Button>("camera-zoom-in"));Assert.Less(rig.TargetOrthographicSize,size);
        }
    }
}

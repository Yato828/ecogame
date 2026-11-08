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
    /// <summary>Actual scene/UI checks for the investigate-act-observe loop replacing the G2 shortcut.</summary>
    public sealed class GameplayIntegrationTests
    {
        GameSession session;
        GameplayHud hud;
        CityOverlay overlay;
        Mouse mouse;
        string saveDirectory;
        static string Evidence
        {
            get
            {
                string[] args=Environment.GetCommandLineArgs();int i=Array.IndexOf(args,"-wattshiftEvidenceOutput");
                return (i>=0&&i+1<args.Length?args[i+1]:Path.GetFullPath(Path.Combine(Application.dataPath,"../docs/evidence/g3")))+Path.DirectorySeparatorChar;
            }
        }

        [UnitySetUp]
        public IEnumerator Setup()
        {
            CameraControlsTests.SetViewport();
            UnityEditor.ShaderUtil.allowAsyncCompilation=false;
            saveDirectory=Path.Combine(Path.GetTempPath(),"WattshiftG3-"+Guid.NewGuid().ToString("N"));
            GameSession.EditorTestSaveDirectory=saveDirectory;
            InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            SceneManager.LoadScene("CitySlice");yield return null;yield return null;
            session=GameSession.Instance;hud=GameplayHud.Instance;overlay=CityOverlay.Instance;
            Assert.IsNotNull(hud);Assert.IsTrue(session.Ready);
            session.enabled=false; // Deterministic clock steps; the separate walkthrough checks wall-clock play.
            mouse=InputSystem.AddDevice<Mouse>("G3 UI verification");
            yield return new WaitForSecondsRealtime(.25f);
        }

        [UnityTearDown]
        public IEnumerator Teardown()
        {
            if(mouse!=null)InputSystem.RemoveDevice(mouse);GameSession.EditorTestSaveDirectory=null;
            CameraControlsTests.SetViewport();
            if(Directory.Exists(saveDirectory))Directory.Delete(saveDirectory,true);
            yield return null;
        }

        IEnumerator Click(Button button)
        {
            Assert.IsNotNull(button);Assert.IsTrue(button.enabledInHierarchy);
            var scroll=button.GetFirstAncestorOfType<ScrollView>();if(scroll!=null)scroll.ScrollTo(button);
            yield return new WaitForSecondsRealtime(.06f);
            float scale=Screen.width/button.panel.visualTree.worldBound.width;
            Vector2 p=Vector2.zero;
            // Settle deferred layout/focus scrolling after the prior action, then move before pressing.
            for(int frame=0;frame<2;frame++)
            {
                p=new Vector2(button.worldBound.center.x*scale,Screen.height-button.worldBound.center.y*scale);
                InputSystem.QueueStateEvent(mouse,new MouseState{position=p});yield return null;
            }
            p=new Vector2(button.worldBound.center.x*scale,Screen.height-button.worldBound.center.y*scale);
            Assert.Greater(button.worldBound.width,0,"The requested button must be visible: "+button.name);
            Debug.Log("G3_UI_CLICK "+button.name+" bound="+button.worldBound+" scale="+scale+" picked="+button.panel.Pick(button.worldBound.center)?.name);
            InputSystem.QueueStateEvent(mouse,new MouseState{position=p,buttons=1});yield return new WaitForSecondsRealtime(.08f);
            InputSystem.QueueStateEvent(mouse,new MouseState{position=p});yield return new WaitForSecondsRealtime(.15f);
        }
        IEnumerator Capture(string name)
        {
            Directory.CreateDirectory(Evidence);string path=Evidence+name;if(File.Exists(path))File.Delete(path);
            yield return new WaitForSecondsRealtime(.5f);ScreenCapture.CaptureScreenshot(path);
            yield return new WaitForSecondsRealtime(.5f);Assert.IsTrue(File.Exists(path));
        }

        [UnityTest]
        public IEnumerator FirstShiftRequiresInvestigationRealObservationAndPreservesProgress()
        {
            Assert.AreEqual(GamePhase.Menu,session.Phase);
            yield return Capture("a-01-main-menu.png");
            yield return Click(hud.Root.Q<Button>("start-mission-0"));
            Assert.AreEqual(GamePhase.Playing,session.Phase);
            Assert.That(session.GameHour,Is.EqualTo(17).Within(.0001));
            var school=UnityEngine.Object.FindObjectsByType<CityBuilding>(FindObjectsSortMode.None).Single(b=>b.SimulationId=="school");
            overlay.Select(school);
            session.Purchase("school","school-computers");
            Assert.AreEqual(0,session.Snapshot.PurchaseCount,"Offers cannot bypass investigation");
            yield return Click(hud.Root.Q<Button>("scanner-toggle"));
            yield return Click(hud.UpgradeRoot.Q<Button>("inspect-building"));
            Assert.IsFalse(session.IsInvestigated("school"),"Before classes end there is not yet an after-hours problem");
            session.Tick(60);
            Assert.That(session.GameHour,Is.EqualTo(18).Within(.0001));
            Assert.Contains("after-class",session.TriggeredEventIds);
            yield return Click(hud.Root.Q<Button>("pause-toggle"));
            double frozen=session.GameHour;session.Tick(120);Assert.AreEqual(frozen,session.GameHour);
            yield return Click(hud.UpgradeRoot.Q<Button>("inspect-building"));
            Assert.IsTrue(session.IsInvestigated("school"));
            yield return Capture("a-02-scanner-investigation.png");
            yield return Click(hud.UpgradeRoot.Q<Button>("upgrade-school-lights"));
            yield return Click(hud.UpgradeRoot.Q<Button>("upgrade-school-computers"));
            Assert.AreEqual(210,session.Snapshot.Budget);
            session.CompleteMission();Assert.IsFalse(session.Snapshot.Completed,"Purchases alone cannot win");
            Assert.AreEqual(GamePhase.Playing,session.Phase);
            double accrued=session.Snapshot.AccumulatedEnergyKWh;
            yield return Click(hud.Root.Q<Button>("pause-toggle"));
            session.Tick(180);
            Assert.That(session.GameHour,Is.EqualTo(21).Within(.0001));
            Assert.Greater(session.Snapshot.AccumulatedEnergyKWh,accrued);
            yield return Capture("a-03-observed-night.png");
            session.Save();
            Assert.IsTrue(session.Simulation.AdvanceTo(21.5).Success); // Deliberately unsaved mutation, not the auto-saving clock.
            session.Load();
            Assert.IsTrue(session.Paused);Assert.That(session.GameHour,Is.EqualTo(21).Within(.0001));
            Assert.IsTrue(session.IsInvestigated("school"));
            session.TogglePause();session.Tick(120);
            Assert.That(session.GameHour,Is.EqualTo(23).Within(.0001));
            Assert.That(session.Snapshot.AccumulatedEnergyKWh,Is.EqualTo(111.52).Within(.001));
            Assert.That(session.Snapshot.AccumulatedBaselineEnergyKWh,Is.EqualTo(161.52).Within(.001));
            yield return Click(hud.CompleteButton);
            Assert.IsTrue(session.Success);Assert.AreEqual(GamePhase.Results,session.Phase);Assert.AreEqual(3,session.Stars);
            yield return Capture("a-04-shift-results.png");
            int credits=session.CampaignCredits;
            session.ReturnToMenu();session.Load();
            Assert.IsTrue(session.Success);Assert.AreEqual(credits,session.CampaignCredits);
            SceneManager.LoadScene("CitySlice");yield return null;yield return null;
            session=GameSession.Instance;session.enabled=false;session.Load();
            Assert.IsTrue(session.Success);Assert.AreEqual(credits,session.CampaignCredits);
        }

        [UnityTest]
        public IEnumerator MissedShiftFailsAndLatePurchasesCannotRewriteEnergy()
        {
            session.BeginMission(0);session.Tick(360);session.CompleteMission();
            Assert.AreEqual(GamePhase.Results,session.Phase);Assert.AreEqual(0,session.Stars);Assert.IsFalse(session.Success);
            session.BeginMission(0);session.Tick(300);session.SetScanner(true);session.InspectBuilding("school");
            double before=session.Snapshot.AccumulatedEnergyKWh;
            session.Purchase("school","school-lights");session.Purchase("school","school-computers");
            Assert.AreEqual(before,session.Snapshot.AccumulatedEnergyKWh);
            session.Tick(60);session.CompleteMission();Assert.IsFalse(session.Success);
            Assert.AreEqual(0,session.CampaignCredits);
            yield return null;
        }

        [UnityTest]
        public IEnumerator MenuAndInspectionRemainReachableAcrossAspects()
        {
            foreach(var size in new[]{new Vector2Int(1920,1080),new Vector2Int(2560,1080),new Vector2Int(1200,900),new Vector2Int(900,1200)})
            {
                CameraControlsTests.SetViewport(size.x,size.y);yield return new WaitForSecondsRealtime(.2f);
                Assert.IsNotNull(hud.Root.Q<Button>("start-mission-0"));
                session.BeginMission(0);session.Tick(60);session.TogglePause();session.SetScanner(true);
                var school=UnityEngine.Object.FindObjectsByType<CityBuilding>(FindObjectsSortMode.None).Single(b=>b.SimulationId=="school");
                overlay.Select(school);yield return null;
                Assert.IsNotNull(hud.UpgradeRoot.Q<Button>("inspect-building"));
                if(size.x==900)yield return Capture("a-05-portrait-inspection.png");
                session.ReturnToMenu();yield return null;
            }
            CameraControlsTests.SetViewport();
        }

        void AtHour(double hour)
        {
            if(session.Paused)session.TogglePause();
            session.Tick((hour-session.GameHour)*session.Mission.RealDurationSeconds/(session.Mission.EndHour-session.Mission.StartHour));
            if(hour<session.Mission.EndHour&&!session.Paused)session.TogglePause();
        }

        IEnumerator Investigate(string id)
        {
            if(!session.ScannerEnabled)yield return Click(hud.Root.Q<Button>("scanner-toggle"));
            var view=UnityEngine.Object.FindObjectsByType<CityBuilding>(FindObjectsSortMode.None).Where(b=>b.SimulationId==id).OrderBy(b=>b.transform.position.z).First();
            overlay.Select(view);yield return null;
            if(!session.IsInvestigated(id))yield return Click(hud.UpgradeRoot.Q<Button>("inspect-building"));
            Assert.IsTrue(session.IsInvestigated(id));
        }

        [UnityTest]
        public IEnumerator ThreeDistinctMissionsUnlockSafelyAndKeepBestRewards()
        {
            Assert.AreEqual(3,session.Missions.Length);Assert.IsFalse(session.IsMissionUnlocked(1));
            session.BeginMission(1);Assert.AreEqual(GamePhase.Menu,session.Phase);
            yield return Click(hud.Root.Q<Button>("start-mission-0"));AtHour(18);
            yield return Investigate("school");
            yield return Click(hud.UpgradeRoot.Q<Button>("upgrade-school-lights"));
            yield return Click(hud.UpgradeRoot.Q<Button>("upgrade-school-computers"));
            AtHour(23);yield return Click(hud.CompleteButton);
            Assert.IsTrue(session.Success);Assert.AreEqual(3,session.Stars);Assert.IsTrue(session.IsMissionUnlocked(1));
            session.ReturnToMenu();yield return null;
            yield return Click(hud.Root.Q<Button>("start-mission-1"));AtHour(18);
            yield return Investigate("crossings");
            int budget=session.Snapshot.Budget;
            yield return Click(hud.UpgradeRoot.Q<Button>("upgrade-crossings-off"));
            Assert.AreEqual(budget,session.Snapshot.Budget);Assert.IsTrue(session.Snapshot.SafetySatisfied);
            yield return Click(hud.UpgradeRoot.Q<Button>("upgrade-crossings-led"));
            var lamps=UnityEngine.Object.FindObjectsByType<StreetlightView>(FindObjectsSortMode.None);
            Assert.AreEqual(4,lamps.Length);
            foreach(var lamp in lamps.Where(l=>l.BuildingId=="crossings"))
                foreach(var light in lamp.Lights){Assert.IsTrue(light.enabled);Assert.That(light.intensity,Is.EqualTo(lamp.BaseIntensity).Within(.001));}
            yield return Investigate("walkways");
            yield return Click(hud.UpgradeRoot.Q<Button>("upgrade-walkways-led"));
            Assert.AreEqual(0,session.Snapshot.Budget);AtHour(21);
            yield return Capture("b-01-safe-streetlighting.png");
            AtHour(23);yield return Click(hud.CompleteButton);
            Assert.IsTrue(session.Success);Assert.AreEqual(3,session.Stars);Assert.IsTrue(session.IsMissionUnlocked(2));
            session.ReturnToMenu();yield return null;
            yield return Click(hud.Root.Q<Button>("start-mission-2"));AtHour(18);
            yield return Investigate("clinic");
            Assert.AreEqual(5,session.Snapshot.Buildings.Single(b=>b.BuildingId=="clinic").Devices.Length);
            yield return Click(hud.UpgradeRoot.Q<Button>("upgrade-clinic-off"));
            Assert.AreEqual(550,session.Snapshot.Budget);
            yield return Click(hud.UpgradeRoot.Q<Button>("upgrade-clinic-task-off"));
            Assert.AreEqual(550,session.Snapshot.Budget);
            yield return Click(hud.UpgradeRoot.Q<Button>("upgrade-clinic-aux"));
            yield return Click(hud.UpgradeRoot.Q<Button>("upgrade-clinic-admin"));
            yield return Click(hud.UpgradeRoot.Q<Button>("upgrade-clinic-led"));
            Assert.AreEqual(0,session.Snapshot.Budget);AtHour(21);
            var clinic=session.Snapshot.Buildings.Single(b=>b.BuildingId=="clinic");
            Assert.That(clinic.CurrentPowerKW,Is.EqualTo(10).Within(.0001));
            Assert.That(clinic.Devices.Single(d=>d.Id=="medical").CurrentPowerKW,Is.EqualTo(8).Within(.0001));
            Assert.IsTrue(session.Snapshot.SafetySatisfied);
            Assert.IsTrue(UnityEngine.Object.FindObjectsByType<BuildingEnergyView>(FindObjectsSortMode.None).Single(v=>v.BuildingId=="clinic").WindowRenderer.enabled);
            (hud.UpgradeRoot.panel.focusController.focusedElement as VisualElement)?.Blur();
            ((ScrollView)hud.UpgradeRoot).scrollOffset=Vector2.zero;yield return null;
            yield return Capture("b-02-clinic-priorities.png");
            AtHour(23);yield return Click(hud.CompleteButton);
            Assert.IsTrue(session.Success);Assert.AreEqual(3,session.Stars);
            int credits=session.CampaignCredits;
            Assert.AreEqual(1110,credits);
            File.Copy(Path.Combine(saveDirectory,"wattshift-campaign-v3.json"), Evidence+"restart-campaign.json",true);
            yield return Capture("b-03-campaign-results.png");
            session.ReturnToMenu();yield return null;yield return Capture("b-04-mission-progress.png");
            SceneManager.LoadScene("CitySlice");yield return null;yield return null;
            session=GameSession.Instance;session.enabled=false;hud=GameplayHud.Instance;overlay=CityOverlay.Instance;
            Assert.IsTrue(session.IsMissionUnlocked(2));Assert.AreEqual(credits,session.CampaignCredits);
            session.Load();Assert.IsTrue(session.Success);Assert.AreEqual(2,session.MissionIndex);
            // Replay may improve results, but repeating an identical route cannot generate credits.
            session.BeginMission(0);AtHour(18);session.SetScanner(true);session.InspectBuilding("school");
            session.Purchase("school","school-lights");session.Purchase("school","school-computers");AtHour(23);session.CompleteMission();
            Assert.IsTrue(session.Success);Assert.AreEqual(credits,session.CampaignCredits);
            // Cheaper street control is allowed only on the lower-priority routes after20:00.
            session.BeginMission(1);AtHour(18);session.SetScanner(true);session.InspectBuilding("crossings");session.InspectBuilding("walkways");
            session.Purchase("walkways","walkways-adaptive");Assert.AreEqual(380,session.Snapshot.Budget);
            session.Purchase("crossings","crossings-led");AtHour(20);session.Purchase("walkways","walkways-adaptive");
            Assert.AreEqual(50,session.Snapshot.Budget);
            foreach(var lamp in UnityEngine.Object.FindObjectsByType<StreetlightView>(FindObjectsSortMode.None).Where(l=>l.BuildingId=="walkways"))
                foreach(var light in lamp.Lights)Assert.That(light.intensity,Is.EqualTo(lamp.BaseIntensity*.65f).Within(.001));
            AtHour(23);session.CompleteMission();Assert.IsTrue(session.Success);Assert.AreEqual(1,session.Stars);Assert.AreEqual(credits,session.CampaignCredits);
        }

        [UnityTest]
        public IEnumerator SettingsPersistWithoutEditingPipelineAndBlockWorldInput()
        {
            var audio=Wattshift.Presentation.CampaignAudio.Instance;
            var quality=Wattshift.Presentation.QualityController.Instance;
            Assert.IsNotNull(audio); Assert.IsNotNull(quality);
            var pipeline=UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline as UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset;
            Assert.IsNotNull(pipeline);
            // Runtime clone has no persistent asset path; approved asset bytes must remain identical.
            Assert.IsEmpty(UnityEditor.AssetDatabase.GetAssetPath(pipeline));
            var assetPaths=UnityEditor.AssetDatabase.FindAssets("t:UniversalRenderPipelineAsset").Select(UnityEditor.AssetDatabase.GUIDToAssetPath).ToArray();
            var hashes=assetPaths.ToDictionary(path=>path,path=>File.ReadAllBytes(path));
            yield return Click(hud.Root.Q<Button>("settings-menu")); Assert.IsTrue(hud.IsSettingsOpen);
            yield return Click(hud.Root.Q<Button>("quality-0")); Assert.AreEqual(0,quality.Tier);
            Assert.That(pipeline.renderScale,Is.LessThanOrEqualTo(.65f));
            hud.Root.Q<Slider>("audio-volume").value=.3f;
            Assert.That(audio.Volume,Is.EqualTo(.3f).Within(.001));
            yield return Click(hud.Root.Q<Button>("audio-mute"));Assert.IsTrue(audio.Muted);
            yield return new WaitForSecondsRealtime(3);
            yield return Capture("c-01-settings.png");
            yield return Click(hud.Root.Q<Button>("settings-close"));
            yield return Click(hud.Root.Q<Button>("start-mission-0"));
            Assert.IsFalse(session.Paused);
            yield return Click(hud.Root.Q<Button>("settings-playing"));Assert.IsTrue(session.Paused);
            Assert.IsFalse(hud.Root.Q<Button>("pause-toggle").enabledInHierarchy);
            Assert.IsFalse(hud.Root.Q<Button>("settings-menu").enabledInHierarchy);
            Assert.IsFalse(overlay.ContextRoot.enabledInHierarchy);
            var rig=Camera.main.GetComponent<IsoCameraController>();
            Vector3 focus=rig.TargetFocusPoint;float size=rig.TargetOrthographicSize;
            var keyboard=InputSystem.AddDevice<Keyboard>("Settings isolation");
            try
            {
                InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.D,Key.Equals,Key.Tab));
                yield return new WaitForSecondsRealtime(.2f);
                InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return null;
                Assert.That(Vector3.Distance(focus,rig.TargetFocusPoint),Is.LessThan(.001f));
                Assert.That(rig.TargetOrthographicSize,Is.EqualTo(size).Within(.001f));Assert.IsNull(overlay.SelectedBuilding);
                var focused=hud.Root.panel.focusController.focusedElement as VisualElement;
                Assert.IsNotNull(focused);Assert.IsTrue(hud.Root.Q("settings-overlay").Contains(focused),"Keyboard focus must remain inside settings");
            }
            finally { InputSystem.RemoveDevice(keyboard); }
            Assert.IsTrue(hud.IsPointerOverUI(new Vector2(Screen.width/2f,Screen.height/2f)));
            yield return Click(hud.Root.Q<Button>("quality-1"));Assert.AreEqual(1,quality.Tier);
            yield return Click(hud.Root.Q<Button>("quality-2"));Assert.AreEqual(2,quality.Tier);
            foreach(var path in assetPaths)CollectionAssert.AreEqual(hashes[path],File.ReadAllBytes(path));
            yield return Click(hud.Root.Q<Button>("settings-close"));Assert.IsTrue(session.Paused);
            Assert.IsTrue(hud.Root.Q<Button>("pause-toggle").enabledInHierarchy);Assert.IsTrue(overlay.ContextRoot.enabledInHierarchy);
            session.Purchase("school","school-lights");Assert.IsTrue(session.LastActionFailed);
            CameraControlsTests.SetViewport(900,1200);yield return new WaitForSecondsRealtime(.3f);
            Assert.AreEqual(DisplayStyle.Flex,hud.Root.Q<Label>("action-message").resolvedStyle.display);
            yield return Capture("c-02-portrait-error.png");
            float storedVolume=audio.Volume; // Keyboard navigation may legitimately adjust the focused volume slider.
            SceneManager.LoadScene("CitySlice");yield return null;yield return null;
            session=GameSession.Instance;session.enabled=false;hud=GameplayHud.Instance;overlay=CityOverlay.Instance;
            Assert.That(Wattshift.Presentation.CampaignAudio.Instance.Volume,Is.EqualTo(storedVolume).Within(.001));
            Assert.IsTrue(Wattshift.Presentation.CampaignAudio.Instance.Muted);Assert.AreEqual(2,Wattshift.Presentation.QualityController.Instance.Tier);
            session.Load();Assert.IsFalse(session.LastActionFailed);
            session.ReturnToMenu();File.WriteAllText(Path.Combine(saveDirectory,"wattshift-campaign-v3.json"),"{broken json");
            session.Load();Assert.IsTrue(session.LastActionFailed);
            yield return null;Assert.AreEqual(DisplayStyle.Flex,hud.Root.Q<Label>("menu-error").resolvedStyle.display);
            yield return Capture("c-03-portrait-load-error.png");
            CameraControlsTests.SetViewport();
        }

        [UnityTest, Explicit("Run in a fresh Editor after the campaign walkthrough writes its actual save.")]
        public IEnumerator FreshEditorRestoresActualCompletedCampaign()
        {
            string actual=Evidence+"restart-campaign.json";Assert.IsTrue(File.Exists(actual));
            Directory.CreateDirectory(saveDirectory);
            File.Copy(actual,Path.Combine(saveDirectory,"wattshift-campaign-v3.json"),true);
            session.Load();yield return null;
            Assert.IsFalse(session.LastActionFailed);Assert.IsTrue(session.Success);
            Assert.AreEqual(2,session.MissionIndex);Assert.AreEqual(1110,session.CampaignCredits);
            for(int i=0;i<3;i++){Assert.AreEqual(3,session.GetBestStars(i));Assert.IsTrue(session.IsMissionUnlocked(i));}
            Assert.AreEqual(0,session.Snapshot.Budget);Assert.That(session.GameHour,Is.EqualTo(23));
            Assert.That(session.Snapshot.AccumulatedEnergyKWh,Is.EqualTo(111.22).Within(.001));
            yield return Capture("d-01-fresh-editor-restored.png");
        }

        [UnityTest, Timeout(900000), Explicit("Two six-minute live shifts, no artificial clock jumps within either shift.")]
        public IEnumerator RealTimeStreetAndClinicWalkthrough()
        {
            // Unlock through a previously tested first-shift route; timed evidence below covers missions2/3 only.
            session.BeginMission(0);AtHour(18);session.SetScanner(true);session.InspectBuilding("school");
            session.Purchase("school","school-lights");session.Purchase("school","school-computers");AtHour(23);session.CompleteMission();
            Assert.IsTrue(session.Success);session.ReturnToMenu();yield return null;
            var timings=new System.Text.StringBuilder("Unity Game View automatic UI walkthrough; actual360second clock per shift.\n");
            for(int mission=1;mission<=2;mission++)
            {
                session.enabled=true;
                yield return Click(hud.Root.Q<Button>("start-mission-"+mission));
                var watch=System.Diagnostics.Stopwatch.StartNew();
                while(session.GameHour<18.01){Assert.Less(watch.Elapsed.TotalSeconds,420);yield return null;}
                yield return Click(hud.Root.Q<Button>("pause-toggle"));
                if(mission==1)
                {
                    yield return Investigate("crossings");
                    yield return Click(hud.UpgradeRoot.Q<Button>("upgrade-crossings-led"));
                    yield return Investigate("walkways");
                    yield return Click(hud.UpgradeRoot.Q<Button>("upgrade-walkways-led"));
                }
                else
                {
                    yield return Investigate("clinic");
                    yield return Click(hud.UpgradeRoot.Q<Button>("upgrade-clinic-aux"));
                    yield return Click(hud.UpgradeRoot.Q<Button>("upgrade-clinic-admin"));
                    yield return Click(hud.UpgradeRoot.Q<Button>("upgrade-clinic-led"));
                }
                yield return Click(hud.Root.Q<Button>("pause-toggle"));bool captured=false;
                while(session.GameHour<23)
                {
                    Assert.Less(watch.Elapsed.TotalSeconds,430);
                    if(!captured&&session.GameHour>=21){captured=true;yield return Capture("live-mission-"+(mission+1)+"-night.png");}
                    yield return null;
                }
                Assert.That(watch.Elapsed.TotalSeconds,Is.InRange(355,430));
                timings.AppendLine(session.Mission.Title+": actual seconds="+watch.Elapsed.TotalSeconds+"; actual kWh="+session.Snapshot.AccumulatedEnergyKWh);
                yield return Click(hud.CompleteButton);Assert.IsTrue(session.Success);Assert.AreEqual(3,session.Stars);
                yield return Capture("live-mission-"+(mission+1)+"-results.png");
                File.WriteAllText(Evidence+"live-remaining-clock.txt",timings.ToString());
                session.ReturnToMenu();yield return null;
            }
        }

        [UnityTest, Timeout(480000), Explicit("Six-minute real-clock Game View walkthrough; run by exact test name.")]
        public IEnumerator RealTimeFirstShiftWalkthrough()
        {
            session.enabled=true;
            yield return Click(hud.Root.Q<Button>("start-mission-0"));
            double started=Time.realtimeSinceStartupAsDouble;
            var activeClock=System.Diagnostics.Stopwatch.StartNew();
            DateTime wallStarted=DateTime.UtcNow;
            var frames=new System.Collections.Generic.List<float>(40000);
            while(session.GameHour<18.01)
            {
                if(activeClock.Elapsed.TotalSeconds>450)Assert.Fail("Clock stalled before investigation");
                if(activeClock.Elapsed.TotalSeconds>20)frames.Add(Time.unscaledDeltaTime);
                yield return null;
            }
            yield return Click(hud.Root.Q<Button>("pause-toggle"));
            yield return Click(hud.Root.Q<Button>("scanner-toggle"));
            var school=UnityEngine.Object.FindObjectsByType<CityBuilding>(FindObjectsSortMode.None).Single(b=>b.SimulationId=="school");
            Vector2 p=Camera.main.WorldToScreenPoint(school.GetComponent<Collider>().bounds.center);
            InputSystem.QueueStateEvent(mouse,new MouseState{position=p,buttons=1});yield return new WaitForSecondsRealtime(.1f);
            InputSystem.QueueStateEvent(mouse,new MouseState{position=p});yield return new WaitForSecondsRealtime(.2f);
            Assert.AreEqual(school,overlay.SelectedBuilding);
            yield return Click(hud.UpgradeRoot.Q<Button>("inspect-building"));
            yield return Capture("live-01-scanner.png");
            yield return Click(hud.UpgradeRoot.Q<Button>("upgrade-school-sensors"));
            yield return Click(hud.UpgradeRoot.Q<Button>("upgrade-school-computers"));
            Assert.AreEqual(170,session.Snapshot.Budget);
            yield return Click(hud.Root.Q<Button>("pause-toggle"));
            bool nightCaptured=false;
            while(session.GameHour<23)
            {
                if(activeClock.Elapsed.TotalSeconds>450)Assert.Fail("Clock stalled during shift");
                if(!session.Paused)frames.Add(Time.unscaledDeltaTime);
                if(!nightCaptured&&session.GameHour>=20.05)
                {nightCaptured=true;yield return Capture("live-02-night-operation.png");}
                yield return null;
            }
            double elapsed=activeClock.Elapsed.TotalSeconds;
            Assert.That(elapsed,Is.InRange(355,440),"Configured six-minute shift must run against actual wall time");
            yield return Click(hud.CompleteButton);
            Assert.IsTrue(session.Success);Assert.AreEqual(2,session.Stars);
            yield return Capture("live-03-results.png");
            frames.Sort();
            File.WriteAllText(Evidence+"live-clock-and-editor-frames.txt",
                "Actual Editor Play Mode walkthrough, default360-second profile; no time jumps.\n"+
                "Active monotonic seconds including decision pauses="+elapsed+"\nUTC wall seconds="+(DateTime.UtcNow-wallStarted).TotalSeconds+"\nUnity realtime seconds="+(Time.realtimeSinceStartupAsDouble-started)+"\nGPU="+SystemInfo.graphicsDeviceName+"\nSamples="+frames.Count+
                "\nMedian frame ms="+(frames.Count>0?frames[frames.Count/2]*1000:0)+
                "\nP95 frame ms="+(frames.Count>0?frames[(int)((frames.Count-1)*.95)]*1000:0)+
                "\nEditor-only sample; no native/browser/physical-device FPS claim.\n");
        }
    }
}

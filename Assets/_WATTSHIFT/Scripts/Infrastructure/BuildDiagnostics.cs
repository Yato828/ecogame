using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Rendering;
using Wattshift.Presentation;

namespace Wattshift
{
    /// <summary>Opt-in native rendered automation. Never presented as browser, phone or human play evidence.</summary>
    public sealed class BuildDiagnostics : MonoBehaviour
    {
        [Serializable] sealed class TierSample
        {
            public string tier, drawCallsStatus, batchesStatus;
            public int renderedFrames;
            public double observedSeconds, medianFrameMs, p95FrameMs, observedFps;
            public long allocatedBytes, reservedBytes, drawCalls, batches;
        }
        [Serializable] sealed class SmokeReport
        {
            public string result = "Running", utc, unity, os, gpu, graphicsApi, output, error;
            public string scope = "Automated native rendered smoke; no browser/device/human-play compatibility claim. Frame timings include development-player instrumentation.";
            public int width, height, meshRenderers, missingMaterials, missingMeshes, consoleErrors, consoleWarnings;
            public bool speed1, speed2, speed4, savedAndLoaded, finished, schoolPurchased;
            public int score, stars, campaignStars;
            public int[] missionScores, missionStars;
            public double finalHour, finalEnergyKWh;
            public TierSample[] quality = Array.Empty<TierSample>();
            public string[] screenshots = Array.Empty<string>();
            public string[] missingReferences = Array.Empty<string>();
        }

        SmokeReport report;
        string directory;
        readonly List<string> screenshots = new List<string>();
        GameSession session;
        bool running;

        void Start()
        {
            string[] args = Environment.GetCommandLineArgs();
            if (Array.IndexOf(args, "-wattshiftSmoke") < 0) { enabled = false; return; }
            Application.runInBackground=true; // Only this opt-in smoke run, so focus changes cannot stall sampling.
            directory = Path.Combine(Application.temporaryCachePath, "WattshiftSmoke");
            int outputArgument = Array.IndexOf(args, "-wattshiftEvidenceOutput");
            if (outputArgument >= 0 && outputArgument + 1 < args.Length) directory = Path.GetFullPath(args[outputArgument + 1]);
            Directory.CreateDirectory(directory);
            report = new SmokeReport { utc = DateTime.UtcNow.ToString("O"), unity = Application.unityVersion,
                os = SystemInfo.operatingSystem, gpu = SystemInfo.graphicsDeviceName, graphicsApi = SystemInfo.graphicsDeviceType.ToString(),
                width = Screen.width, height = Screen.height, output = directory };
            Application.logMessageReceived += Log;
            running = true;
            StartCoroutine(Guard(Array.IndexOf(args,"-wattshiftRestoreSmoke")>=0 ? RestoreCampaign() : Walkthrough()));
        }

        IEnumerator Guard(IEnumerator routine)
        {
            // Flatten nested iterators so every automation exception produces a report and exit code.
            var stack = new Stack<IEnumerator>(); stack.Push(routine);
            while (stack.Count > 0)
            {
                object current = null; bool moved = false; Exception failure = null;
                try { moved = stack.Peek().MoveNext(); if (moved) current = stack.Peek().Current; }
                catch (Exception exception) { failure = exception; }
                if (failure != null) { Finish(false, failure.ToString()); yield break; }
                if (!moved) { stack.Pop(); continue; }
                if (current is IEnumerator nested) stack.Push(nested);
                else yield return current;
            }
            Finish(report.consoleErrors == 0 && report.missingMaterials == 0 && report.missingMeshes == 0, null);
        }

        IEnumerator RestoreCampaign()
        {
            yield return null;yield return null;
            session=GameSession.Instance;
            if(session==null||session.TotalStars!=9)throw new InvalidOperationException("Previous process campaign was not restored into menu.");
            session.Load();
            if(!session.Success||session.MissionIndex!=2||session.TotalStars!=9||session.Snapshot.Budget!=0)
                throw new InvalidOperationException("Previous process active result did not restore.");
            report.savedAndLoaded=true;report.finished=true;report.campaignStars=session.TotalStars;report.score=session.Score;report.stars=session.Stars;
            report.finalHour=session.GameHour;report.finalEnergyKWh=session.Snapshot.AccumulatedEnergyKWh;
            InspectReferences();yield return Capture("09-restored-campaign");
        }

        IEnumerator Walkthrough()
        {
            yield return null; yield return null;
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) throw new InvalidOperationException("Rendered smoke requires a real graphics device; do not launch with -nographics.");
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-wattshiftEvidence") < 0)
                throw new InvalidOperationException("Smoke requires -wattshiftEvidence to isolate saves and settings.");
            session = GameSession.Instance;
            if (session == null || !session.Ready || GameplayHud.Instance == null || ProblemMarkers.Instance == null)
                throw new InvalidOperationException("Required scene gameplay or marker components were not initialized.");
            InspectReferences();
            yield return Capture("01-boot-menu");
            session.BeginMission(0); session.SkipTutorial(); if (!session.Paused) session.TogglePause();
            yield return null; yield return Capture("02-problem-markers");
            session.TogglePause();session.Tick(180);session.TogglePause(); // Measure at20:00 with real street lights active.
            session.OpenProblem("school");
            if (CityOverlay.Instance != null) CityOverlay.Instance.SetExpandedCard(true);
            yield return new WaitForSecondsRealtime(.6f);
            yield return Capture("03-expanded-school");
            var samples = new List<TierSample>(3);
            for (int tier = 0; tier < 3; tier++)
            {
                if (QualityController.Instance == null) throw new InvalidOperationException("QualityController missing.");
                QualityController.Instance.Tier = tier;
                yield return new WaitForSecondsRealtime(1);
                var sample = new TierSample { tier = new[] { "Low", "Medium", "High" }[tier] };
                yield return Measure(sample);
                samples.Add(sample); report.quality = samples.ToArray();
                yield return Capture("04-quality-" + sample.tier.ToLowerInvariant());
            }
            session.BeginMission(0); // Independent early-decision route after the paused night benchmark.
            session.SetSpeed(1); report.speed1 = session.SpeedMultiplier == 1;
            session.SetSpeed(2); report.speed2 = session.SpeedMultiplier == 2;
            session.SetSpeed(4); report.speed4 = session.SpeedMultiplier == 4;
            if (!report.speed1 || !report.speed2 || !report.speed4) throw new InvalidOperationException("Speed selection did not preserve 1/2/4 values.");
            session.SetSpeed(1);
            // Deterministic scenario advancement is separate from measured wall-time frame windows.
            if (session.Paused) session.TogglePause();
            session.Tick(61);
            if (!session.Paused) session.TogglePause();
            session.OpenProblem("school");
            session.Purchase("school", "school-lights");
            if (session.LastActionFailed) throw new InvalidOperationException("School lighting purchase failed: " + session.LastMessage);
            session.Purchase("school", "school-computers");
            if (session.LastActionFailed) throw new InvalidOperationException("School computer purchase failed: " + session.LastMessage);
            report.schoolPurchased = true;
            session.Save(); if (session.LastActionFailed) throw new InvalidOperationException("Smoke save failed: " + session.LastMessage);
            double savedHour = session.GameHour; int savedBudget = session.Snapshot.Budget;
            session.Load();
            report.savedAndLoaded = !session.LastActionFailed && Math.Abs(session.GameHour - savedHour) < .00001 && session.Snapshot.Budget == savedBudget;
            if (!report.savedAndLoaded) throw new InvalidOperationException("Save/load smoke did not preserve hour and budget.");
            yield return Capture("05-resolved-school");
            session.FinishShiftEarly();
            report.finished = session.Phase == GamePhase.Results;
            report.score = session.Score; report.stars = session.Stars; report.finalHour = session.GameHour;
            report.finalEnergyKWh = session.Snapshot.AccumulatedEnergyKWh;
            if (!report.finished) throw new InvalidOperationException("FinishShiftEarly did not produce results: " + session.LastMessage);
            yield return Capture("06-results");
            report.missionScores=new int[3];report.missionStars=new int[3];report.missionScores[0]=session.Score;report.missionStars[0]=session.Stars;
            session.BeginMission(1);session.OpenProblem("crossings");session.Purchase("crossings","crossings-led");session.OpenProblem("walkways");session.Purchase("walkways","walkways-led");session.FinishShiftEarly();
            if(!session.Success||session.Stars<1)throw new InvalidOperationException("Street mission did not complete safely.");
            report.missionScores[1]=session.Score;report.missionStars[1]=session.Stars;yield return Capture("07-street-results");
            session.BeginMission(2);session.OpenProblem("clinic");session.Purchase("clinic","clinic-aux");session.Purchase("clinic","clinic-admin");session.Purchase("clinic","clinic-led");session.FinishShiftEarly();
            if(!session.Success||session.Stars<1)throw new InvalidOperationException("Clinic mission did not complete safely.");
            report.missionScores[2]=session.Score;report.missionStars[2]=session.Stars;report.campaignStars=session.TotalStars;session.Save();session.Load();
            if(session.LastActionFailed||session.TotalStars<3)throw new InvalidOperationException("Campaign restore failed in native player.");
            yield return Capture("08-clinic-results");
        }

        IEnumerator Measure(TierSample sample)
        {
            var frames = new List<double>(8192);
            var drawSamples=new List<long>(2048);var batchSamples=new List<long>(2048);
            ProfilerRecorder draws = default, batches = default;
            try { draws = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count", 1); } catch (Exception) { }
            try { batches = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Batches Count", 1); } catch (Exception) { }
            double start = Time.realtimeSinceStartupAsDouble, previous = start;
            int firstRenderedFrame = Time.renderedFrameCount;
            while (Time.realtimeSinceStartupAsDouble - start < 10)
            {
                yield return new WaitForEndOfFrame();
                double now = Time.realtimeSinceStartupAsDouble;
                frames.Add((now - previous) * 1000); previous = now;
                if(draws.Valid && draws.Count>0 && draws.LastValue>0)drawSamples.Add(draws.LastValue);
                if(batches.Valid && batches.Count>0 && batches.LastValue>0)batchSamples.Add(batches.LastValue);
            }
            sample.observedSeconds = Time.realtimeSinceStartupAsDouble - start;
            sample.renderedFrames = Time.renderedFrameCount - firstRenderedFrame;
            frames.Sort();
            if (frames.Count == 0 || sample.renderedFrames == 0) throw new InvalidOperationException("No rendered frames in measurement.");
            sample.medianFrameMs = frames[frames.Count / 2];
            sample.p95FrameMs = frames[Math.Min(frames.Count - 1, (int)Math.Ceiling(frames.Count * .95) - 1)];
            sample.observedFps = sample.renderedFrames / sample.observedSeconds;
            sample.allocatedBytes = Profiler.GetTotalAllocatedMemoryLong(); sample.reservedBytes = Profiler.GetTotalReservedMemoryLong();
            drawSamples.Sort();batchSamples.Sort();
            sample.drawCallsStatus=drawSamples.Count>0 ? "median nonzero rendered samples: "+drawSamples.Count : "unavailable: no positive render sample";
            sample.batchesStatus=batchSamples.Count>0 ? "median nonzero rendered samples: "+batchSamples.Count : "unavailable: no positive render sample";
            sample.drawCalls=drawSamples.Count>0 ? drawSamples[drawSamples.Count/2] : -1;
            sample.batches=batchSamples.Count>0 ? batchSamples[batchSamples.Count/2] : -1;
            draws.Dispose(); batches.Dispose();
        }

        IEnumerator Capture(string name)
        {
            yield return new WaitForEndOfFrame();
            string path = Path.Combine(directory, name + ".png");
            if (File.Exists(path)) File.Delete(path);
            ScreenCapture.CaptureScreenshot(path);
            double deadline = Time.realtimeSinceStartupAsDouble + 8;
            // CaptureScreenshot completes asynchronously after this frame.
            yield return null; yield return null;
            while (!File.Exists(path) && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
            if (!File.Exists(path)) throw new IOException("Screenshot was not written: " + path);
            screenshots.Add(path);
        }

        void InspectReferences()
        {
            var missing = new List<string>();
            var renderers = FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            report.meshRenderers = renderers.Length;
            foreach (var renderer in renderers)
            {
                var filter = renderer.GetComponent<MeshFilter>();
                if (filter == null || filter.sharedMesh == null) { report.missingMeshes++; missing.Add(renderer.name + ": mesh missing"); }
                Material[] materials = renderer.sharedMaterials;
                if (materials.Length == 0) { report.missingMaterials++; missing.Add(renderer.name + ": no material slots"); }
                foreach (Material material in materials)
                    if (material == null || material.shader == null || material.shader.name == "Hidden/InternalErrorShader")
                    { report.missingMaterials++; missing.Add(renderer.name + ": missing material/shader"); }
            }
            report.missingReferences = missing.ToArray();
        }

        void Log(string message, string stack, LogType type)
        {
            if (type == LogType.Error || type == LogType.Assert || type == LogType.Exception) report.consoleErrors++;
            if (type == LogType.Warning) report.consoleWarnings++;
        }

        void Finish(bool success, string error)
        {
            if (!running) return;
            running = false;
            report.result = success ? "Passed" : "Failed"; report.error = error;
            report.screenshots = screenshots.ToArray();
            File.WriteAllText(Path.Combine(directory, "native-smoke.json"), JsonUtility.ToJson(report, true));
            Application.logMessageReceived -= Log;
            Debug.Log("WATTSHIFT_G4_NATIVE_SMOKE: " + report.result + " " + directory);
            Application.Quit(success ? 0 : 1);
        }

        void OnDestroy() { Application.logMessageReceived -= Log; }
    }
}

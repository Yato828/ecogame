using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace Wattshift.Tests
{
    public sealed class G3CampaignPersistenceTests
    {
        readonly List<UnityEngine.Object> assets = new List<UnityEngine.Object>();
        string directory;
        CampaignSaveService service;

        [SetUp] public void Setup()
        {
            directory = Path.Combine(Path.GetTempPath(), "wattshift-campaign-test-" + Guid.NewGuid().ToString("N"));
            service = new CampaignSaveService(Path.Combine(directory, "campaign.json"));
        }

        [TearDown] public void Cleanup()
        {
            foreach (var asset in assets) UnityEngine.Object.DestroyImmediate(asset);
            assets.Clear();
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }

        T Asset<T>() where T : ScriptableObject
        {
            var asset = ScriptableObject.CreateInstance<T>(); assets.Add(asset); return asset;
        }

        MissionDefinition Mission(string id)
        {
            var half = Asset<UpgradeDefinition>(); half.Id = "half"; half.Cost = 40; half.RequiredAfterHour = 18;
            half.Effects = new[] { new UpgradeEffect { DeviceId = "load", PowerMultiplier = .5f } };
            var extra = Asset<UpgradeDefinition>(); extra.Id = "extra"; extra.Cost = 20; extra.RequiredAfterHour = 18;
            extra.Effects = new[] { new UpgradeEffect { DeviceId = "load", PowerMultiplier = .5f } };
            var building = Asset<BuildingDefinition>(); building.Id = "school";
            building.Devices = new[]
            {
                new DeviceDefinition { Id = "load", PowerKW = 6, StartHour = 17, EndHour = 23 },
                new DeviceDefinition { Id = "critical", PowerKW = 2, StartHour = 17, EndHour = 23, Essential = true }
            };
            building.Upgrades = new[] { half, extra };
            var mission = Asset<MissionDefinition>(); mission.Id = id;
            mission.UseTimeline = true; mission.StartHour = 17; mission.EndHour = 23;
            mission.InitialBudget = 100; mission.BaseReward = 100; mission.TargetSavingPercent = 25;
            mission.MinimumUpgradeCount = 1; mission.MinimumObservationHours = .5f;
            mission.RequiredBuildingIds = new[] { "school" }; mission.Buildings = new[] { building };
            mission.StarSavingThresholds = new float[] { 25, 30, 40 };
            return mission;
        }

        static CityState Win(MissionDefinition mission, double purchaseHour = 18, bool extra = false)
        {
            var sim = new CitySimulationService(mission);
            Assert.That(sim.AdvanceTo(purchaseHour).Success, Is.True);
            Assert.That(sim.TryPurchase("school", "half").Success, Is.True);
            if (extra) Assert.That(sim.TryPurchase("school", "extra").Success, Is.True);
            Assert.That(sim.AdvanceTo(23).Success, Is.True);
            Assert.That(sim.TryCompleteMission().Success, Is.True);
            return sim.ExportState();
        }

        static string Records(CampaignProgress progress) => JsonUtility.ToJson(new CampaignSave { Records = progress.Export() });

        [Test] public void ImprovedStarsPayOnlyDifferenceAndWorseReplayCannotReplaceBestRun()
        {
            var mission = Mission("first"); var campaign = new CampaignProgress(new[] { mission });
            var oneStar = Win(mission, 19); var twoStars = Win(mission); var threeStars = Win(mission, extra: true);
            Assert.That(campaign.Record(0, oneStar), Is.EqualTo(100));
            Assert.That(campaign.BestStars(0), Is.EqualTo(1));
            Assert.That(campaign.Record(0, twoStars), Is.EqualTo(100));
            Assert.That(campaign.BestStars(0), Is.EqualTo(2));
            Assert.That(campaign.Record(0, threeStars), Is.EqualTo(100));
            string best = Records(campaign);
            Assert.That(campaign.Record(0, threeStars), Is.Zero);
            Assert.That(campaign.Record(0, oneStar), Is.Zero);
            Assert.That(campaign.Credits, Is.EqualTo(300));
            Assert.That(campaign.BestStars(0), Is.EqualTo(3));
            Assert.That(Records(campaign), Is.EqualTo(best));
            var restored = new CampaignProgress(new[] { mission });
            Assert.That(restored.TryRestore(campaign.Export(), out string error), Is.True, error);
            Assert.That(restored.Record(0, threeStars), Is.Zero);
            Assert.That(restored.Credits, Is.EqualTo(300));
        }

        [Test] public void BetterScoreWithinSameStarBandUpdatesBestWithoutExtraReward()
        {
            var mission = Mission("first"); var campaign = new CampaignProgress(new[] { mission });
            campaign.Record(0, Win(mission, 18.1));
            string before = Records(campaign);
            var better = Win(mission);
            Assert.That(campaign.Record(0, better), Is.Zero);
            Assert.That(campaign.BestStars(0), Is.EqualTo(2));
            Assert.That(campaign.Credits, Is.EqualTo(200));
            Assert.That(Records(campaign), Is.Not.EqualTo(before));
            Assert.That(campaign.Export()[0].BestRun.History[0].Hour, Is.EqualTo(18));
        }

        [Test] public void MissionsUnlockSequentiallyAndLockedWinCannotGrantCredit()
        {
            var first = Mission("first"); var second = Mission("second"); var third = Mission("third");
            var campaign = new CampaignProgress(new[] { first, second, third });
            Assert.Throws<ArgumentException>(() => campaign.Record(1, Win(second)));
            Assert.That(campaign.Credits, Is.Zero); Assert.That(campaign.IsUnlocked(1), Is.False);
            campaign.Record(0, Win(first));
            Assert.That(campaign.IsUnlocked(1), Is.True); Assert.That(campaign.IsUnlocked(2), Is.False);
            campaign.Record(1, Win(second));
            Assert.That(campaign.IsUnlocked(2), Is.True); Assert.That(campaign.Credits, Is.EqualTo(400));
        }

        [Test] public void CallerCannotMutateStoredRunThroughInputOrExport()
        {
            var mission = Mission("first"); var campaign = new CampaignProgress(new[] { mission });
            var run = Win(mission); campaign.Record(0, run); string before = Records(campaign);
            run.History[0].UpgradeId = "invented"; run.Buildings[0].UpgradeIds[0] = "invented";
            var exported = campaign.Export(); exported[0].MissionId = "invented";
            exported[0].BestRun.History[0].Hour = 0; exported[0].BestRun.Completed = false;
            Assert.That(Records(campaign), Is.EqualTo(before));
            var restored = new CampaignProgress(new[] { mission });
            Assert.That(restored.TryRestore(campaign.Export(), out string error), Is.True, error);
            Assert.That(restored.Credits, Is.EqualTo(200));
        }

        [TestCase("null")]
        [TestCase("null-array")]
        [TestCase("too-many")]
        [TestCase("duplicate")]
        [TestCase("unknown")]
        [TestCase("order")]
        [TestCase("missing-predecessor")]
        [TestCase("forged-time")]
        [TestCase("forged-upgrade")]
        [TestCase("uncompleted")]
        [TestCase("wrong-mission")]
        public void InvalidCampaignRestorePreservesExistingCreditRecordsAndUnlocks(string corruption)
        {
            var first = Mission("first"); var second = Mission("second");
            var campaign = new CampaignProgress(new[] { first, second }); campaign.Record(0, Win(first));
            var saved = new[]
            {
                new CampaignRecord { MissionId = first.Id, BestRun = Win(first, extra: true) },
                new CampaignRecord { MissionId = second.Id, BestRun = Win(second) }
            };
            if (corruption == "null") saved[1] = null;
            if (corruption == "null-array") saved = null;
            if (corruption == "too-many") saved = new[] { saved[0], saved[1], saved[1] };
            if (corruption == "duplicate") saved[1] = saved[0];
            if (corruption == "unknown") saved[1].MissionId = "unknown";
            if (corruption == "order") Array.Reverse(saved);
            if (corruption == "missing-predecessor") saved = new[] { saved[1] };
            if (corruption == "forged-time") saved[1].BestRun.History[0].Hour = 17;
            if (corruption == "forged-upgrade") saved[1].BestRun.Buildings[0].UpgradeIds[0] = "invented";
            if (corruption == "uncompleted") saved[1].BestRun.Completed = false;
            if (corruption == "wrong-mission") saved[1].BestRun.MissionId = first.Id;
            string before = Records(campaign);
            Assert.That(campaign.TryRestore(saved, out string error), Is.False);
            Assert.That(error, Is.Not.Empty); Assert.That(Records(campaign), Is.EqualTo(before));
            Assert.That(campaign.Credits, Is.EqualTo(200)); Assert.That(campaign.IsUnlocked(1), Is.True);
        }

        [Test] public void CampaignFileRoundTripPreservesVerifiedRewardsAndBackup()
        {
            var mission = Mission("first"); var campaign = new CampaignProgress(new[] { mission });
            var run = Win(mission); campaign.Record(0, run);
            var save = new CampaignSave { City = run, Records = campaign.Export(), InvestigatedIds = new[] { "school" }, Results = true };
            Assert.That(service.Save(save, out string message), Is.True, message);
            string first = File.ReadAllText(service.FilePath);
            save.ScannerEnabled = true;
            Assert.That(service.Save(save, out message), Is.True, message);
            Assert.That(File.ReadAllText(service.FilePath + ".bak"), Is.EqualTo(first));
            Assert.That(File.Exists(service.FilePath + ".tmp"), Is.False);
            Assert.That(service.Load(out var loaded, out message), Is.True, message);
            Assert.That(loaded.ScannerEnabled, Is.True); Assert.That(loaded.Results, Is.True);
            Assert.That(loaded.InvestigatedIds, Is.EqualTo(new[] { "school" }));
            var restored = new CampaignProgress(new[] { mission });
            Assert.That(restored.TryRestore(loaded.Records, out message), Is.True, message);
            Assert.That(restored.Credits, Is.EqualTo(200)); Assert.That(restored.Record(0, loaded.City), Is.Zero);
        }

        [Test] public void FailedOverwritePreservesLastReadableSave()
        {
            Assert.That(service.Save(new CampaignSave { ScannerEnabled = true }, out string message), Is.True, message);
            string before = File.ReadAllText(service.FilePath);
            // A directory at the staging path deterministically prevents writing the new file.
            Directory.CreateDirectory(service.FilePath + ".tmp");
            Assert.That(service.Save(new CampaignSave { ScannerEnabled = false }, out message), Is.False);
            Assert.That(message, Is.Not.Empty); Assert.That(File.ReadAllText(service.FilePath), Is.EqualTo(before));
            Assert.That(service.Load(out var loaded, out message), Is.True, message);
            Assert.That(loaded.ScannerEnabled, Is.True);
        }

        [TestCase("not-json")]
        [TestCase("{broken")]
        [TestCase("{\"Version\":3,\"City\":{")]
        [TestCase("{\"Version\":99}")]
        [TestCase("{\"City\":{}}")]
        public void CorruptOrUnsupportedFileFailsWithoutRewritingEvidence(string contents)
        {
            Directory.CreateDirectory(directory); File.WriteAllText(service.FilePath, contents);
            Assert.That(service.Load(out var loaded, out string message), Is.False);
            Assert.That(loaded, Is.Null); Assert.That(message, Is.Not.Empty);
            Assert.That(File.ReadAllText(service.FilePath), Is.EqualTo(contents));
            Assert.That(File.Exists(service.FilePath + ".tmp"), Is.False);
        }

        [Test] public void OversizeSaveIsRejectedBeforeJsonParsingAndKeptIntact()
        {
            Directory.CreateDirectory(directory);
            using (var stream = File.Create(service.FilePath)) stream.SetLength(4 * 1024 * 1024 + 1);
            Assert.That(service.Load(out var loaded, out string message), Is.False);
            Assert.That(loaded, Is.Null); Assert.That(message, Does.Contain("большой"));
            Assert.That(new FileInfo(service.FilePath).Length, Is.EqualTo(4 * 1024 * 1024 + 1));
        }

        [Test] public void MissingFileReturnsFailureWithoutCreatingSave()
        {
            Assert.That(service.Load(out var loaded, out string message), Is.False);
            Assert.That(loaded, Is.Null); Assert.That(message, Is.Not.Empty); Assert.That(service.Exists, Is.False);
        }
    }
}

using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Wattshift.Tests
{
    public sealed class G4RatingTests
    {
        [Serializable] sealed class LegacyRecord { public string MissionId; public CityState BestRun; }
        [Serializable] sealed class LegacyRecords { public LegacyRecord[] Records; }
        readonly List<UnityEngine.Object> assets = new List<UnityEngine.Object>();
        T Asset<T>() where T : ScriptableObject { var asset = ScriptableObject.CreateInstance<T>(); assets.Add(asset); return asset; }
        [TearDown] public void Cleanup() { foreach (var asset in assets) UnityEngine.Object.DestroyImmediate(asset); assets.Clear(); }

        MissionDefinition Mission()
        {
            var building = Asset<BuildingDefinition>(); building.Id = "school";
            building.Devices = new DeviceDefinition[4]; building.Upgrades = new UpgradeDefinition[4];
            var mission = Asset<MissionDefinition>(); mission.Id = "decisions";
            mission.UseTimeline = true; mission.UseDecisionRating = true; mission.TargetSavingPercent = 40;
            mission.InitialBudget = 1000; mission.RationalSpend = 400; mission.MinimumUpgradeCount = 99;
            mission.Buildings = new[] { building }; mission.Problems = new MissionProblemDefinition[4];
            for (int i = 0; i < 4; i++)
            {
                string id = "device" + i;
                building.Devices[i] = new DeviceDefinition { Id = id, PowerKW = 10, StartHour = 17, EndHour = 23 };
                var offer = Asset<UpgradeDefinition>(); offer.Id = "arbitrary-choice" + i; offer.Cost = 100;
                offer.Effects = new[] { new UpgradeEffect { DeviceId = id, PowerMultiplier = .5f } };
                building.Upgrades[i] = offer;
                mission.Problems[i] = new MissionProblemDefinition { Id = "problem" + i, BuildingId = building.Id, DeviceIds = new[] { id }, Title = id };
            }
            return mission;
        }

        CitySimulationService Run(MissionDefinition mission, int count, double hour = 18)
        {
            var sim = new CitySimulationService(mission); Assert.That(sim.AdvanceTo(hour).Success, Is.True);
            for (int i = 0; i < count; i++) Assert.That(sim.TryPurchase("school", "arbitrary-choice" + i).Success, Is.True);
            Assert.That(sim.AdvanceTo(23).Success, Is.True); return sim;
        }
        static MissionEvaluation Result(MissionDefinition mission, CitySimulationService sim) => MissionEvaluator.Evaluate(mission, sim.Evaluate(sim.CurrentHour));
        CityState Completed(MissionDefinition mission, int count, double hour = 18)
        {
            var sim = Run(mission, count, hour); Assert.That(sim.TryCompleteMission().Success, Is.True); return sim.ExportState();
        }

        [TestCase(0, 0)] [TestCase(1, 1)] [TestCase(3, 2)] [TestCase(4, 3)]
        public void PartialResultsAwardZeroThroughThreeStarsWithoutExactUpgradeCombination(int purchases, int stars)
        {
            var mission = Mission(); var sim = Run(mission, purchases); var value = Result(mission, sim);
            Assert.That(value.Stars, Is.EqualTo(stars)); Assert.That(value.ResolvedProblems, Is.EqualTo(purchases));
            Assert.That(value.Score, Is.InRange(0, 100)); Assert.That(value.CanComplete, Is.EqualTo(stars > 0));
            Assert.That(sim.TryCompleteMission().Success, Is.EqualTo(stars > 0));
        }

        [Test] public void UnsafeOrUncomfortableRunsNeverEarnStarsOrRewards()
        {
            var mission = Mission(); var sim = Run(mission, 4); var snapshot = sim.Evaluate(23);
            snapshot.SafetySatisfied = false; var unsafeResult = MissionEvaluator.Evaluate(mission, snapshot);
            Assert.That(unsafeResult.Stars, Is.Zero); Assert.That(unsafeResult.Reward, Is.Zero);
            snapshot.SafetySatisfied = true; snapshot.Comfort = 0;
            Assert.That(MissionEvaluator.Evaluate(mission, snapshot).Stars, Is.Zero);
        }

        [Test] public void LateActionCannotRewritePastConsumptionAndMustBeObserved()
        {
            var mission = Mission(); var sim = Run(mission, 4, 22.75); var result = Result(mission, sim);
            Assert.That(result.ResolvedProblems, Is.EqualTo(4)); Assert.That(result.EnergySavedKWh, Is.EqualTo(5).Within(1e-6));
            Assert.That(result.Stars, Is.Zero); Assert.That(sim.TryCompleteMission().Success, Is.False);
        }

        [Test] public void LastTenSimulatedSecondsCannotEarnStarsFromProjectedProblemResolution()
        {
            var mission = Mission(); mission.MinimumObservationHours = 0;
            var sim = Run(mission, 4, 23 - 10d / 3600); var result = Result(mission, sim);
            Assert.That(result.ResolvedProblems, Is.EqualTo(4));
            Assert.That(sim.Evaluate(23).ObservedSavingPercent, Is.LessThan(.25));
            Assert.That(result.EnergySavedKWh, Is.GreaterThan(0));
            Assert.That(result.Stars, Is.Zero); Assert.That(result.Reward, Is.Zero);
            Assert.That(sim.TryCompleteMission().Success, Is.False);
        }

        [TestCase(-1)] [TestCase(101)] [TestCase(float.NaN)] [TestCase(float.PositiveInfinity)]
        public void UsefulSavingThresholdMustBeFinitePercentage(float value)
        {
            var mission = Mission(); mission.MinimumUsefulSavingPercent = value;
            Assert.Throws<ArgumentException>(() => new CitySimulationService(mission));
        }

        [Test] public void BudgetEfficiencyUsesRationalSpendAndSupportsZeroBudgetChoices()
        {
            var mission = Mission(); mission.RationalSpend = 100;
            var result = Result(mission, Run(mission, 4)); Assert.That(result.BudgetPoints, Is.EqualTo(2.5).Within(1e-6));
            mission.InitialBudget = 0; mission.RationalSpend = 0;
            foreach (var offer in mission.Buildings[0].Upgrades) offer.Cost = 0;
            result = Result(mission, Run(mission, 4)); Assert.That(result.BudgetPoints, Is.EqualTo(10)); Assert.That(result.Stars, Is.EqualTo(3));
        }

        [Test] public void ConfiguredDeviceReductionAndWeightsControlRating()
        {
            var mission = Mission(); mission.Problems[0].MinimumReductionPercent = 60;
            var result = Result(mission, Run(mission, 1)); Assert.That(result.ResolvedProblems, Is.Zero);
            mission.ProblemWeight = 0; mission.EnergyWeight = 100; mission.SafetyWeight = mission.BudgetWeight = 0;
            result = Result(mission, Run(mission, 1)); Assert.That(result.Score, Is.EqualTo(26));
            Assert.That(result.ProblemPoints, Is.Zero); Assert.That(result.EnergyPoints, Is.EqualTo(26.0416667).Within(.0001));
        }

        [Test] public void InFlightRulesAndDeviceGroupsAreCopied()
        {
            var mission = Mission(); var sim = new CitySimulationService(mission);
            mission.Problems[0].DeviceIds[0] = "invented"; mission.Problems[0].MinimumReductionPercent = 90;
            mission.RatingStarThresholds[0] = 100; mission.MinimumUpgradeCount = 999;
            Assert.That(sim.AdvanceTo(18).Success, Is.True); Assert.That(sim.TryPurchase("school", "arbitrary-choice0").Success, Is.True);
            Assert.That(sim.AdvanceTo(23).Success, Is.True); Assert.That(sim.TryCompleteMission().Success, Is.True);
        }

        [Test] public void IndependentVerifiedRecordsSurviveReplayAndRejectForgedAdditionalRun()
        {
            var mission = Mission(); var progress = new CampaignProgress(new[] { mission });
            progress.Record(0, Completed(mission, 4));
            int score = progress.BestScore(0); double saving = progress.BestSavingPercent(0);
            Assert.That(progress.Record(0, Completed(mission, 1)), Is.Zero);
            Assert.That(progress.TotalStars, Is.EqualTo(3)); Assert.That(progress.BestScore(0), Is.EqualTo(score));
            Assert.That(progress.BestSavingPercent(0), Is.EqualTo(saving));
            var restored = new CampaignProgress(new[] { mission });
            Assert.That(restored.TryRestore(progress.Export(), out var error), Is.True, error);
            Assert.That(restored.BestScore(0), Is.EqualTo(score)); Assert.That(restored.BestSavingPercent(0), Is.EqualTo(saving));
            var forged = progress.Export(); forged[0].BestSavingRun.History[0].UpgradeId = "invented";
            Assert.That(restored.TryRestore(forged, out _), Is.False); Assert.That(restored.TotalStars, Is.EqualTo(3));
        }

        [Test] public void BestScoreAndBestSavingCanComeFromDifferentVerifiedRuns()
        {
            var mission = Mission(); mission.Problems = new[] { mission.Problems[0] };
            mission.TargetSavingPercent = 5; mission.RationalSpend = 100;
            var progress = new CampaignProgress(new[] { mission });
            progress.Record(0, Completed(mission, 1));
            Assert.That(progress.BestScore(0), Is.EqualTo(100));
            Assert.That(progress.Record(0, Completed(mission, 4)), Is.Zero);
            Assert.That(progress.BestScore(0), Is.EqualTo(100));
            Assert.That(progress.BestSavingPercent(0), Is.EqualTo(41.6666667).Within(.0001));
            Assert.That(progress.Export()[0].BestScoreRun.History.Length, Is.EqualTo(1));
            Assert.That(progress.Export()[0].BestSavingRun.History.Length, Is.EqualTo(4));
            var restored = new CampaignProgress(new[] { mission });
            Assert.That(restored.TryRestore(progress.Export(), out var error), Is.True, error);
            Assert.That(restored.BestScore(0), Is.EqualTo(100));
            Assert.That(restored.BestSavingPercent(0), Is.EqualTo(progress.BestSavingPercent(0)));
            Assert.That(restored.Credits, Is.EqualTo(300));
        }

        [Test] public void OldJsonWithoutOptionalReplaysRestoresVerifiedRecords()
        {
            var mission = Mission(); var run = Completed(mission, 1);
            string oldJson = JsonUtility.ToJson(new LegacyRecords { Records = new[] { new LegacyRecord { MissionId = mission.Id, BestRun = run } } });
            Assert.That(oldJson, Does.Not.Contain("BestScoreRun"));
            var loaded = JsonUtility.FromJson<CampaignSave>(oldJson);
            var progress = new CampaignProgress(new[] { mission });
            Assert.That(progress.TryRestore(loaded.Records, out var error), Is.True, error);
            Assert.That(progress.BestStars(0), Is.EqualTo(1));
            Assert.That(progress.BestScore(0), Is.GreaterThan(0));
            Assert.That(progress.BestSavingPercent(0), Is.GreaterThan(0));
        }

        [TestCase("empty")] [TestCase("mission")] [TestCase("hour")] [TestCase("completed")] [TestCase("version")]
        public void OnlyEntirelyEmptyOptionalReplayIsTreatedAsAbsent(string kind)
        {
            var mission = Mission(); var optional = new CityState();
            if (kind == "mission") optional.MissionId = "forged";
            if (kind == "hour") optional.CurrentHour = 23;
            if (kind == "completed") optional.Completed = true;
            if (kind == "version") optional.Version = 2;
            var progress = new CampaignProgress(new[] { mission });
            bool restored = progress.TryRestore(new[] { new CampaignRecord { MissionId = mission.Id, BestRun = Completed(mission, 1), BestScoreRun = optional, BestSavingRun = new CityState { Version = 0 } } }, out _);
            Assert.That(restored, Is.EqualTo(kind == "empty"));
            Assert.That(progress.TotalStars, Is.EqualTo(kind == "empty" ? 1 : 0));
        }

        [Test] public void LegacyRecordWithoutAdditionalReplaysRemainsReadable()
        {
            var mission = Mission(); var run = Completed(mission, 1);
            var progress = new CampaignProgress(new[] { mission });
            Assert.That(progress.TryRestore(new[] { new CampaignRecord { MissionId = mission.Id, BestRun = run } }, out var error), Is.True, error);
            Assert.That(progress.BestStars(0), Is.EqualTo(1)); Assert.That(progress.BestScore(0), Is.GreaterThan(0));
        }
    }
}

using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Wattshift.Tests
{
    public sealed class G3TimelineTests
    {
        readonly List<UnityEngine.Object> assets = new List<UnityEngine.Object>();

        [TearDown] public void Cleanup()
        {
            foreach (var asset in assets) UnityEngine.Object.DestroyImmediate(asset);
            assets.Clear();
        }

        T Asset<T>() where T : ScriptableObject
        {
            var asset = ScriptableObject.CreateInstance<T>(); assets.Add(asset); return asset;
        }

        UpgradeDefinition Offer(string id, string device = "load", float multiplier = .5f, int cost = 40)
        {
            var offer = Asset<UpgradeDefinition>();
            offer.Id = id; offer.Cost = cost; offer.RequiredAfterHour = 18;
            offer.Effects = new[] { new UpgradeEffect { DeviceId = device, PowerMultiplier = multiplier } };
            return offer;
        }

        MissionDefinition Fixture()
        {
            var building = Asset<BuildingDefinition>(); building.Id = "school";
            building.Devices = new[]
            {
                new DeviceDefinition { Id = "load", PowerKW = 6, StartHour = 17, EndHour = 23 },
                new DeviceDefinition { Id = "critical", PowerKW = 2, StartHour = 17, EndHour = 23, Essential = true, Priority = LoadPriority.Critical }
            };
            building.Upgrades = new[] { Offer("half"), Offer("quarter", multiplier: .5f, cost: 20) };
            var mission = Asset<MissionDefinition>(); mission.Id = "timeline";
            mission.UseTimeline = true; mission.StartHour = 17; mission.EndHour = 23; mission.EvaluationHour = 20;
            mission.InitialBudget = 100; mission.TargetSavingPercent = 25; mission.MinimumUpgradeCount = 1;
            mission.RequiredBuildingIds = new[] { "school" }; mission.StarSavingThresholds = new float[] { 25, 30, 40 };
            mission.MinimumObservationHours = .5f; mission.Buildings = new[] { building };
            return mission;
        }

        static CityState Clone(CityState state) => JsonUtility.FromJson<CityState>(JsonUtility.ToJson(state));
        static string State(CitySimulationService sim) => JsonUtility.ToJson(sim.ExportState());
        static void Advance(CitySimulationService sim, double hour) => Assert.That(sim.AdvanceTo(hour).Success, Is.True);
        static void Buy(CitySimulationService sim, string id = "half") => Assert.That(sim.TryPurchase("school", id).Success, Is.True);
        static SimulationSnapshot Snapshot(CitySimulationService sim) => sim.Evaluate(sim.CurrentHour);

        [TestCase(18, 33, 31.25)]
        [TestCase(22, 45, 6.25)]
        public void EnergyIntegratesOnlyAfterActualPurchase(double purchaseHour, double expectedEnergy, double saving)
        {
            var sim = new CitySimulationService(Fixture()); Advance(sim, purchaseHour);
            var before = Snapshot(sim); Buy(sim);
            Assert.That(Snapshot(sim).AccumulatedEnergyKWh, Is.EqualTo(before.AccumulatedEnergyKWh).Within(1e-9));
            Advance(sim, 23); var result = Snapshot(sim);
            Assert.That(result.AccumulatedBaselineEnergyKWh, Is.EqualTo(48).Within(1e-9));
            Assert.That(result.AccumulatedEnergyKWh, Is.EqualTo(expectedEnergy).Within(1e-9));
            Assert.That(result.ObservedSavingPercent, Is.EqualTo(saving).Within(1e-9));
        }

        [Test] public void SamplingForecastDoesNotAdvanceClockOrAccumulateEnergy()
        {
            var sim = new CitySimulationService(Fixture()); var state = State(sim);
            sim.Evaluate(23); sim.Evaluate(18);
            Assert.That(State(sim), Is.EqualTo(state)); Assert.That(Snapshot(sim).AccumulatedEnergyKWh, Is.Zero);
        }

        [Test] public void CompletionRequiresActualShiftEnd()
        {
            var sim = new CitySimulationService(Fixture()); Advance(sim, 18); Buy(sim); Advance(sim, 22);
            Assert.That(sim.TryCompleteMission().Success, Is.False);
            Advance(sim, 23); Assert.That(sim.TryCompleteMission().Success, Is.True);
        }

        [Test] public void ForecastSavingCannotSatisfyObservedSavingTarget()
        {
            var sim = new CitySimulationService(Fixture()); Advance(sim, 22); Buy(sim); Advance(sim, 23);
            Assert.That(Snapshot(sim).SavingPercent, Is.GreaterThan(25));
            Assert.That(sim.TryCompleteMission().Success, Is.False);
        }

        [Test] public void CompletionRequiresObservationSinceLastTargetUpgrade()
        {
            var mission = Fixture(); mission.TargetSavingPercent = 0;
            var sim = new CitySimulationService(mission); Advance(sim, 22.75); Buy(sim); Advance(sim, 23);
            Assert.That(sim.TryCompleteMission().Success, Is.False);
        }

        [Test] public void CompletionRequiresConfiguredNumberOfPurchases()
        {
            var mission = Fixture(); mission.MinimumUpgradeCount = 2;
            var sim = new CitySimulationService(mission); Advance(sim, 18); Buy(sim); Advance(sim, 23);
            Assert.That(sim.TryCompleteMission().Success, Is.False);
        }

        [Test] public void CompletionRequiresEachTargetBuilding()
        {
            var mission = Fixture();
            var other = Asset<BuildingDefinition>(); other.Id = "clinic";
            other.Devices = new[] { new DeviceDefinition { Id = "load", PowerKW = 0, StartHour = 17, EndHour = 23 } };
            mission.Buildings = new[] { mission.Buildings[0], other }; mission.RequiredBuildingIds = new[] { "school", "clinic" };
            var sim = new CitySimulationService(mission); Advance(sim, 18); Buy(sim); Advance(sim, 23);
            Assert.That(sim.TryCompleteMission().Success, Is.False);
        }

        [Test] public void RestoreReplaysTimestampedEnergyAndBudgetExactly()
        {
            var mission = Fixture(); var sim = new CitySimulationService(mission);
            Advance(sim, 18); Buy(sim); Advance(sim, 20); Buy(sim, "quarter"); Advance(sim, 22);
            var restored = new CitySimulationService(mission); Assert.That(restored.TryRestore(sim.ExportState()).Success, Is.True);
            Assert.That(Snapshot(restored).AccumulatedEnergyKWh, Is.EqualTo(25).Within(1e-9));
            Assert.That(Snapshot(restored).AccumulatedBaselineEnergyKWh, Is.EqualTo(40).Within(1e-9));
            Assert.That(Snapshot(restored).Budget, Is.EqualTo(40));
            Advance(sim, 23); Advance(restored, 23);
            Assert.That(Snapshot(restored).AccumulatedEnergyKWh, Is.EqualTo(Snapshot(sim).AccumulatedEnergyKWh).Within(1e-9));
        }

        [TestCase("order")]
        [TestCase("early")]
        [TestCase("missing")]
        [TestCase("forged")]
        public void InvalidHistoryRestoreIsTransactional(string corruption)
        {
            var mission = Fixture(); var sim = new CitySimulationService(mission);
            Advance(sim, 18); Buy(sim); Advance(sim, 20); Buy(sim, "quarter"); Advance(sim, 21);
            var invalid = Clone(sim.ExportState());
            if (corruption == "order") invalid.History[1].Hour = 17.5;
            if (corruption == "early") invalid.History[0].Hour = 17.5;
            if (corruption == "missing") invalid.History = new[] { invalid.History[0] };
            if (corruption == "forged") invalid.History[1].UpgradeId = "invented";
            var before = State(sim);
            Assert.That(sim.TryRestore(invalid).Success, Is.False); Assert.That(State(sim), Is.EqualTo(before));
        }

        [Test] public void ExclusiveChoiceAndRepeatPurchasePreserveBudget()
        {
            var mission = Fixture(); foreach (var offer in mission.Buildings[0].Upgrades) offer.ExclusiveGroup = "lighting";
            var sim = new CitySimulationService(mission); Advance(sim, 18); Buy(sim); var before = State(sim);
            Assert.That(sim.TryPurchase("school", "quarter").Code, Is.EqualTo("EXCLUSIVE_UPGRADE"));
            Assert.That(sim.TryPurchase("school", "half").Code, Is.EqualTo("ALREADY_PURCHASED"));
            Assert.That(State(sim), Is.EqualTo(before));
        }

        [Test] public void LedReducesPowerWhilePreservingRequiredIllumination()
        {
            var mission = Fixture(); var building = mission.Buildings[0];
            building.Devices[0].IsLighting = true; building.Devices[0].MinimumService = .8f;
            var off = Offer("off", multiplier: 0); var dim = Offer("dim"); dim.Effects[0].ServiceMultiplier = .5f;
            building.Upgrades = new[] { building.Upgrades[0], off, dim };
            var sim = new CitySimulationService(mission); Advance(sim, 18);
            Assert.That(sim.TryPurchase("school", "off").Code, Is.EqualTo("SAFETY_CONSTRAINT"));
            Assert.That(sim.TryPurchase("school", "dim").Code, Is.EqualTo("SAFETY_CONSTRAINT"));
            Buy(sim); var result = Snapshot(sim);
            Assert.That(result.CurrentPowerKW, Is.EqualTo(5).Within(1e-9));
            Assert.That(result.Buildings[0].LightingRatio, Is.EqualTo(1).Within(1e-9));
            Assert.That(result.Buildings[0].ServiceLevel, Is.EqualTo(1).Within(1e-9)); Assert.That(result.SafetySatisfied, Is.True);
        }

        [Test] public void ProtectedCriticalLoadCannotBeReduced()
        {
            var mission = Fixture(); mission.Buildings[0].Upgrades = new[] { Offer("unsafe", "critical") };
            var sim = new CitySimulationService(mission); Advance(sim, 18); var before = State(sim);
            Assert.That(sim.TryPurchase("school", "unsafe").Code, Is.EqualTo("ESSENTIAL_LOAD_PROTECTED"));
            Assert.That(State(sim), Is.EqualTo(before));
        }

        [Test] public void BudgetFailureDoesNotSpendOrInstallUpgrade()
        {
            var mission = Fixture(); mission.InitialBudget = 39;
            var sim = new CitySimulationService(mission); Advance(sim, 18); var before = State(sim);
            Assert.That(sim.TryPurchase("school", "half").Code, Is.EqualTo("INSUFFICIENT_BUDGET"));
            Assert.That(State(sim), Is.EqualTo(before));
        }

        [Test] public void ClockRejectsBackwardBeyondEndAndPurchaseAfterEnd()
        {
            var sim = new CitySimulationService(Fixture()); Advance(sim, 18); var before = State(sim);
            Assert.That(sim.AdvanceTo(17).Success, Is.False); Assert.That(sim.AdvanceTo(24).Success, Is.False);
            Assert.That(State(sim), Is.EqualTo(before)); Advance(sim, 23);
            Assert.That(sim.TryPurchase("school", "half").Code, Is.EqualTo("SHIFT_ENDED"));
        }

        [Test] public void CampaignRewardsOnlyVerifiedBestRunAndCannotBeFarmed()
        {
            var mission = Fixture(); var second = Fixture(); second.Id = "second";
            var campaign = new CampaignProgress(new[] { mission, second }); var sim = new CitySimulationService(mission);
            Assert.That(campaign.IsUnlocked(1), Is.False); Advance(sim, 18); Buy(sim); Advance(sim, 23);
            Assert.That(sim.TryCompleteMission().Success, Is.True);
            int earned = campaign.Record(0, sim.ExportState()); Assert.That(earned, Is.GreaterThan(0));
            Assert.That(campaign.IsUnlocked(1), Is.True); Assert.That(campaign.Record(0, sim.ExportState()), Is.Zero);
            Assert.That(campaign.Credits, Is.EqualTo(earned));
            var restored = new CampaignProgress(new[] { mission, second });
            Assert.That(restored.TryRestore(campaign.Export(), out var error), Is.True, error);
            Assert.That(restored.Credits, Is.EqualTo(earned)); Assert.That(restored.Record(0, sim.ExportState()), Is.Zero);
        }

        [Test] public void ForgedCompletionCannotUnlockNextMission()
        {
            var mission = Fixture(); var second = Fixture(); second.Id = "second";
            var campaign = new CampaignProgress(new[] { mission, second });
            var forged = new CitySimulationService(mission).ExportState(); forged.Completed = true;
            Assert.Throws<ArgumentException>(() => campaign.Record(0, forged));
            Assert.That(campaign.IsUnlocked(1), Is.False); Assert.That(campaign.Credits, Is.Zero);
            Assert.That(campaign.TryRestore(new[] { new CampaignRecord { MissionId = mission.Id, BestRun = forged } }, out _), Is.False);
            Assert.That(campaign.Credits, Is.Zero);
        }
    }
}

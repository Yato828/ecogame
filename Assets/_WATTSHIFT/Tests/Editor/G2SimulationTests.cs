using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Wattshift.Tests
{
    /// <summary>Independent arithmetic and transactional regression checks for the G2 domain.</summary>
    public sealed class G2SimulationTests
    {
        readonly List<UnityEngine.Object> created = new List<UnityEngine.Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (UnityEngine.Object obj in created) UnityEngine.Object.DestroyImmediate(obj);
            created.Clear();
        }

        T Asset<T>() where T : ScriptableObject
        {
            T obj = ScriptableObject.CreateInstance<T>();
            created.Add(obj);
            return obj;
        }

        static DeviceDefinition Device(string id, float power, float start, float end, bool essential = false)
        {
            return new DeviceDefinition { Id = id, PowerKW = power, StartHour = start, EndHour = end, Essential = essential, IsLighting = id == "lights" };
        }

        BuildingDefinition Building(params DeviceDefinition[] devices)
        {
            var building = Asset<BuildingDefinition>();
            building.Id = "school";
            building.Title = "Школа";
            building.BaseComfort = 100;
            building.Devices = devices;
            building.Upgrades = Array.Empty<UpgradeDefinition>();
            return building;
        }

        UpgradeDefinition Upgrade(string id, string deviceId, int cost = 40, float multiplier = .5f, float comfortDelta = 0)
        {
            var upgrade = Asset<UpgradeDefinition>();
            upgrade.Id = id; upgrade.Title = id; upgrade.Description = "Test configuration";
            upgrade.Cost = cost; upgrade.ComfortDelta = comfortDelta;
            upgrade.Effects = new[] { new UpgradeEffect { DeviceId = deviceId, PowerMultiplier = multiplier } };
            return upgrade;
        }

        MissionDefinition Mission(BuildingDefinition building, float start = 17, float end = 23, int budget = 100, float target = 25)
        {
            var mission = Asset<MissionDefinition>();
            mission.Id = "after-hours-test";
            mission.Title = "После занятий";
            mission.Description = "Independent test fixture";
            mission.Assumptions = "Educational example values";
            mission.StartHour = start; mission.EndHour = end; mission.EvaluationHour = start;
            mission.InitialBudget = budget; mission.TargetSavingPercent = target; mission.MinComfort = 80;
            mission.Buildings = new[] { building };
            return mission;
        }

        CitySimulationService Purchasable(out MissionDefinition mission, int cost = 40)
        {
            var building = Building(Device("lights", 6, 17, 23));
            building.Upgrades = new[] { Upgrade("led", "lights", cost) };
            mission = Mission(building);
            return new CitySimulationService(mission);
        }

        static string StateJson(CitySimulationService service) => JsonUtility.ToJson(service.ExportState());
        static CityState Clone(CityState state) => JsonUtility.FromJson<CityState>(JsonUtility.ToJson(state));

        [Test]
        public void SixKilowattsForThreeHoursIsEighteenKilowattHoursAndEndIsExclusive()
        {
            var service = new CitySimulationService(Mission(Building(Device("lights", 6, 17, 20))));
            SimulationSnapshot active = service.Evaluate(19);
            Assert.That(active.CurrentPowerKW, Is.EqualTo(6).Within(1e-6));
            Assert.That(active.CurrentEnergyKWh, Is.EqualTo(18).Within(1e-6));
            SimulationSnapshot ended = service.Evaluate(20);
            Assert.That(ended.CurrentPowerKW, Is.Zero.Within(1e-6));
            Assert.That(ended.CurrentEnergyKWh, Is.EqualTo(18).Within(1e-6), "Forecast window energy does not become instantaneous power");
        }

        [Test]
        public void MidnightScheduleIntegratesBothSidesOfMidnight()
        {
            var service = new CitySimulationService(Mission(Building(Device("lights", 2, 22, 6)), 21, 7));
            Assert.That(service.Evaluate(23).CurrentEnergyKWh, Is.EqualTo(16).Within(1e-6));
            Assert.That(service.Evaluate(5).CurrentPowerKW, Is.EqualTo(2).Within(1e-6));
            Assert.That(service.Evaluate(6).CurrentPowerKW, Is.Zero.Within(1e-6));
        }

        [Test]
        public void FractionalWindowUsesExactHoursRatherThanIntegerTicks()
        {
            var service = new CitySimulationService(Mission(Building(Device("lights", 2, 17, 20)), 17.5f, 19.25f));
            Assert.That(service.Evaluate(18).CurrentEnergyKWh, Is.EqualTo(3.5).Within(1e-6));
        }

        [Test]
        public void FullDayAndEmptySchedulesHaveDistinctMeanings()
        {
            var service = new CitySimulationService(Mission(Building(Device("fridge", 2, 0, 24, true), Device("empty", 99, 0, 0))));
            Assert.That(service.Evaluate(20).CurrentPowerKW, Is.EqualTo(2).Within(1e-6));
            Assert.That(service.Evaluate(20).CurrentEnergyKWh, Is.EqualTo(12).Within(1e-6));
        }

        [Test]
        public void ProtectedLoadCannotUseTheNormalizedEmptyTwentyFourToZeroSchedule()
        {
            MissionDefinition mission = Mission(Building(Device("medical", 8, 24, 0, true)));
            Assert.Throws<ArgumentException>(() => new CitySimulationService(mission));
        }

        [Test]
        public void ScheduleUpgradeRecomputesEnergyAndKeepsEssentialLoad()
        {
            var building = Building(Device("lights", 6, 17, 23), Device("security", 1, 0, 24, true));
            var schedule = Upgrade("schedule", "lights", multiplier: 1);
            schedule.Effects[0].OverrideSchedule = true;
            schedule.Effects[0].StartHour = 18; schedule.Effects[0].EndHour = 19;
            building.Upgrades = new[] { schedule };
            var service = new CitySimulationService(Mission(building));
            Assert.IsTrue(service.TryPurchase("school", "schedule").Success);
            var snapshot = service.Evaluate(20);
            Assert.That(snapshot.BaselineEnergyKWh, Is.EqualTo(42).Within(1e-6));
            Assert.That(snapshot.CurrentEnergyKWh, Is.EqualTo(12).Within(1e-6));
            Assert.That(snapshot.SavedEnergyKWh, Is.EqualTo(30).Within(1e-6));
            Assert.That(snapshot.CurrentPowerKW, Is.EqualTo(1).Within(1e-6));
            Assert.IsTrue(snapshot.SafetySatisfied);
        }

        [Test]
        public void DuplicatePurchaseDoesNotSpendOrEmitAnotherChange()
        {
            var service = Purchasable(out _);
            int changes = 0; service.Changed += () => changes++;
            Assert.IsTrue(service.TryPurchase("school", "led").Success);
            string before = StateJson(service);
            Assert.IsFalse(service.TryPurchase("school", "led").Success);
            Assert.AreEqual(before, StateJson(service));
            Assert.That(service.Evaluate(20).Budget, Is.EqualTo(60));
            Assert.That(changes, Is.EqualTo(1));
        }

        [Test]
        public void InsufficientBudgetRejectsAtomically()
        {
            var service = Purchasable(out _, 101);
            string before = StateJson(service);
            Assert.IsFalse(service.TryPurchase("school", "led").Success);
            Assert.AreEqual(before, StateJson(service));
            Assert.That(service.Evaluate(20).Budget, Is.EqualTo(100));
            Assert.That(service.Evaluate(20).CurrentEnergyKWh, Is.EqualTo(36).Within(1e-6));
        }

        [Test]
        public void EssentialShutdownIsRejectedBeforeSpending()
        {
            var building = Building(Device("medical", 8, 0, 24, true));
            building.Upgrades = new[] { Upgrade("shutdown", "medical", cost: 0, multiplier: 0) };
            var service = new CitySimulationService(Mission(building));
            string before = StateJson(service);
            Assert.IsFalse(service.TryPurchase("school", "shutdown").Success);
            Assert.AreEqual(before, StateJson(service));
            Assert.That(service.Evaluate(20).CurrentPowerKW, Is.EqualTo(8).Within(1e-6));
            Assert.IsTrue(service.Evaluate(20).SafetySatisfied);
        }

        [Test]
        public void ComfortIsAHardPurchaseConstraint()
        {
            var building = Building(Device("lights", 6, 17, 23));
            building.Upgrades = new[] { Upgrade("dark-room", "lights", multiplier: 0, comfortDelta: -30) };
            var service = new CitySimulationService(Mission(building));
            string before = StateJson(service);
            Assert.IsFalse(service.TryPurchase("school", "dark-room").Success);
            Assert.AreEqual(before, StateJson(service));
            Assert.That(service.Evaluate(20).Comfort, Is.EqualTo(100).Within(.001f));
        }

        [Test]
        public void MissionRejectsPrematureCompletionThenAcceptsCalculatedSavings()
        {
            var service = Purchasable(out MissionDefinition mission);
            Assert.IsFalse(service.TryCompleteMission().Success);
            Assert.IsFalse(service.Evaluate(20).Completed);
            Assert.IsTrue(service.TryPurchase("school", "led").Success);
            var snapshot = service.Evaluate(20);
            Assert.That(snapshot.CurrentEnergyKWh, Is.EqualTo(18).Within(1e-6));
            Assert.That(snapshot.SavingPercent, Is.EqualTo(50).Within(1e-6));
            Assert.IsTrue(MissionEvaluator.Evaluate(mission, snapshot).CanComplete);
            Assert.IsTrue(service.TryCompleteMission().Success);
            Assert.IsTrue(service.Evaluate(20).Completed);
        }

        [Test]
        public void RestoreReplaysPurchasesAndRecalculatesBudgetAndEnergy()
        {
            var original = Purchasable(out MissionDefinition mission);
            Assert.IsTrue(original.TryPurchase("school", "led").Success);
            var restored = new CitySimulationService(mission);
            Assert.IsTrue(restored.TryRestore(original.ExportState()).Success);
            Assert.That(restored.Evaluate(20).Budget, Is.EqualTo(60));
            Assert.That(restored.Evaluate(20).CurrentEnergyKWh, Is.EqualTo(18).Within(1e-6));
            Assert.IsTrue(restored.IsPurchased("school", "led"));
        }

        [Test]
        public void UnsupportedSaveVersionsAreRejectedWithoutChangingLiveState()
        {
            var service = Purchasable(out _);
            Assert.IsTrue(service.TryPurchase("school", "led").Success);
            string before = StateJson(service);
            foreach (int version in new[] { 0, 2 })
            {
                CityState invalid = Clone(service.ExportState()); invalid.Version = version;
                Assert.IsFalse(service.TryRestore(invalid).Success);
                Assert.AreEqual(before, StateJson(service));
            }
        }

        [Test]
        public void UnknownOrDuplicateSaveIdsAreRejectedTransactionally()
        {
            var service = Purchasable(out _);
            string before = StateJson(service);
            CityState valid = service.ExportState();
            var invalid = new List<CityState>();
            CityState wrongMission = Clone(valid); wrongMission.MissionId = "other-mission"; invalid.Add(wrongMission);
            CityState wrongBuilding = Clone(valid); wrongBuilding.Buildings[0].BuildingId = "other-building"; invalid.Add(wrongBuilding);
            CityState duplicateBuilding = Clone(valid); duplicateBuilding.Buildings = new[] { duplicateBuilding.Buildings[0], duplicateBuilding.Buildings[0] }; invalid.Add(duplicateBuilding);
            CityState wrongUpgrade = Clone(valid); wrongUpgrade.Buildings[0].UpgradeIds = new[] { "unknown" }; invalid.Add(wrongUpgrade);
            CityState duplicateUpgrade = Clone(valid); duplicateUpgrade.Buildings[0].UpgradeIds = new[] { "led", "led" }; invalid.Add(duplicateUpgrade);
            foreach (CityState candidate in invalid)
            {
                Assert.IsFalse(service.TryRestore(candidate).Success);
                Assert.AreEqual(before, StateJson(service));
                Assert.That(service.Evaluate(20).Budget, Is.EqualTo(100));
            }
        }

        [Test]
        public void SaveCannotForgeCompletedMissionWithoutMeetingRequirements()
        {
            var service = Purchasable(out _);
            CityState forged = Clone(service.ExportState()); forged.Completed = true;
            Assert.IsFalse(service.TryRestore(forged).Success);
            Assert.IsFalse(service.Evaluate(20).Completed);
            Assert.That(service.Evaluate(20).Budget, Is.EqualTo(100));
        }

        [Test]
        public void SimulationSnapshotsConfigurationInsteadOfReadingMutableAssetNumbers()
        {
            var building = Building(Device("lights", 6, 17, 23));
            var service = new CitySimulationService(Mission(building));
            building.Devices[0].PowerKW = 999;
            Assert.That(service.Evaluate(20).BaselinePowerKW, Is.EqualTo(6).Within(1e-6));
            Assert.That(service.Evaluate(20).BaselineEnergyKWh, Is.EqualTo(36).Within(1e-6));
        }
    }
}

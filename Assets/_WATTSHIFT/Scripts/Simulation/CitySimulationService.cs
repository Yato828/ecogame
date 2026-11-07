using System;
using System.Collections.Generic;

namespace Wattshift
{
    /// <summary>
    /// Deterministic district simulation. Numeric configuration is copied on construction;
    /// state contains only stable IDs. Energy is the integral over the configured mission window.
    /// </summary>
    public sealed class CitySimulationService
    {
        sealed class Device
        {
            public string Id;
            public double Power, Start, End;
            public bool Essential, Lighting;
            public Device Copy() => (Device)MemberwiseClone();
        }

        sealed class Effect
        {
            public string DeviceId;
            public double Multiplier, Start, End;
            public bool OverrideSchedule;
        }

        sealed class Offer
        {
            public string Id, Title;
            public int Cost;
            public double ComfortDelta;
            public Effect[] Effects;
        }

        sealed class Building
        {
            public string Id;
            public double Comfort;
            public BuildingDefinition Original;
            public Device[] Devices;
            public readonly Dictionary<string, Offer> Offers = new Dictionary<string, Offer>(StringComparer.Ordinal);
        }

        readonly Building[] buildings;
        readonly Dictionary<string, Building> byId = new Dictionary<string, Building>(StringComparer.Ordinal);
        readonly string missionId;
        readonly double startHour, endHour, observationHour;
        readonly float targetSaving, minimumComfort;
        readonly int initialBudget;
        Dictionary<string, List<string>> purchases;
        bool completed;

        public MissionDefinition Mission { get; }
        public event Action Changed;

        public CitySimulationService(MissionDefinition mission)
        {
            if (mission == null) throw new ArgumentNullException(nameof(mission));
            RequireId(mission.Id, "mission");
            Require(Finite(mission.StartHour) && Finite(mission.EndHour) && mission.StartHour >= 0 && mission.StartHour < 24 && mission.EndHour >= 0 && mission.EndHour <= 48 && mission.EndHour != mission.StartHour, "Mission window must have positive duration within 0..48.");
            double normalizedEnd = mission.EndHour < mission.StartHour ? mission.EndHour + 24 : mission.EndHour;
            double normalizedObservation = mission.EvaluationHour < mission.StartHour ? mission.EvaluationHour + 24 : mission.EvaluationHour;
            Require(normalizedEnd - mission.StartHour <= 24, "Mission window cannot exceed 24 hours.");
            Require(Finite(mission.EvaluationHour) && mission.EvaluationHour >= 0 && normalizedObservation >= mission.StartHour && normalizedObservation <= normalizedEnd, "Observation hour must be inside mission window.");
            Require(mission.InitialBudget >= 0, "Initial budget cannot be negative.");
            Require(Percent(mission.TargetSavingPercent) && Percent(mission.MinComfort), "Mission target and comfort must be finite percentages.");
            Require(mission.Buildings != null && mission.Buildings.Length > 0, "Mission needs buildings.");
            Mission = mission; missionId = mission.Id;
            startHour = mission.StartHour; endHour = normalizedEnd; observationHour = normalizedObservation;
            targetSaving = mission.TargetSavingPercent; minimumComfort = mission.MinComfort; initialBudget = mission.InitialBudget;
            buildings = new Building[mission.Buildings.Length];
            for (int i = 0; i < buildings.Length; i++)
            {
                BuildingDefinition definition = mission.Buildings[i];
                Require(definition != null, "Null building definition."); RequireId(definition.Id, "building");
                Require(!byId.ContainsKey(definition.Id), "Duplicate building ID: " + definition.Id);
                Require(Percent(definition.BaseComfort), "Building comfort must be a finite percentage.");
                Require(definition.Devices != null && definition.Devices.Length > 0, "Building needs devices: " + definition.Id);
                var building = new Building { Id = definition.Id, Original = definition, Comfort = definition.BaseComfort, Devices = new Device[definition.Devices.Length] };
                var deviceIds = new HashSet<string>(StringComparer.Ordinal);
                for (int j = 0; j < building.Devices.Length; j++)
                {
                    DeviceDefinition d = definition.Devices[j];
                    Require(d != null, "Null device definition."); RequireId(d.Id, "device");
                    Require(deviceIds.Add(d.Id), "Duplicate device ID: " + d.Id);
                    Require(Finite(d.PowerKW) && d.PowerKW >= 0, "Device power must be finite and nonnegative.");
                    Require(ScheduleValid(d.StartHour, d.EndHour), "Device schedule must be within 0..24.");
                    double duration = d.EndHour - d.StartHour;
                    if (duration < 0) duration += 24;
                    Require(!d.Essential || (d.PowerKW > 0 && duration > 0), "Protected devices need a nonzero baseline load and schedule.");
                    building.Devices[j] = new Device { Id = d.Id, Power = d.PowerKW, Start = d.StartHour, End = d.EndHour, Essential = d.Essential, Lighting = d.IsLighting };
                }
                foreach (UpgradeDefinition upgrade in definition.Upgrades ?? Array.Empty<UpgradeDefinition>())
                {
                    Require(upgrade != null, "Null upgrade definition."); RequireId(upgrade.Id, "upgrade");
                    Require(!building.Offers.ContainsKey(upgrade.Id), "Duplicate upgrade ID: " + upgrade.Id);
                    Require(upgrade.Cost >= 0 && Finite(upgrade.ComfortDelta), "Upgrade cost/comfort invalid.");
                    UpgradeEffect[] source = upgrade.Effects ?? Array.Empty<UpgradeEffect>();
                    var offer = new Offer { Id = upgrade.Id, Title = upgrade.Title, Cost = upgrade.Cost, ComfortDelta = upgrade.ComfortDelta, Effects = new Effect[source.Length] };
                    var targets = new HashSet<string>(StringComparer.Ordinal);
                    for (int j = 0; j < source.Length; j++)
                    {
                        UpgradeEffect e = source[j];
                        Require(e != null && !string.IsNullOrWhiteSpace(e.DeviceId) && deviceIds.Contains(e.DeviceId), "Upgrade references unknown device.");
                        Require(targets.Add(e.DeviceId), "Upgrade repeats a device effect.");
                        Require(Finite(e.PowerMultiplier) && e.PowerMultiplier >= 0 && ScheduleValid(e.StartHour, e.EndHour), "Invalid upgrade effect.");
                        offer.Effects[j] = new Effect { DeviceId = e.DeviceId, Multiplier = e.PowerMultiplier, OverrideSchedule = e.OverrideSchedule, Start = e.StartHour, End = e.EndHour };
                    }
                    building.Offers.Add(offer.Id, offer);
                }
                buildings[i] = building; byId.Add(building.Id, building);
            }
            purchases = EmptyPurchases();
        }

        public BuildingDefinition GetBuildingDefinition(string id) => id != null && byId.TryGetValue(id, out Building b) ? b.Original : null;
        public bool IsPurchased(string buildingId, string upgradeId) => buildingId != null && purchases.TryGetValue(buildingId, out List<string> list) && list.Contains(upgradeId);

        public SimulationSnapshot Evaluate(double hour)
        {
            if (!Finite(hour)) throw new ArgumentOutOfRangeException(nameof(hour));
            return EvaluateState(purchases, completed, hour);
        }

        public ActionResult TryPurchase(string buildingId, string upgradeId)
        {
            if (completed) return ActionResult.Fail("MISSION_COMPLETED", "Миссия уже завершена. Начните заново для других решений.");
            ActionResult result = ValidatePurchase(purchases, buildingId, upgradeId);
            if (!result.Success) return result;
            purchases[buildingId].Add(upgradeId);
            Changed?.Invoke();
            return ActionResult.Ok("Модернизация выполнена.");
        }

        public ActionResult TryCompleteMission()
        {
            if (completed) return ActionResult.Fail("MISSION_COMPLETED", "Миссия уже завершена.");
            MissionEvaluation evaluation = MissionEvaluator.Evaluate(EvaluateState(purchases, false, observationHour), targetSaving, minimumComfort, initialBudget);
            if (!evaluation.CanComplete) return ActionResult.Fail("REQUIREMENTS_NOT_MET", string.Join(" ", evaluation.Reasons));
            completed = true;
            Changed?.Invoke();
            return ActionResult.Ok("Миссия выполнена. Все обязательные условия соблюдены.");
        }

        public CityState ExportState()
        {
            var state = new CityState { Version = 1, MissionId = missionId, Completed = completed, Buildings = new BuildingState[buildings.Length] };
            for (int i = 0; i < buildings.Length; i++) state.Buildings[i] = new BuildingState { BuildingId = buildings[i].Id, UpgradeIds = purchases[buildings[i].Id].ToArray() };
            return state;
        }

        public ActionResult TryRestore(CityState state)
        {
            if (state == null) return ActionResult.Fail("INVALID_SAVE", "Файл сохранения не содержит состояния города.");
            if (state.Version != 1) return ActionResult.Fail("UNSUPPORTED_SAVE_VERSION", "Версия сохранения не поддерживается.");
            if (state.MissionId != missionId) return ActionResult.Fail("MISSION_MISMATCH", "Сохранение относится к другой миссии.");
            if (state.Buildings == null || state.Buildings.Length != buildings.Length) return ActionResult.Fail("INVALID_SAVE", "Набор зданий в сохранении не совпадает с миссией.");
            var candidate = EmptyPurchases();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            // Validate the entire shape before replay; no live state is touched until all checks pass.
            foreach (BuildingState saved in state.Buildings)
            {
                if (saved == null || string.IsNullOrWhiteSpace(saved.BuildingId) || !byId.ContainsKey(saved.BuildingId) || !ids.Add(saved.BuildingId) || saved.UpgradeIds == null)
                    return ActionResult.Fail("INVALID_SAVE", "Повреждены идентификаторы зданий или улучшений.");
                var upgrades = new HashSet<string>(StringComparer.Ordinal);
                foreach (string id in saved.UpgradeIds)
                    if (string.IsNullOrWhiteSpace(id) || !upgrades.Add(id) || !byId[saved.BuildingId].Offers.ContainsKey(id))
                        return ActionResult.Fail("INVALID_SAVE", "Сохранение содержит неизвестное или повторное улучшение.");
            }
            foreach (BuildingState saved in state.Buildings)
                foreach (string id in saved.UpgradeIds)
                {
                    ActionResult result = ValidatePurchase(candidate, saved.BuildingId, id);
                    if (!result.Success) return ActionResult.Fail("INVALID_SAVE", "Сохранение нарушает правила: " + result.Message);
                    candidate[saved.BuildingId].Add(id);
                }
            if (state.Completed && !MissionEvaluator.Evaluate(EvaluateState(candidate, false, observationHour), targetSaving, minimumComfort, initialBudget).CanComplete)
                return ActionResult.Fail("INVALID_SAVE", "Завершённая миссия в сохранении не выполняет требования.");
            purchases = candidate; completed = state.Completed;
            Changed?.Invoke();
            return ActionResult.Ok("Прогресс восстановлен.");
        }

        Dictionary<string, List<string>> EmptyPurchases()
        {
            var result = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            foreach (Building b in buildings) result.Add(b.Id, new List<string>());
            return result;
        }

        ActionResult ValidatePurchase(Dictionary<string, List<string>> state, string buildingId, string upgradeId)
        {
            if (buildingId == null || !byId.TryGetValue(buildingId, out Building b)) return ActionResult.Fail("UNKNOWN_BUILDING", "Здание не найдено.");
            if (upgradeId == null || !b.Offers.TryGetValue(upgradeId, out Offer offer)) return ActionResult.Fail("UNKNOWN_UPGRADE", "Улучшение недоступно этому зданию.");
            if (state[buildingId].Contains(upgradeId)) return ActionResult.Fail("ALREADY_PURCHASED", "Это улучшение уже приобретено.");
            foreach (Effect effect in offer.Effects)
                foreach (Device device in b.Devices)
                    if (device.Id == effect.DeviceId && device.Essential && (effect.Multiplier != 1 || (effect.OverrideSchedule && (effect.Start != device.Start || effect.End != device.End))))
                        return ActionResult.Fail("ESSENTIAL_LOAD_PROTECTED", "Нельзя отключать или сокращать питание защищённых потребителей.");
            var proposed = new List<string>(state[buildingId]) { upgradeId };
            if (ComfortOf(b, proposed) + 0.0001 < minimumComfort) return ActionResult.Fail("COMFORT_CONSTRAINT", "Улучшение снизит комфорт ниже допустимого уровня.");
            foreach (Device device in EffectiveDevices(b, proposed))
                if (!Finite(device.Power)) return ActionResult.Fail("INVALID_UPGRADE_EFFECT", "Результат улучшения выходит за допустимый диапазон.");
            if (Spent(state) + offer.Cost > initialBudget) return ActionResult.Fail("INSUFFICIENT_BUDGET", "Недостаточно бюджета для этого улучшения.");
            return ActionResult.Ok();
        }

        long Spent(Dictionary<string, List<string>> state)
        {
            long total = 0;
            foreach (Building b in buildings) foreach (string id in state[b.Id]) total += b.Offers[id].Cost;
            return total;
        }

        float ComfortOf(Building b, List<string> upgrades)
        {
            double comfort = b.Comfort;
            foreach (string id in upgrades) comfort += b.Offers[id].ComfortDelta;
            return (float)Math.Max(0, Math.Min(100, comfort));
        }

        Device[] EffectiveDevices(Building b, List<string> upgrades)
        {
            var devices = new Device[b.Devices.Length];
            for (int i = 0; i < devices.Length; i++) devices[i] = b.Devices[i].Copy();
            foreach (string id in upgrades)
                foreach (Effect effect in b.Offers[id].Effects)
                    foreach (Device d in devices)
                        if (d.Id == effect.DeviceId)
                        {
                            d.Power *= effect.Multiplier;
                            if (effect.OverrideSchedule) { d.Start = effect.Start; d.End = effect.End; }
                        }
            return devices;
        }

        SimulationSnapshot EvaluateState(Dictionary<string, List<string>> state, bool isCompleted, double hour)
        {
            var snapshot = new SimulationSnapshot { Hour = hour, Budget = (int)(initialBudget - Spent(state)), Comfort = 100, SafetySatisfied = true, Completed = isCompleted, Buildings = new BuildingMetrics[buildings.Length] };
            for (int i = 0; i < buildings.Length; i++)
            {
                Building b = buildings[i]; Device[] effective = EffectiveDevices(b, state[b.Id]);
                var metrics = new BuildingMetrics { BuildingId = b.Id, Comfort = ComfortOf(b, state[b.Id]), HasUpgrade = state[b.Id].Count > 0 };
                double baselineLighting = 0, currentLighting = 0;
                for (int j = 0; j < b.Devices.Length; j++)
                {
                    Device baseline = b.Devices[j], current = effective[j];
                    double bp = ActiveAt(baseline, hour) ? baseline.Power : 0;
                    double cp = ActiveAt(current, hour) ? current.Power : 0;
                    metrics.BaselinePowerKW += bp; metrics.CurrentPowerKW += cp;
                    metrics.BaselineEnergyKWh += baseline.Power * ActiveHours(baseline, startHour, endHour);
                    metrics.CurrentEnergyKWh += current.Power * ActiveHours(current, startHour, endHour);
                    if (baseline.Lighting) { baselineLighting += bp; currentLighting += cp; }
                    if (baseline.Essential && (current.Power != baseline.Power || current.Start != baseline.Start || current.End != baseline.End)) snapshot.SafetySatisfied = false;
                }
                metrics.LightingRatio = baselineLighting > 0 ? currentLighting / baselineLighting : 0;
                snapshot.Buildings[i] = metrics;
                snapshot.BaselinePowerKW += metrics.BaselinePowerKW; snapshot.CurrentPowerKW += metrics.CurrentPowerKW;
                snapshot.BaselineEnergyKWh += metrics.BaselineEnergyKWh; snapshot.CurrentEnergyKWh += metrics.CurrentEnergyKWh;
                snapshot.Comfort = Math.Min(snapshot.Comfort, metrics.Comfort);
            }
            snapshot.SavedEnergyKWh = snapshot.BaselineEnergyKWh - snapshot.CurrentEnergyKWh;
            snapshot.SavingPercent = snapshot.BaselineEnergyKWh > 0 ? snapshot.SavedEnergyKWh / snapshot.BaselineEnergyKWh * 100 : 0;
            return snapshot;
        }

        static bool ActiveAt(Device device, double hour)
        {
            if (device.Start == device.End) return false;
            double h = hour % 24; if (h < 0) h += 24;
            return device.Start < device.End ? h >= device.Start && h < device.End : h >= device.Start || h < device.End;
        }

        static double ActiveHours(Device device, double from, double to)
        {
            if (device.Start == device.End) return 0;
            double total = 0;
            int firstDay = (int)Math.Floor(from / 24) - 1;
            int lastDay = (int)Math.Floor(to / 24);
            for (int day = firstDay; day <= lastDay; day++)
            {
                double begin = day * 24 + device.Start;
                double end = day * 24 + device.End + (device.End < device.Start ? 24 : 0);
                total += Math.Max(0, Math.Min(to, end) - Math.Max(from, begin));
            }
            return total;
        }

        static bool ScheduleValid(double start, double end) => Finite(start) && Finite(end) && start >= 0 && start <= 24 && end >= 0 && end <= 24;
        static bool Percent(double value) => Finite(value) && value >= 0 && value <= 100;
        static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
        static void Require(bool condition, string message) { if (!condition) throw new ArgumentException(message); }
        static void RequireId(string id, string kind) { Require(!string.IsNullOrWhiteSpace(id), "Missing " + kind + " ID."); }
    }
}

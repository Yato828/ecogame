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
            public string Id, Title;
            public LoadPriority Priority;
            public double Power, Start, End, Service = 1, MinimumService;
            public bool Essential, Lighting;
            public Device Copy() => (Device)MemberwiseClone();
        }

        sealed class Effect
        {
            public string DeviceId;
            public double Multiplier, Start, End, ServiceMultiplier;
            public bool OverrideSchedule;
        }

        sealed class Offer
        {
            public string Id, Title, ExclusiveGroup;
            public double RequiredAfterHour;
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
        readonly float minimumComfort;
        readonly int initialBudget;
        readonly MissionRules rules;
        readonly bool useTimeline;
        readonly List<UpgradePurchaseRecord> history = new List<UpgradePurchaseRecord>();
        double currentHour, accumulatedEnergy, accumulatedBaseline;
        Dictionary<string, List<string>> purchases;
        bool completed;

        public MissionDefinition Mission { get; }
        public double CurrentHour => currentHour;
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
            minimumComfort = mission.MinComfort; initialBudget = mission.InitialBudget;
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
                    Require(Finite(d.MinimumService) && d.MinimumService >= 0 && d.MinimumService <= 1, "Minimum service must be 0..1.");
                    Require(d.Priority != LoadPriority.Critical || d.Essential || d.MinimumService > 0, "Critical devices need explicit load or service protection.");
                    Require(ScheduleValid(d.StartHour, d.EndHour), "Device schedule must be within 0..24.");
                    double duration = d.EndHour - d.StartHour;
                    if (duration < 0) duration += 24;
                    Require(!d.Essential || (d.PowerKW > 0 && duration > 0), "Protected devices need a nonzero baseline load and schedule.");
                    building.Devices[j] = new Device { Id = d.Id, Title = d.Title, Priority = d.Priority, Power = d.PowerKW, Start = d.StartHour, End = d.EndHour, Essential = d.Essential, Lighting = d.IsLighting, MinimumService = d.MinimumService };
                }
                foreach (UpgradeDefinition upgrade in definition.Upgrades ?? Array.Empty<UpgradeDefinition>())
                {
                    Require(upgrade != null, "Null upgrade definition."); RequireId(upgrade.Id, "upgrade");
                    Require(!building.Offers.ContainsKey(upgrade.Id), "Duplicate upgrade ID: " + upgrade.Id);
                    Require(Finite(upgrade.RequiredAfterHour) && upgrade.RequiredAfterHour >= -1 && upgrade.RequiredAfterHour <= 48, "Invalid offer availability hour.");
                    Require(upgrade.Cost >= 0 && Finite(upgrade.ComfortDelta), "Upgrade cost/comfort invalid.");
                    UpgradeEffect[] source = upgrade.Effects ?? Array.Empty<UpgradeEffect>();
                    double requiredHour = upgrade.RequiredAfterHour;
                    if (requiredHour >= 0 && requiredHour < startHour) requiredHour += 24;
                    var offer = new Offer { Id = upgrade.Id, Title = upgrade.Title, Cost = upgrade.Cost, ComfortDelta = upgrade.ComfortDelta, ExclusiveGroup = upgrade.ExclusiveGroup ?? "", RequiredAfterHour = requiredHour, Effects = new Effect[source.Length] };
                    var targets = new HashSet<string>(StringComparer.Ordinal);
                    for (int j = 0; j < source.Length; j++)
                    {
                        UpgradeEffect e = source[j];
                        Require(e != null && !string.IsNullOrWhiteSpace(e.DeviceId) && deviceIds.Contains(e.DeviceId), "Upgrade references unknown device.");
                        Require(targets.Add(e.DeviceId), "Upgrade repeats a device effect.");
                        Require(Finite(e.ServiceMultiplier) && e.ServiceMultiplier >= 0 && e.ServiceMultiplier <= 1 && Finite(e.PowerMultiplier) && e.PowerMultiplier >= 0 && ScheduleValid(e.StartHour, e.EndHour), "Invalid upgrade effect.");
                        offer.Effects[j] = new Effect { DeviceId = e.DeviceId, Multiplier = e.PowerMultiplier, ServiceMultiplier = e.ServiceMultiplier, OverrideSchedule = e.OverrideSchedule, Start = e.StartHour, End = e.EndHour };
                    }
                    building.Offers.Add(offer.Id, offer);
                }
                buildings[i] = building; byId.Add(building.Id, building);
            }
            useTimeline = mission.UseTimeline;
            rules = MissionRules.From(mission, normalizedEnd);
            Require(!useTimeline || (Finite(mission.RealDurationSeconds) && mission.RealDurationSeconds > 0), "Timeline duration must be positive.");
            Require(Finite(mission.MinimumObservationHours) && mission.MinimumObservationHours >= 0 && (!useTimeline || mission.MinimumObservationHours <= normalizedEnd - startHour), "Observation duration must fit the mission window.");
            Require(mission.MinimumUpgradeCount >= 0 && mission.BaseReward >= 0 && mission.BaseReward <= int.MaxValue / 3, "Invalid mission count/reward.");
            Require(rules.StarThresholds.Length == 3 && Percent(rules.StarThresholds[0]) && Percent(rules.StarThresholds[1]) && Percent(rules.StarThresholds[2]) && rules.StarThresholds[0] <= rules.StarThresholds[1] && rules.StarThresholds[1] <= rules.StarThresholds[2], "Three ascending star thresholds required.");
            var requiredIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (string id in rules.RequiredBuildingIds) Require(id != null && byId.ContainsKey(id) && requiredIds.Add(id), "Invalid required building ID.");
            if (rules.DecisionRating)
            {
                Require(Percent(rules.MinimumUsefulSavingPercent), "Minimum useful saving must be a finite percentage.");
                Require(rules.RatingThresholds.Length == 3 && rules.RatingThresholds[0] >= 0 && rules.RatingThresholds[2] <= 100 && rules.RatingThresholds[0] <= rules.RatingThresholds[1] && rules.RatingThresholds[1] <= rules.RatingThresholds[2], "Invalid rating thresholds.");
                Require(Finite(rules.ProblemWeight) && rules.ProblemWeight >= 0 && Finite(rules.EnergyWeight) && rules.EnergyWeight >= 0 && Finite(rules.SafetyWeight) && rules.SafetyWeight >= 0 && Finite(rules.BudgetWeight) && rules.BudgetWeight >= 0 && rules.ProblemWeight + rules.EnergyWeight + rules.SafetyWeight + rules.BudgetWeight > 0 && rules.RationalSpend >= 0, "Invalid rating weights.");
                var problemIds = new HashSet<string>(StringComparer.Ordinal);
                foreach (var problem in rules.Problems)
                {
                    Require(problem != null && !string.IsNullOrWhiteSpace(problem.Id) && problemIds.Add(problem.Id) && problem.BuildingId != null && byId.ContainsKey(problem.BuildingId) && Percent(problem.MinimumReductionPercent) && problem.DeviceIds.Length > 0, "Invalid mission problem.");
                    var devices = new HashSet<string>(StringComparer.Ordinal);
                    foreach (string id in problem.DeviceIds)
                        Require(!string.IsNullOrWhiteSpace(id) && devices.Add(id) && Array.Exists(byId[problem.BuildingId].Devices, d => d.Id == id), "Invalid problem device.");
                }
            }
            purchases = EmptyPurchases();
            currentHour = startHour;
        }

        public BuildingDefinition GetBuildingDefinition(string id) => id != null && byId.TryGetValue(id, out Building b) ? b.Original : null;
        public bool IsPurchased(string buildingId, string upgradeId) => buildingId != null && purchases.TryGetValue(buildingId, out List<string> list) && list.Contains(upgradeId);

        public SimulationSnapshot Evaluate(double hour)
        {
            if (!Finite(hour)) throw new ArgumentOutOfRangeException(nameof(hour));
            return EvaluateState(purchases, completed, hour);
        }

        /// <summary>Integrates the already installed state; later purchases never rewrite past energy.</summary>
        public ActionResult AdvanceTo(double hour)
        {
            if (!useTimeline) return ActionResult.Fail("TIMELINE_DISABLED", "У этой миссии нет игрового времени.");
            if (!Finite(hour) || hour < currentHour || hour > endHour) return ActionResult.Fail("INVALID_TIME", "Нельзя перемещать время назад или за пределы смены.");
            if (completed && hour != currentHour) return ActionResult.Fail("MISSION_COMPLETED", "Смена уже завершена.");
            Integrate(purchases, currentHour, hour, ref accumulatedBaseline, ref accumulatedEnergy);
            currentHour = hour;
            Changed?.Invoke();
            return ActionResult.Ok();
        }

        /// <summary>Read-only forecast for the remaining shift; uses exactly the purchase validation and integrator.</summary>
        public UpgradePreview PreviewPurchase(string buildingId,string upgradeId)
        {
            var validation=ValidatePurchase(purchases,buildingId,upgradeId,currentHour);
            if(!validation.Success)return new UpgradePreview { Available=false,Reason=validation.Message };
            var candidate=EmptyPurchases();
            foreach(var pair in purchases)candidate[pair.Key].AddRange(pair.Value);
            candidate[buildingId].Add(upgradeId);
            double baselineBefore=0,before=0,baselineAfter=0,after=0;
            Integrate(purchases,currentHour,endHour,ref baselineBefore,ref before);
            Integrate(candidate,currentHour,endHour,ref baselineAfter,ref after);
            var snapshot=EvaluateState(candidate,false,currentHour);
            return new UpgradePreview { Available=true,Safe=snapshot.SafetySatisfied && snapshot.Comfort>=minimumComfort,SavedEnergyKWh=before-after,CurrentPowerKW=snapshot.CurrentPowerKW };
        }

        public ActionResult TryPurchase(string buildingId, string upgradeId)
        {
            if (completed) return ActionResult.Fail("MISSION_COMPLETED", "Миссия уже завершена. Начните заново для других решений.");
            ActionResult result = ValidatePurchase(purchases, buildingId, upgradeId, currentHour);
            if (!result.Success) return result;
            if (useTimeline && currentHour >= endHour) return ActionResult.Fail("SHIFT_ENDED", "Смена окончена: изменения не могут повлиять на прошедшее время.");
            purchases[buildingId].Add(upgradeId);
            if (useTimeline) history.Add(new UpgradePurchaseRecord { BuildingId = buildingId, UpgradeId = upgradeId, Hour = currentHour });
            Changed?.Invoke();
            return ActionResult.Ok("Модернизация выполнена.");
        }

        public ActionResult TryCompleteMission()
        {
            if (completed) return ActionResult.Fail("MISSION_COMPLETED", "Миссия уже завершена.");
            MissionEvaluation evaluation = MissionEvaluator.Evaluate(EvaluateState(purchases, false, useTimeline ? currentHour : observationHour), rules);
            if (!evaluation.CanComplete) return ActionResult.Fail("REQUIREMENTS_NOT_MET", string.Join(" ", evaluation.Reasons));
            completed = true;
            Changed?.Invoke();
            return ActionResult.Ok("Миссия выполнена. Все обязательные условия соблюдены.");
        }

        public CityState ExportState()
        {
            var state = new CityState { Version = useTimeline ? 2 : 1, MissionId = missionId, Completed = completed, Buildings = new BuildingState[buildings.Length], CurrentHour = currentHour, History = new UpgradePurchaseRecord[history.Count] };
            for (int i = 0; i < history.Count; i++) state.History[i] = new UpgradePurchaseRecord { BuildingId = history[i].BuildingId, UpgradeId = history[i].UpgradeId, Hour = history[i].Hour };
            for (int i = 0; i < buildings.Length; i++) state.Buildings[i] = new BuildingState { BuildingId = buildings[i].Id, UpgradeIds = purchases[buildings[i].Id].ToArray() };
            return state;
        }

        public ActionResult TryRestore(CityState state)
        {
            if (state == null) return ActionResult.Fail("INVALID_SAVE", "Файл сохранения не содержит состояния города.");
            if (state.Version != (useTimeline ? 2 : 1)) return ActionResult.Fail("UNSUPPORTED_SAVE_VERSION", "Версия сохранения не поддерживается этой миссией.");
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
            double restoredHour = startHour, restoredBaseline = 0, restoredEnergy = 0;
            var restoredHistory = new List<UpgradePurchaseRecord>();
            if (useTimeline)
            {
                if (!Finite(state.CurrentHour) || state.CurrentHour < startHour || state.CurrentHour > endHour || state.History == null)
                    return ActionResult.Fail("INVALID_SAVE", "Повреждено время смены или история решений.");
                foreach (UpgradePurchaseRecord record in state.History)
                {
                    if (record == null || !Finite(record.Hour) || record.Hour < restoredHour || record.Hour >= endHour || record.Hour > state.CurrentHour)
                        return ActionResult.Fail("INVALID_SAVE", "Нарушен порядок времени установки улучшений.");
                    ActionResult result = ValidatePurchase(candidate, record.BuildingId, record.UpgradeId, record.Hour);
                    if (!result.Success) return ActionResult.Fail("INVALID_SAVE", "История решений нарушает правила: " + result.Message);
                    Integrate(candidate, restoredHour, record.Hour, ref restoredBaseline, ref restoredEnergy);
                    restoredHour = record.Hour;
                    candidate[record.BuildingId].Add(record.UpgradeId);
                    restoredHistory.Add(new UpgradePurchaseRecord { BuildingId = record.BuildingId, UpgradeId = record.UpgradeId, Hour = record.Hour });
                }
                Integrate(candidate, restoredHour, state.CurrentHour, ref restoredBaseline, ref restoredEnergy);
                restoredHour = state.CurrentHour;
                foreach (BuildingState saved in state.Buildings)
                    if (!SameIds(candidate[saved.BuildingId], saved.UpgradeIds)) return ActionResult.Fail("INVALID_SAVE", "История не совпадает с установленными улучшениями.");
            }
            else
            {
                foreach (BuildingState saved in state.Buildings)
                    foreach (string id in saved.UpgradeIds)
                    {
                        ActionResult result = ValidatePurchase(candidate, saved.BuildingId, id, startHour);
                        if (!result.Success) return ActionResult.Fail("INVALID_SAVE", "Сохранение нарушает правила: " + result.Message);
                        candidate[saved.BuildingId].Add(id);
                    }
            }
            SimulationSnapshot restored = EvaluateState(candidate, false, useTimeline ? restoredHour : observationHour);
            ApplyTimelineMetrics(restored, restoredHour, restoredBaseline, restoredEnergy);
            restored.LastUpgradeHour = LastUpgrade(restoredHistory);
            if (state.Completed && !MissionEvaluator.Evaluate(restored, rules).CanComplete)
                return ActionResult.Fail("INVALID_SAVE", "Завершённая миссия в сохранении не выполняет требования.");
            purchases = candidate; completed = state.Completed;
            currentHour = restoredHour; accumulatedBaseline = restoredBaseline; accumulatedEnergy = restoredEnergy;
            history.Clear(); history.AddRange(restoredHistory);
            Changed?.Invoke();
            return ActionResult.Ok("Прогресс восстановлен.");
        }

        Dictionary<string, List<string>> EmptyPurchases()
        {
            var result = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            foreach (Building b in buildings) result.Add(b.Id, new List<string>());
            return result;
        }

        ActionResult ValidatePurchase(Dictionary<string, List<string>> state, string buildingId, string upgradeId, double atHour)
        {
            if (buildingId == null || !byId.TryGetValue(buildingId, out Building b)) return ActionResult.Fail("UNKNOWN_BUILDING", "Здание не найдено.");
            if (upgradeId == null || !b.Offers.TryGetValue(upgradeId, out Offer offer)) return ActionResult.Fail("UNKNOWN_UPGRADE", "Улучшение недоступно этому зданию.");
            if (useTimeline && offer.RequiredAfterHour >= 0 && atHour + 1e-9 < offer.RequiredAfterHour) return ActionResult.Fail("NOT_YET_APPLICABLE", "Это решение применимо позднее в смене.");
            if (state[buildingId].Contains(upgradeId)) return ActionResult.Fail("ALREADY_PURCHASED", "Это улучшение уже приобретено.");
            foreach (string installed in state[buildingId])
                if (!string.IsNullOrEmpty(offer.ExclusiveGroup) && b.Offers[installed].ExclusiveGroup == offer.ExclusiveGroup)
                    return ActionResult.Fail("EXCLUSIVE_UPGRADE", "Для этой системы уже выбран другой способ модернизации.");
            foreach (Effect effect in offer.Effects)
                foreach (Device device in b.Devices)
                    if (device.Id == effect.DeviceId && device.Essential && (effect.Multiplier != 1 || effect.ServiceMultiplier != 1 || (effect.OverrideSchedule && (effect.Start != device.Start || effect.End != device.End))))
                        return ActionResult.Fail("ESSENTIAL_LOAD_PROTECTED", "Нельзя отключать или сокращать питание защищённых потребителей.");
            var proposed = new List<string>(state[buildingId]) { upgradeId };
            if (ComfortOf(b, proposed) + 0.0001 < minimumComfort) return ActionResult.Fail("COMFORT_CONSTRAINT", "Улучшение снизит комфорт ниже допустимого уровня.");
            Device[] effective = EffectiveDevices(b, proposed);
            for (int i = 0; i < effective.Length; i++)
            {
                Device device = effective[i];
                if (!Finite(device.Power) || !Finite(device.Service) || !Finite(device.Power * 24)) return ActionResult.Fail("INVALID_UPGRADE_EFFECT", "Результат улучшения выходит за допустимый диапазон.");
                if (!ServiceSafe(b.Devices[i], device, startHour, endHour)) return ActionResult.Fail("SAFETY_CONSTRAINT", "Недостаточный уровень необходимого освещения или работы системы.");
            }
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
                            d.Service *= effect.ServiceMultiplier;
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
                var metrics = new BuildingMetrics { BuildingId = b.Id, Comfort = ComfortOf(b, state[b.Id]), HasUpgrade = state[b.Id].Count > 0, Devices = new DeviceMetrics[b.Devices.Length] };
                double baselineLighting = 0, currentLighting = 0;
                for (int j = 0; j < b.Devices.Length; j++)
                {
                    Device baseline = b.Devices[j], current = effective[j];
                    double bp = ActiveAt(baseline, hour) ? baseline.Power : 0;
                    double cp = ActiveAt(current, hour) ? current.Power : 0;
                    metrics.Devices[j] = new DeviceMetrics { Id = current.Id, Title = current.Title, Priority = current.Priority, Essential = current.Essential, CurrentPowerKW = cp, BaselineEnergyKWh = baseline.Power * ActiveHours(baseline, startHour, endHour), CurrentEnergyKWh = current.Power * ActiveHours(current, startHour, endHour), ServiceLevel = cp > 0 ? current.Service : 0 };
                    metrics.BaselinePowerKW += bp; metrics.CurrentPowerKW += cp;
                    metrics.BaselineEnergyKWh += baseline.Power * ActiveHours(baseline, startHour, endHour);
                    metrics.CurrentEnergyKWh += current.Power * ActiveHours(current, startHour, endHour);
                    if (baseline.Lighting) { baselineLighting += bp; currentLighting += ActiveAt(current, hour) && current.Power > 0 ? baseline.Power * current.Service : 0; }
                    if (ActiveAt(baseline, hour) && baseline.MinimumService > 0) metrics.ServiceLevel = Math.Min(metrics.ServiceLevel, ActiveAt(current, hour) && current.Power > 0 ? current.Service : 0);
                    if (!ServiceSafe(baseline, current, startHour, endHour)) snapshot.SafetySatisfied = false;
                    if (baseline.Essential && (current.Power != baseline.Power || current.Start != baseline.Start || current.End != baseline.End)) snapshot.SafetySatisfied = false;
                }
                metrics.LightingRatio = baselineLighting > 0 ? currentLighting / baselineLighting : 0;
                snapshot.Buildings[i] = metrics;
                snapshot.BaselinePowerKW += metrics.BaselinePowerKW; snapshot.CurrentPowerKW += metrics.CurrentPowerKW;
                snapshot.BaselineEnergyKWh += metrics.BaselineEnergyKWh; snapshot.CurrentEnergyKWh += metrics.CurrentEnergyKWh;
                snapshot.Comfort = Math.Min(snapshot.Comfort, metrics.Comfort);
            }
            foreach (Building b in buildings)
            {
                snapshot.PurchaseCount += state[b.Id].Count;
            }
            var upgradedIds = new List<string>();
            foreach (Building b in buildings) if (state[b.Id].Count > 0) upgradedIds.Add(b.Id);
            snapshot.UpgradedBuildingIds = upgradedIds.ToArray();
            ApplyTimelineMetrics(snapshot, currentHour, accumulatedBaseline, accumulatedEnergy);
            snapshot.SavedEnergyKWh = snapshot.BaselineEnergyKWh - snapshot.CurrentEnergyKWh;
            snapshot.SavingPercent = snapshot.BaselineEnergyKWh > 0 ? snapshot.SavedEnergyKWh / snapshot.BaselineEnergyKWh * 100 : 0;
            return snapshot;
        }

        void Integrate(Dictionary<string, List<string>> state, double from, double to, ref double baseline, ref double current)
        {
            foreach (Building b in buildings)
            {
                Device[] effective = EffectiveDevices(b, state[b.Id]);
                for (int i = 0; i < b.Devices.Length; i++)
                {
                    baseline += b.Devices[i].Power * ActiveHours(b.Devices[i], from, to);
                    current += effective[i].Power * ActiveHours(effective[i], from, to);
                }
            }
        }

        void ApplyTimelineMetrics(SimulationSnapshot snapshot, double hour, double baseline, double current)
        {
            snapshot.IsTimeline = useTimeline;
            snapshot.AccumulatedBaselineEnergyKWh = baseline;
            snapshot.AccumulatedEnergyKWh = current;
            snapshot.ObservedSavingPercent = baseline > 0 ? (baseline - current) / baseline * 100 : 0;
            snapshot.ElapsedHours = useTimeline ? hour - startHour : 0;
            snapshot.LastUpgradeHour = LastUpgrade(history);
        }

        double LastUpgrade(List<UpgradePurchaseRecord> records)
        {
            double last = startHour;
            foreach (UpgradePurchaseRecord record in records)
                if (rules.DecisionRating || rules.RequiredBuildingIds.Length == 0 || Array.IndexOf(rules.RequiredBuildingIds, record.BuildingId) >= 0)
                    last = Math.Max(last, record.Hour);
            return last;
        }

        static bool SameIds(List<string> actual, string[] expected)
        {
            if (actual.Count != expected.Length) return false;
            for (int i = 0; i < actual.Count; i++) if (actual[i] != expected[i]) return false;
            return true;
        }

        static bool ServiceSafe(Device baseline, Device effective, double from, double to)
        {
            if (baseline.MinimumService <= 0) return true;
            if (effective.Power <= 0 && ActiveHours(baseline, from, to) > 0) return false;
            if (effective.Service + 1e-9 < baseline.MinimumService && ActiveHours(baseline, from, to) > 0) return false;
            var cuts = new List<double> { from, to };
            AddCuts(cuts, baseline, from, to); AddCuts(cuts, effective, from, to); cuts.Sort();
            for (int i = 1; i < cuts.Count; i++)
            {
                if (cuts[i] <= cuts[i - 1]) continue;
                double middle = (cuts[i - 1] + cuts[i]) / 2;
                if (ActiveAt(baseline, middle) && !ActiveAt(effective, middle)) return false;
            }
            return true;
        }

        static void AddCuts(List<double> cuts, Device device, double from, double to)
        {
            for (int day = (int)Math.Floor(from / 24) - 1; day <= (int)Math.Floor(to / 24); day++)
            {
                double begin = day * 24 + device.Start, end = day * 24 + device.End + (device.End < device.Start ? 24 : 0);
                if (begin > from && begin < to) cuts.Add(begin);
                if (end > from && end < to) cuts.Add(end);
            }
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

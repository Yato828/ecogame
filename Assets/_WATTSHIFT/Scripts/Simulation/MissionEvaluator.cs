using System;
using System.Collections.Generic;

namespace Wattshift
{
    /// <summary>Copied evaluation rules ensure an in-flight mission cannot be changed by asset mutation.</summary>
    internal sealed class MissionRules
    {
        public bool Timeline, RequireEnd;
        public double Start, End, MinimumObservation;
        public float Target, MinimumComfort;
        public int InitialBudget, MinimumPurchases, BaseReward;
        public string[] RequiredBuildingIds;
        public float[] StarThresholds;
        public bool DecisionRating;
        public MissionProblemDefinition[] Problems;
        public int[] RatingThresholds;
        public double ProblemWeight, EnergyWeight, SafetyWeight, BudgetWeight;
        public int RationalSpend;
        public double MinimumUsefulSavingPercent;

        public static MissionRules From(MissionDefinition mission, double normalizedEnd)
        {
            return new MissionRules
            {
                Timeline = mission.UseTimeline, RequireEnd = mission.RequireEndOfShift,
                Start = mission.StartHour, End = normalizedEnd, MinimumObservation = mission.MinimumObservationHours,
                Target = mission.TargetSavingPercent, MinimumComfort = mission.MinComfort,
                InitialBudget = mission.InitialBudget, MinimumPurchases = mission.MinimumUpgradeCount,
                BaseReward = mission.BaseReward,
                DecisionRating = mission.UseDecisionRating,
                Problems = CopyProblems(mission.Problems),
                RatingThresholds = (int[])(mission.RatingStarThresholds ?? new[] { 25, 60, 85 }).Clone(),
                ProblemWeight = mission.ProblemWeight, EnergyWeight = mission.EnergyWeight,
                SafetyWeight = mission.SafetyWeight, BudgetWeight = mission.BudgetWeight,
                RationalSpend = mission.RationalSpend,
                MinimumUsefulSavingPercent = mission.MinimumUsefulSavingPercent,
                RequiredBuildingIds = (string[])(mission.RequiredBuildingIds ?? Array.Empty<string>()).Clone(),
                StarThresholds = (float[])(mission.StarSavingThresholds ?? new float[] { 0, 30, 40 }).Clone()
            };
        }
        static MissionProblemDefinition[] CopyProblems(MissionProblemDefinition[] source)
        {
            if (source == null) return Array.Empty<MissionProblemDefinition>();
            var copy = new MissionProblemDefinition[source.Length];
            for (int i = 0; i < copy.Length; i++)
            {
                var p = source[i];
                if (p == null) continue;
                copy[i] = new MissionProblemDefinition { Id = p.Id, BuildingId = p.BuildingId, Title = p.Title, Required = p.Required, Urgent = p.Urgent, MinimumReductionPercent = p.MinimumReductionPercent, DeviceIds = (string[])(p.DeviceIds ?? Array.Empty<string>()).Clone() };
            }
            return copy;
        }
    }

    /// <summary>Stateless mission rules; completed timeline outcomes use observed, never forecast, energy.</summary>
    public static class MissionEvaluator
    {
        public static MissionEvaluation Evaluate(MissionDefinition mission, SimulationSnapshot snapshot)
        {
            if (mission == null) throw new ArgumentNullException(nameof(mission));
            double end = mission.EndHour < mission.StartHour ? mission.EndHour + 24 : mission.EndHour;
            return Evaluate(snapshot, MissionRules.From(mission, end));
        }

        internal static MissionEvaluation Evaluate(SimulationSnapshot snapshot, float target, float minComfort, int initialBudget)
        {
            return Evaluate(snapshot, new MissionRules
            {
                Target = target, MinimumComfort = minComfort, InitialBudget = initialBudget,
                RequiredBuildingIds = Array.Empty<string>(), StarThresholds = new float[] { 0, 30, 40 }
            });
        }

        internal static MissionEvaluation Evaluate(SimulationSnapshot snapshot, MissionRules rules)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            if (rules.DecisionRating) return EvaluateDecisions(snapshot, rules);
            var reasons = new List<string>();
            if (!snapshot.SafetySatisfied) reasons.Add("Необходимо сохранить питание защищённых потребителей и безопасное освещение.");
            if (!Finite(snapshot.Comfort) || snapshot.Comfort + .0001 < rules.MinimumComfort) reasons.Add("Комфорт ниже допустимого уровня.");
            double saving = rules.Timeline ? snapshot.ObservedSavingPercent : snapshot.SavingPercent;
            if (!Finite(saving) || saving + .000001 < rules.Target) reasons.Add("Цель фактической экономии энергии ещё не достигнута.");
            if (snapshot.Budget < 0) reasons.Add("Бюджет превышен.");
            if (rules.Timeline)
            {
                if (!snapshot.IsTimeline) reasons.Add("Отсутствуют наблюдения за работой города.");
                if (rules.RequireEnd && snapshot.ElapsedHours + 1e-9 < rules.End - rules.Start) reasons.Add("Дождитесь окончания вечерней смены, чтобы оценить реальные последствия решений.");
                if (snapshot.PurchaseCount < rules.MinimumPurchases) reasons.Add("Не устранены все требуемые виды неэффективного потребления.");
                foreach (string building in rules.RequiredBuildingIds)
                    if (snapshot.UpgradedBuildingIds == null || Array.IndexOf(snapshot.UpgradedBuildingIds, building) < 0)
                        reasons.Add("Один из объектов задания ещё не модернизирован.");
                if (snapshot.Hour - snapshot.LastUpgradeHour + 1e-9 < rules.MinimumObservation)
                    reasons.Add("После последней модернизации нужно понаблюдать за работой объекта.");
            }
            bool pass = reasons.Count == 0;
            double retained = rules.InitialBudget > 0 ? Math.Max(0, Math.Min(1, (double)snapshot.Budget / rules.InitialBudget)) : 0;
            int score = pass ? (int)Math.Round(Math.Max(0, Math.Min(100, saving)) * 10 + retained * 100) : 0;
            int stars = 0;
            if (pass)
            {
                stars = 1;
                if (rules.StarThresholds.Length > 1 && saving + .000001 >= rules.StarThresholds[1]) stars = 2;
                if (rules.StarThresholds.Length > 2 && saving + .000001 >= rules.StarThresholds[2]) stars = 3;
            }
            return new MissionEvaluation { CanComplete = pass, Reasons = reasons.ToArray(), Score = score, Stars = stars, Reward = pass ? rules.BaseReward * stars : 0 };
        }

        static MissionEvaluation EvaluateDecisions(SimulationSnapshot snapshot, MissionRules rules)
        {
            var result = new MissionEvaluation { TotalProblems = rules.Problems.Length, Problems = new ProblemEvaluation[rules.Problems.Length] };
            bool safe = snapshot.SafetySatisfied && Finite(snapshot.Comfort) && snapshot.Comfort + .0001 >= rules.MinimumComfort;
            bool allRequired = true;
            for (int i = 0; i < rules.Problems.Length; i++)
            {
                var definition = rules.Problems[i];
                var problem = new ProblemEvaluation { Id = definition.Id, BuildingId = definition.BuildingId, Title = definition.Title, Required = definition.Required, Urgent = definition.Urgent };
                double baseline = 0, current = 0; bool found = true;
                var building = Array.Find(snapshot.Buildings, b => b.BuildingId == definition.BuildingId);
                foreach (string id in definition.DeviceIds)
                {
                    var device = building == null ? null : Array.Find(building.Devices, d => d.Id == id);
                    if (device == null || !Finite(device.BaselineEnergyKWh) || !Finite(device.CurrentEnergyKWh)) { found = false; continue; }
                    baseline += device.BaselineEnergyKWh; current += device.CurrentEnergyKWh;
                }
                problem.ReductionPercent = baseline > 0 ? Math.Max(0, (baseline - current) / baseline * 100) : 0;
                problem.Resolved = found && baseline > 0 && safe && problem.ReductionPercent + 1e-6 >= definition.MinimumReductionPercent;
                if (problem.Resolved) result.ResolvedProblems++;
                if (problem.Required && !problem.Resolved) allRequired = false;
                result.Problems[i] = problem;
            }
            double saving = rules.Timeline ? snapshot.ObservedSavingPercent : snapshot.SavingPercent;
            result.EnergySavedKWh = rules.Timeline ? snapshot.AccumulatedBaselineEnergyKWh - snapshot.AccumulatedEnergyKWh : snapshot.SavedEnergyKWh;
            double totalWeight = rules.ProblemWeight + rules.EnergyWeight + rules.SafetyWeight + rules.BudgetWeight;
            double scale = totalWeight > 0 ? 100 / totalWeight : 0;
            result.ProblemPoints = rules.ProblemWeight * scale * (result.TotalProblems > 0 ? (double)result.ResolvedProblems / result.TotalProblems : 0);
            result.EnergyPoints = rules.EnergyWeight * scale * Clamp(rules.Target > 0 ? saving / rules.Target : saving > 0 ? 1 : 0);
            result.SafetyPoints = safe ? rules.SafetyWeight * scale : 0;
            double spent = Math.Max(0, (double)rules.InitialBudget - snapshot.Budget);
            double efficiency = spent <= rules.RationalSpend ? 1 : rules.RationalSpend / spent;
            result.BudgetPoints = snapshot.Budget >= 0 ? rules.BudgetWeight * scale * Clamp(efficiency) : 0;
            result.Score = (int)Math.Round(Math.Max(0, Math.Min(100, result.ProblemPoints + result.EnergyPoints + result.SafetyPoints + result.BudgetPoints)));
            var reasons = new List<string>();
            if (!safe) reasons.Add("Безопасность или комфорт ниже обязательного уровня.");
            if (snapshot.Budget < 0) reasons.Add("Бюджет превышен.");
            bool useful = snapshot.PurchaseCount > 0 && Finite(saving) && Finite(result.EnergySavedKWh) && result.EnergySavedKWh > 1e-6 && saving > 1e-6 && saving + 1e-6 >= rules.MinimumUsefulSavingPercent;
            if (!useful) reasons.Add("Нет фактической экономии от полезного решения.");
            if (rules.Timeline)
            {
                if (!snapshot.IsTimeline) reasons.Add("Отсутствуют наблюдения за работой города.");
                if (snapshot.ElapsedHours + 1e-9 < rules.End - rules.Start) reasons.Add("Дождитесь окончания смены.");
                if (snapshot.Hour - snapshot.LastUpgradeHour + 1e-9 < rules.MinimumObservation) reasons.Add("Недостаточно времени для наблюдения после решения.");
            }
            if (reasons.Count == 0)
            {
                if (result.Score >= rules.RatingThresholds[0]) result.Stars = 1;
                bool majority = result.TotalProblems > 0 && result.ResolvedProblems > result.TotalProblems / 2;
                bool meaningful = saving + 1e-6 >= rules.Target * .5;
                if (result.Score >= rules.RatingThresholds[1] && majority && meaningful) result.Stars = 2;
                if (result.Score >= rules.RatingThresholds[2] && allRequired && majority && meaningful) result.Stars = 3;
                if (result.Stars == 0) reasons.Add("Недостаточно полезных результатов для первой звезды.");
            }
            result.CanComplete = result.Stars > 0;
            result.Reward = result.CanComplete ? rules.BaseReward * result.Stars : 0;
            result.Reasons = reasons.ToArray();
            result.RatingExplanation = !safe ? "Безопасность важнее экономии. Сохрани комфорт и работу важных систем."
                : result.Stars >= 3 && result.ResolvedProblems == result.TotalProblems ? "Отлично! Все проблемы устранены."
                : result.Stars >= 2 ? "Хороший результат. Можно сэкономить ещё больше."
                : result.Stars == 1 ? "Часть проблем решена. Для двух звёзд реши большинство проблем и увеличь экономию."
                : !useful ? "Нет полезной экономии. Попробуй принять решение раньше."
                : "Смена ещё не завершена или результат пока недостаточен.";
            return result;
        }

        static double Clamp(double value) => Finite(value) ? Math.Max(0, Math.Min(1, value)) : 0;
        static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    }
}

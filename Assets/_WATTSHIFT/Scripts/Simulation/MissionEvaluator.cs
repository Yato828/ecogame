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

        public static MissionRules From(MissionDefinition mission, double normalizedEnd)
        {
            return new MissionRules
            {
                Timeline = mission.UseTimeline, RequireEnd = mission.RequireEndOfShift,
                Start = mission.StartHour, End = normalizedEnd, MinimumObservation = mission.MinimumObservationHours,
                Target = mission.TargetSavingPercent, MinimumComfort = mission.MinComfort,
                InitialBudget = mission.InitialBudget, MinimumPurchases = mission.MinimumUpgradeCount,
                BaseReward = mission.BaseReward,
                RequiredBuildingIds = (string[])(mission.RequiredBuildingIds ?? Array.Empty<string>()).Clone(),
                StarThresholds = (float[])(mission.StarSavingThresholds ?? new float[] { 0, 30, 40 }).Clone()
            };
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

        static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    }
}

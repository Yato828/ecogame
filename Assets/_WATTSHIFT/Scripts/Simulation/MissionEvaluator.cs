using System;
using System.Collections.Generic;

namespace Wattshift
{
    /// <summary>Stateless mission rules. No scene access or presentation calculations.</summary>
    public static class MissionEvaluator
    {
        public static MissionEvaluation Evaluate(MissionDefinition mission, SimulationSnapshot snapshot)
        {
            if (mission == null) throw new ArgumentNullException(nameof(mission));
            return Evaluate(snapshot, mission.TargetSavingPercent, mission.MinComfort, mission.InitialBudget);
        }

        internal static MissionEvaluation Evaluate(SimulationSnapshot snapshot, float target, float minComfort, int initialBudget)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            var reasons = new List<string>();
            if (!snapshot.SafetySatisfied) reasons.Add("Необходимо сохранить питание защищённых потребителей.");
            if (!Finite(snapshot.Comfort) || snapshot.Comfort + 0.0001 < minComfort) reasons.Add("Комфорт ниже допустимого уровня.");
            if (!Finite(snapshot.SavingPercent) || snapshot.SavingPercent + 0.000001 < target) reasons.Add("Цель экономии энергии ещё не достигнута.");
            if (snapshot.Budget < 0) reasons.Add("Бюджет превышен.");
            bool pass = reasons.Count == 0;
            double retained = initialBudget > 0 ? Math.Max(0, Math.Min(1, (double)snapshot.Budget / initialBudget)) : 0;
            int score = pass ? (int)Math.Round(Math.Max(0, Math.Min(100, snapshot.SavingPercent)) * 10 + retained * 100) : 0;
            return new MissionEvaluation { CanComplete = pass, Reasons = reasons.ToArray(), Score = score };
        }

        static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    }
}

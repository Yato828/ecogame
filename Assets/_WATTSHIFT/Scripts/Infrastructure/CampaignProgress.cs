using System;
using System.Collections.Generic;
using UnityEngine;

namespace Wattshift
{
    [Serializable] public sealed class CampaignRecord { public string MissionId; public CityState BestRun; public CityState BestScoreRun; public CityState BestSavingRun; }

    /// <summary>Rewards derive from verified best runs, never repeated completion clicks.</summary>
    public sealed class CampaignProgress
    {
        readonly MissionDefinition[] missions;
        readonly Dictionary<string, CampaignRecord> records = new Dictionary<string, CampaignRecord>(StringComparer.Ordinal);
        readonly Dictionary<string, MissionEvaluation> evaluations = new Dictionary<string, MissionEvaluation>(StringComparer.Ordinal);
        public CampaignProgress(MissionDefinition[] definitions) { missions = definitions; }
        public int Credits { get { int sum = 0; foreach (var e in evaluations.Values) sum = checked(sum + e.Reward); return sum; } }
        readonly Dictionary<string, MissionEvaluation> scoreRecords = new Dictionary<string, MissionEvaluation>(StringComparer.Ordinal);
        readonly Dictionary<string, double> savingRecords = new Dictionary<string, double>(StringComparer.Ordinal);
        public int TotalStars { get { int sum = 0; foreach (var value in evaluations.Values) sum += value.Stars; return sum; } }
        public int BestScore(int index) => index >= 0 && index < missions.Length && scoreRecords.TryGetValue(missions[index].Id, out var value) ? value.Score : 0;
        public double BestSavingPercent(int index) => index >= 0 && index < missions.Length && savingRecords.TryGetValue(missions[index].Id, out var value) ? value : 0;
        public int BestStars(int index) => index >= 0 && index < missions.Length && evaluations.TryGetValue(missions[index].Id, out var e) ? e.Stars : 0;
        public bool IsUnlocked(int index) => index >= 0 && index < missions.Length && (index == 0 || BestStars(index - 1) > 0);
        public CampaignRecord[] Export()
        {
            var output = new List<CampaignRecord>();
            foreach (var mission in missions) if (records.TryGetValue(mission.Id, out var record)) output.Add(Clone(record));
            return output.ToArray();
        }
        public int Record(int index, CityState run)
        {
            if (!IsUnlocked(index)) throw new ArgumentException("Mission is locked.");
            var mission = missions[index]; var sim = new CitySimulationService(mission);
            if (run == null || !run.Completed || !sim.TryRestore(run).Success) throw new ArgumentException("Unverified run.");
            var value = MissionEvaluator.Evaluate(mission, sim.Evaluate(run.CurrentHour));
            if (!value.CanComplete) throw new ArgumentException("Unmet requirements.");
            int before = Credits;
            var snapshot = sim.Evaluate(run.CurrentHour);
            double saving = mission.UseTimeline ? snapshot.ObservedSavingPercent : snapshot.SavingPercent;
            if (!records.TryGetValue(mission.Id, out var record))
            {
                record = new CampaignRecord { MissionId = mission.Id };
                records.Add(mission.Id, record);
            }
            if (!evaluations.TryGetValue(mission.Id, out var old) || value.Stars > old.Stars || (value.Stars == old.Stars && value.Score > old.Score))
            { record.BestRun = CloneRun(run); evaluations[mission.Id] = value; }
            if (!scoreRecords.TryGetValue(mission.Id, out var score) || value.Score > score.Score)
            { record.BestScoreRun = CloneRun(run); scoreRecords[mission.Id] = value; }
            if (!savingRecords.TryGetValue(mission.Id, out var bestSaving) || saving > bestSaving)
            { record.BestSavingRun = CloneRun(run); savingRecords[mission.Id] = saving; }
            return Credits - before;
        }
        public bool TryRestore(CampaignRecord[] saved, out string error)
        {
            error = null;
            if (saved == null || saved.Length > missions.Length) { error = "Повреждён список миссий."; return false; }
            var candidate = new CampaignProgress(missions); var ids = new HashSet<string>(StringComparer.Ordinal);
            try
            {
                foreach (var record in saved)
                {
                    int index = record == null ? -1 : Array.FindIndex(missions, m => m.Id == record.MissionId);
                    if (index < 0 || !ids.Add(record.MissionId)) { error = "Повреждены результаты кампании."; return false; }
                    candidate.Record(index, record.BestRun);
                    if (!AbsentOptionalRun(record.BestScoreRun)) candidate.Record(index, record.BestScoreRun);
                    if (!AbsentOptionalRun(record.BestSavingRun)) candidate.Record(index, record.BestSavingRun);
                }
                for (int i = 1; i < missions.Length; i++)
                    if (candidate.BestStars(i) > 0 && candidate.BestStars(i - 1) == 0) { error = "Нарушен порядок миссий."; return false; }
            }
            catch (ArgumentException) { error = "Результат не подтверждается состоянием миссии."; return false; }
            records.Clear(); evaluations.Clear(); scoreRecords.Clear(); savingRecords.Clear();
            foreach (var item in candidate.records) records.Add(item.Key, item.Value);
            foreach (var item in candidate.evaluations) evaluations.Add(item.Key, item.Value);
            foreach (var item in candidate.scoreRecords) scoreRecords.Add(item.Key, item.Value);
            foreach (var item in candidate.savingRecords) savingRecords.Add(item.Key, item.Value);
            return true;
        }
        // JsonUtility can materialize missing nested serializable fields as an empty object.
        // Only the entirely empty default shape is absent; any populated replay must verify.
        static bool AbsentOptionalRun(CityState value) => value == null ||
            ((value.Version == 0 || value.Version == 1) && string.IsNullOrEmpty(value.MissionId) && !value.Completed &&
             value.CurrentHour == 0 && (value.Buildings == null || value.Buildings.Length == 0) &&
             (value.History == null || value.History.Length == 0));

        static CityState CloneRun(CityState value) => JsonUtility.FromJson<CityState>(JsonUtility.ToJson(value));
        static CampaignRecord Clone(CampaignRecord value) => JsonUtility.FromJson<CampaignRecord>(JsonUtility.ToJson(value));
    }
}

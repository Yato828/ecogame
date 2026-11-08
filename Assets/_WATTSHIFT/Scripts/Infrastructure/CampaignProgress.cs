using System;
using System.Collections.Generic;
using UnityEngine;

namespace Wattshift
{
    [Serializable] public sealed class CampaignRecord { public string MissionId; public CityState BestRun; }

    /// <summary>Rewards derive from verified best runs, never repeated completion clicks.</summary>
    public sealed class CampaignProgress
    {
        readonly MissionDefinition[] missions;
        readonly Dictionary<string, CampaignRecord> records = new Dictionary<string, CampaignRecord>(StringComparer.Ordinal);
        readonly Dictionary<string, MissionEvaluation> evaluations = new Dictionary<string, MissionEvaluation>(StringComparer.Ordinal);
        public CampaignProgress(MissionDefinition[] definitions) { missions = definitions; }
        public int Credits { get { int sum = 0; foreach (var e in evaluations.Values) sum = checked(sum + e.Reward); return sum; } }
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
            if (!evaluations.TryGetValue(mission.Id, out var old) || value.Stars > old.Stars || (value.Stars == old.Stars && value.Score > old.Score))
            { records[mission.Id] = Clone(new CampaignRecord { MissionId = mission.Id, BestRun = run }); evaluations[mission.Id] = value; }
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
                }
                for (int i = 1; i < missions.Length; i++)
                    if (candidate.BestStars(i) > 0 && candidate.BestStars(i - 1) == 0) { error = "Нарушен порядок миссий."; return false; }
            }
            catch (ArgumentException) { error = "Результат не подтверждается состоянием миссии."; return false; }
            records.Clear(); evaluations.Clear();
            foreach (var item in candidate.records) records.Add(item.Key, item.Value);
            foreach (var item in candidate.evaluations) evaluations.Add(item.Key, item.Value);
            return true;
        }
        static CampaignRecord Clone(CampaignRecord value) => JsonUtility.FromJson<CampaignRecord>(JsonUtility.ToJson(value));
    }
}

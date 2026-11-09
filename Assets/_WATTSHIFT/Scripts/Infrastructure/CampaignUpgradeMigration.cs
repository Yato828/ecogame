using System;
using System.Linq;
using UnityEngine;

namespace Wattshift
{
    /// <summary>Verify old histories with old rules before replaying compatible decisions under G4 rules.</summary>
    public static class CampaignUpgradeMigration
    {
        public static bool TryMigrate(string oldPath, CampaignSaveService destination, MissionDefinition[] legacy, MissionDefinition[] current, out string message)
        {
            message = "Предыдущий прогресс не найден.";
            var source = new CampaignSaveService(oldPath);
            if (!source.Exists || destination.Exists || legacy.Length != current.Length) return false;
            if (!source.Load(out var saved, out message)) return false;
            var oldProgress = new CampaignProgress(legacy);
            if (!oldProgress.TryRestore(saved.Records, out message) || saved.MissionIndex < 0 || saved.MissionIndex >= legacy.Length || !oldProgress.IsUnlocked(saved.MissionIndex)) return false;
            var oldMission=legacy[saved.MissionIndex];
            if(saved.InvestigatedIds==null || saved.City==null || saved.InvestigatedIds.Distinct().Count()!=saved.InvestigatedIds.Length
                || saved.InvestigatedIds.Any(id=>id==null||!oldMission.RequiredBuildingIds.Contains(id))
                || (saved.InvestigatedIds.Length>0&&saved.City.CurrentHour<oldMission.InvestigationAvailableHour)
                || (saved.City.History??Array.Empty<UpgradePurchaseRecord>()).Any(h=>h==null||!saved.InvestigatedIds.Contains(h.BuildingId)))
            { message="Обследование предыдущей смены не подтверждено.";return false; }
            var active = new CitySimulationService(legacy[saved.MissionIndex]);
            if (!active.TryRestore(saved.City).Success) { message = "Предыдущее состояние не подтверждено."; return false; }
            var nextProgress = new CampaignProgress(current);
            try
            {
                foreach (var record in oldProgress.Export())
                {
                    int index = Array.FindIndex(legacy,m => m.Id == record.MissionId);
                    foreach (var run in new[]{record.BestRun,record.BestScoreRun,record.BestSavingRun})
                    {
                        if (run == null) continue;
                        var converted = JsonUtility.FromJson<CityState>(JsonUtility.ToJson(run)); converted.MissionId = current[index].Id;
                        var simulation = new CitySimulationService(current[index]);
                        if (!simulation.TryRestore(converted).Success) { message = "Прогресс не совместим с новыми правилами. Старый файл сохранён."; return false; }
                        nextProgress.Record(index,converted);
                    }
                }
                var city = JsonUtility.FromJson<CityState>(JsonUtility.ToJson(saved.City));city.MissionId = current[saved.MissionIndex].Id;
                var test = new CitySimulationService(current[saved.MissionIndex]);
                if (!test.TryRestore(city).Success) { message = "Активная смена не совместима. Старый файл сохранён."; return false; }
                return destination.Save(new CampaignSave { MissionIndex=saved.MissionIndex,City=city,Records=nextProgress.Export(),InvestigatedIds=saved.InvestigatedIds,Results=saved.Results,ScannerEnabled=saved.ScannerEnabled,SpeedMultiplier=1 },out message);
            }
            catch (ArgumentException) { message = "Перенос не подтверждён. Старый файл сохранён."; return false; }
        }
    }
}

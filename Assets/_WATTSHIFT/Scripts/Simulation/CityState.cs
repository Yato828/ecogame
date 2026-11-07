using System;

namespace Wattshift
{
    [Serializable]
    public sealed class CityState
    {
        public int Version = 1;
        public string MissionId;
        public bool Completed;
        public BuildingState[] Buildings = Array.Empty<BuildingState>();
    }

    [Serializable]
    public sealed class BuildingState
    {
        public string BuildingId;
        public string[] UpgradeIds = Array.Empty<string>();
    }

    public sealed class BuildingMetrics
    {
        public string BuildingId;
        public double BaselinePowerKW;
        public double CurrentPowerKW;
        public double BaselineEnergyKWh;
        public double CurrentEnergyKWh;
        public double LightingRatio;
        public float Comfort;
        public bool HasUpgrade;
    }

    public sealed class SimulationSnapshot
    {
        public double Hour;
        public double BaselinePowerKW;
        public double CurrentPowerKW;
        public double BaselineEnergyKWh;
        public double CurrentEnergyKWh;
        public double SavedEnergyKWh;
        public double SavingPercent;
        public int Budget;
        public float Comfort;
        public bool SafetySatisfied;
        public bool Completed;
        public BuildingMetrics[] Buildings = Array.Empty<BuildingMetrics>();
    }

    public sealed class ActionResult
    {
        public bool Success;
        public string Code;
        public string Message;

        public static ActionResult Ok(string message = "Готово.") => new ActionResult { Success = true, Code = "OK", Message = message };
        public static ActionResult Fail(string code, string message) => new ActionResult { Success = false, Code = code, Message = message };
    }

    public sealed class MissionEvaluation
    {
        public bool CanComplete;
        public string[] Reasons = Array.Empty<string>();
        public int Score;
    }
}

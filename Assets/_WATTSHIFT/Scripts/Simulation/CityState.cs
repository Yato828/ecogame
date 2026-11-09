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
        public double CurrentHour;
        public UpgradePurchaseRecord[] History = Array.Empty<UpgradePurchaseRecord>();
    }

    [Serializable]
    public sealed class UpgradePurchaseRecord
    {
        public string BuildingId;
        public string UpgradeId;
        public double Hour;
    }

    [Serializable]
    public sealed class BuildingState
    {
        public string BuildingId;
        public string[] UpgradeIds = Array.Empty<string>();
    }

    public sealed class DeviceMetrics
    {
        public string Id;
        public string Title;
        public LoadPriority Priority;
        public bool Essential;
        public double CurrentPowerKW;
        public double BaselineEnergyKWh;
        public double CurrentEnergyKWh;
        public double ServiceLevel;
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
        public double ServiceLevel = 1;
        public DeviceMetrics[] Devices = Array.Empty<DeviceMetrics>();
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
        public bool IsTimeline;
        public double AccumulatedEnergyKWh;
        public double AccumulatedBaselineEnergyKWh;
        public double ObservedSavingPercent;
        public double ElapsedHours;
        public double LastUpgradeHour;
        public int PurchaseCount;
        public string[] UpgradedBuildingIds = Array.Empty<string>();
    }

    public sealed class ActionResult
    {
        public bool Success;
        public string Code;
        public string Message;

        public static ActionResult Ok(string message = "Готово.") => new ActionResult { Success = true, Code = "OK", Message = message };
        public static ActionResult Fail(string code, string message) => new ActionResult { Success = false, Code = code, Message = message };
    }

    public sealed class ProblemEvaluation
    {
        public string Id, BuildingId, Title;
        public bool Required, Urgent, Resolved;
        public double ReductionPercent;
    }

    public sealed class MissionEvaluation
    {
        public bool CanComplete;
        public string[] Reasons = Array.Empty<string>();
        public int Score;
        public int Stars;
        public int Reward;
        public ProblemEvaluation[] Problems = Array.Empty<ProblemEvaluation>();
        public int TotalProblems, ResolvedProblems;
        public double ProblemPoints, EnergyPoints, SafetyPoints, BudgetPoints, EnergySavedKWh;
        public string RatingExplanation = "";
    }
}

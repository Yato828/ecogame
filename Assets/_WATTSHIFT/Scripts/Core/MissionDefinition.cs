using System;
using UnityEngine;

namespace Wattshift
{
    [CreateAssetMenu(menuName = "WATTSHIFT/Mission")]
    public sealed class MissionDefinition : ScriptableObject
    {
        public string Id;
        public string Title;
        [TextArea] public string Description;
        [TextArea] public string Assumptions;
        public float StartHour = 17;
        public float EndHour = 23;
        public float EvaluationHour = 20;
        [Min(0)] public int InitialBudget = 650;
        [Range(0, 100)] public float TargetSavingPercent = 30;
        [Range(0, 100)] public float MinComfort = 80;
        public BuildingDefinition[] Buildings = Array.Empty<BuildingDefinition>();
        public bool UseTimeline;
        [Min(1)] public float RealDurationSeconds = 360;
        public bool RequireEndOfShift = true;
        [Min(0)] public int MinimumUpgradeCount = 2;
        public string[] RequiredBuildingIds = Array.Empty<string>();
        public float InvestigationAvailableHour = 18;
        [Min(0)] public float MinimumObservationHours = .5f;
        public MissionEventDefinition[] Events = Array.Empty<MissionEventDefinition>();
        public float[] StarSavingThresholds = { 0, 30, 40 };
        [Min(0)] public int BaseReward = 100;
        public bool UseDecisionRating;
        [Range(0, 100)] public float MinimumUsefulSavingPercent = .25f;
        public MissionProblemDefinition[] Problems = Array.Empty<MissionProblemDefinition>();
        public int[] RatingStarThresholds = { 25, 60, 85 };
        [Min(0)] public float ProblemWeight = 40;
        [Min(0)] public float EnergyWeight = 30;
        [Min(0)] public float SafetyWeight = 20;
        [Min(0)] public float BudgetWeight = 10;
        [Min(0)] public int RationalSpend = 440;
    }

    [Serializable]
    public sealed class MissionProblemDefinition
    {
        public string Id;
        public string BuildingId;
        public string Title;
        public string[] DeviceIds = Array.Empty<string>();
        public bool Required = true;
        public bool Urgent;
        [Range(0, 100)] public float MinimumReductionPercent = 25;
    }

    [Serializable]
    public sealed class MissionEventDefinition
    {
        public string Id;
        public float Hour;
        [TextArea] public string Text;
    }
}

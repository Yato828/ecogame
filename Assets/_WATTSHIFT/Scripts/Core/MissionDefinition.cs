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
    }
}

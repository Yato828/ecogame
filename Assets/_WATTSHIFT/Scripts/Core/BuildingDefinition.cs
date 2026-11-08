using System;
using UnityEngine;

namespace Wattshift
{
    [CreateAssetMenu(menuName = "WATTSHIFT/Building")]
    public sealed class BuildingDefinition : ScriptableObject
    {
        public string Id;
        public string Title;
        [TextArea] public string Description;
        [Range(0, 100)] public float BaseComfort = 100;
        public DeviceDefinition[] Devices = Array.Empty<DeviceDefinition>();
        public UpgradeDefinition[] Upgrades = Array.Empty<UpgradeDefinition>();
    }

    [Serializable]
    public sealed class DeviceDefinition
    {
        public string Id;
        public string Title;
        [Min(0)] public float PowerKW;
        [Range(0, 24)] public float StartHour;
        [Range(0, 24)] public float EndHour = 24;
        public bool Essential;
        public bool IsLighting;
        public LoadPriority Priority;
        [Range(0, 1)] public float MinimumService;
    }

    public enum LoadPriority { NonCritical, Important, Critical }
}

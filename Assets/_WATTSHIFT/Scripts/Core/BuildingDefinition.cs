using System;
using UnityEngine;

namespace Wattshift
{
    [CreateAssetMenu(menuName = "WATTSHIFT/Building")]
    public sealed class BuildingDefinition : ScriptableObject
    {
        public string Id;
        public string Title;
        [Range(0, 100)] public float BaseComfort = 100;
        public DeviceDefinition[] Devices = Array.Empty<DeviceDefinition>();
        public UpgradeDefinition[] Upgrades = Array.Empty<UpgradeDefinition>();
    }

    [Serializable]
    public sealed class DeviceDefinition
    {
        public string Id;
        [Min(0)] public float PowerKW;
        [Range(0, 24)] public float StartHour;
        [Range(0, 24)] public float EndHour = 24;
        public bool Essential;
        public bool IsLighting;
    }
}

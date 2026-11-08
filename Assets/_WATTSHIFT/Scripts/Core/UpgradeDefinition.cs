using System;
using UnityEngine;

namespace Wattshift
{
    [CreateAssetMenu(menuName = "WATTSHIFT/Upgrade")]
    public sealed class UpgradeDefinition : ScriptableObject
    {
        public string Id;
        public string Title;
        [TextArea] public string Description;
        [Min(0)] public int Cost;
        public float ComfortDelta;
        public string ExclusiveGroup = "";
        public float RequiredAfterHour = -1;
        public UpgradeEffect[] Effects = Array.Empty<UpgradeEffect>();
    }

    [Serializable]
    public sealed class UpgradeEffect
    {
        public string DeviceId;
        [Min(0)] public float PowerMultiplier = 1;
        [Range(0, 1)] public float ServiceMultiplier = 1;
        public bool OverrideSchedule;
        [Range(0, 24)] public float StartHour;
        [Range(0, 24)] public float EndHour = 24;
    }
}

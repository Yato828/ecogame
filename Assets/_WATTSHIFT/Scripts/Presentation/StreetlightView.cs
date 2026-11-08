using UnityEngine;

namespace Wattshift
{
    /// <summary>Light output follows service level, not electricity efficiency.</summary>
    public sealed class StreetlightView : MonoBehaviour
    {
        public string BuildingId;
        public Light[] Lights = System.Array.Empty<Light>();
        [Min(0)] public float BaseIntensity = 1.3f;
        [Range(0, 24)] public float EveningStart = 18;
        [Range(0, 24)] public float EveningEnd = 24;

        public void Apply(double hour, BuildingMetrics metrics)
        {
            double clock = hour % 24;
            bool scheduled = EveningStart < EveningEnd ? clock >= EveningStart && clock < EveningEnd : clock >= EveningStart || clock < EveningEnd;
            bool powered = metrics == null || metrics.CurrentPowerKW > .000001;
            float service = metrics == null ? 1 : Mathf.Clamp01((float)metrics.ServiceLevel);
            foreach (Light light in Lights)
            {
                if (light == null) continue;
                light.enabled = scheduled && powered && service > .001f;
                light.intensity = BaseIntensity * service;
                light.shadows = LightShadows.None;
            }
        }
    }
}

using System;
using System.Collections.Generic;
using UnityEngine;

namespace Wattshift
{
    /// <summary>Presentation of game-clock state; never advances the clock or calculates energy.</summary>
    public sealed class CityAtmosphere : MonoBehaviour
    {
        public Light Sun;
        public Camera SceneCamera;
        public StreetlightView[] Streetlights = Array.Empty<StreetlightView>();
        public string[] TargetBuildingIds = Array.Empty<string>();
        [Range(0, 100)] public float ResolvedSavingsPercent = 25;

        CityBuilding[] buildings = Array.Empty<CityBuilding>();
        SourceBuildingHighlight[] highlights = Array.Empty<SourceBuildingHighlight>();
        readonly Dictionary<string, BuildingMetrics> metrics = new Dictionary<string, BuildingMetrics>(StringComparer.Ordinal);
        readonly HashSet<string> inspected = new HashSet<string>(StringComparer.Ordinal);
        readonly HashSet<string> targets = new HashSet<string>(StringComparer.Ordinal);
        Color originalSun, originalSky, originalEquator, originalGround, originalBackground;
        float originalIntensity;
        Quaternion originalRotation;
        float desiredIntensity;
        Color desiredSun, desiredSky, desiredEquator, desiredGround, desiredBackground;
        Quaternion desiredRotation;
        bool bound;

        public void Bind(CityBuilding[] sourceBuildings, BuildingEnergyView[] windows)
        {
            buildings = sourceBuildings ?? Array.Empty<CityBuilding>();
            highlights = new SourceBuildingHighlight[buildings.Length];
            for (int i = 0; i < buildings.Length; i++)
                if (buildings[i] != null)
                {
                    highlights[i] = buildings[i].GetComponent<SourceBuildingHighlight>();
                    if (highlights[i] == null) highlights[i] = buildings[i].gameObject.AddComponent<SourceBuildingHighlight>();
                }
            if (bound) return;
            if (Sun == null) Sun = RenderSettings.sun;
            if (SceneCamera == null) SceneCamera = Camera.main;
            originalSun = Sun != null ? Sun.color : Color.white;
            originalIntensity = Sun != null ? Sun.intensity : 2.4f;
            originalRotation = Sun != null ? Sun.transform.rotation : Quaternion.identity;
            originalSky = RenderSettings.ambientSkyColor;
            originalEquator = RenderSettings.ambientEquatorColor;
            originalGround = RenderSettings.ambientGroundColor;
            originalBackground = SceneCamera != null ? SceneCamera.backgroundColor : new Color(.77f, .84f, .81f);
            bound = true;
            SetTime(17);
            ApplyLighting(1);
        }

        public void Present(double hour, bool scannerEnabled, SimulationSnapshot snapshot, string[] investigatedIds, ProblemEvaluation[] problems = null)
        {
            if (!bound) return;
            SetTime(hour);
            metrics.Clear(); inspected.Clear(); targets.Clear();
            if (snapshot != null && snapshot.Buildings != null)
                foreach (BuildingMetrics item in snapshot.Buildings) metrics[item.BuildingId] = item;
            if (investigatedIds != null) foreach (string id in investigatedIds) inspected.Add(id);
            if (TargetBuildingIds != null) foreach (string id in TargetBuildingIds) targets.Add(id);
            for (int i = 0; i < buildings.Length; i++)
            {
                CityBuilding building = buildings[i];
                if (building == null || highlights[i] == null) continue;
                ScannerHighlightState state = ScannerHighlightState.Off;
                if (scannerEnabled && targets.Contains(building.SimulationId))
                {
                    bool known = inspected.Contains(building.SimulationId);
                    state = known ? ScannerHighlightState.Problem : ScannerHighlightState.Unexplored;
                    if (known && metrics.TryGetValue(building.SimulationId, out BuildingMetrics value))
                    {
                        double saving = value.BaselineEnergyKWh > .00001 ? (value.BaselineEnergyKWh - value.CurrentEnergyKWh) / value.BaselineEnergyKWh * 100 : 0;
                        bool observed = value.CurrentPowerKW + .00001 < value.BaselinePowerKW;
                        if (problems == null)
                        {
                            if (value.HasUpgrade && observed && saving + .00001 >= ResolvedSavingsPercent) state = ScannerHighlightState.Resolved;
                        }
                        else
                        {
                            int total=0,resolved=0;
                            foreach(var problem in problems)if(problem.BuildingId==building.SimulationId){total++;if(problem.Resolved)resolved++;}
                            if(total>0 && resolved==total)state=ScannerHighlightState.Resolved;
                        }
                    }
                }
                highlights[i].SetScannerState(state);
            }
            foreach (StreetlightView streetlight in Streetlights)
                if (streetlight != null) { metrics.TryGetValue(streetlight.BuildingId, out BuildingMetrics value); streetlight.Apply(hour, value); }
        }

        void SetTime(double hour)
        {
            float progress = Mathf.Clamp01(((float)hour - 17) / 6);
            float night = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.1f, .83f, progress));
            desiredIntensity = Mathf.Lerp(originalIntensity, .18f, night);
            Color sunset = new Color(1, .64f, .36f);
            desiredSun = progress < .45f ? Color.Lerp(originalSun, sunset, progress / .45f) : Color.Lerp(sunset, new Color(.46f, .61f, 1), (progress - .45f) / .55f);
            desiredSky = Color.Lerp(originalSky, new Color(.16f, .22f, .36f), night);
            desiredEquator = Color.Lerp(originalEquator, new Color(.13f, .18f, .27f), night);
            desiredGround = Color.Lerp(originalGround, new Color(.08f, .11f, .17f), night);
            desiredBackground = Color.Lerp(originalBackground, new Color(.105f, .16f, .245f), night);
            desiredRotation = originalRotation * Quaternion.Euler(-progress * 20, progress * 14, 0);
        }

        void Update()
        {
            if (bound) ApplyLighting(1 - Mathf.Exp(-8 * Mathf.Min(Time.unscaledDeltaTime, .05f)));
        }

        void ApplyLighting(float amount)
        {
            if (Sun != null)
            {
                Sun.intensity = Mathf.Lerp(Sun.intensity, desiredIntensity, amount);
                Sun.color = Color.Lerp(Sun.color, desiredSun, amount);
                Sun.transform.rotation = Quaternion.Slerp(Sun.transform.rotation, desiredRotation, amount);
            }
            RenderSettings.ambientSkyColor = Color.Lerp(RenderSettings.ambientSkyColor, desiredSky, amount);
            RenderSettings.ambientEquatorColor = Color.Lerp(RenderSettings.ambientEquatorColor, desiredEquator, amount);
            RenderSettings.ambientGroundColor = Color.Lerp(RenderSettings.ambientGroundColor, desiredGround, amount);
            if (SceneCamera != null) SceneCamera.backgroundColor = Color.Lerp(SceneCamera.backgroundColor, desiredBackground, amount);
        }

        void OnDisable()
        {
            if (!bound) return;
            if (Sun != null) { Sun.intensity = originalIntensity; Sun.color = originalSun; Sun.transform.rotation = originalRotation; }
            RenderSettings.ambientSkyColor = originalSky; RenderSettings.ambientEquatorColor = originalEquator; RenderSettings.ambientGroundColor = originalGround;
            if (SceneCamera != null) SceneCamera.backgroundColor = originalBackground;
            foreach (SourceBuildingHighlight item in highlights) if (item != null) item.SetScannerState(ScannerHighlightState.Off);
        }
    }
}

using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Wattshift.Presentation
{
    /// <summary>Runtime-only URP clone: original approved pipeline assets are never edited.</summary>
    [DefaultExecutionOrder(-60)]
    public sealed class QualityController : MonoBehaviour
    {
        public static QualityController Instance { get; private set; }
        UniversalRenderPipelineAsset runtimePipeline;
        RenderPipelineAsset originalDefault, originalQuality;
        float highScale, highShadowDistance;
        int highMsaa, highShadowResolution, highCascades, highAdditionalLights;
        string key;
        int tier = 2;

        public int Tier
        {
            get => tier;
            set
            {
                tier = Mathf.Clamp(value, 0, 2);
                if (key != null) PlayerPrefs.SetInt(key, tier);
                Apply();
            }
        }

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
            key = PresentationSettingsScope.Key("quality");
            tier = Mathf.Clamp(PlayerPrefs.GetInt(key, 2), 0, 2);
            originalDefault = GraphicsSettings.defaultRenderPipeline;
            originalQuality = QualitySettings.renderPipeline;
            var source = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (source == null) source = originalQuality as UniversalRenderPipelineAsset;
            if (source == null) source = originalDefault as UniversalRenderPipelineAsset;
            if (source == null) { Debug.LogWarning("WATTSHIFT: пресеты качества требуют настроенного URP."); return; }
            highScale = source.renderScale; highMsaa = source.msaaSampleCount;
            highShadowDistance = source.shadowDistance; highShadowResolution = source.mainLightShadowmapResolution;
            highCascades = source.shadowCascadeCount;
            highAdditionalLights = source.maxAdditionalLightsCount;
            runtimePipeline = Instantiate(source);
            runtimePipeline.name = source.name + " (WATTSHIFT runtime quality)";
            runtimePipeline.hideFlags = HideFlags.DontSave;
            GraphicsSettings.defaultRenderPipeline = runtimePipeline;
            QualitySettings.renderPipeline = runtimePipeline;
            Apply();
        }

        void Apply()
        {
            if (runtimePipeline == null) return;
            runtimePipeline.renderScale = tier == 2 ? highScale : Mathf.Min(highScale, tier == 1 ? .85f : .65f);
            runtimePipeline.msaaSampleCount = tier == 2 ? highMsaa : tier == 1 ? Mathf.Min(highMsaa, 2) : 1;
            runtimePipeline.shadowDistance = tier == 2 ? highShadowDistance : Mathf.Min(highShadowDistance, tier == 1 ? 38 : 24);
            runtimePipeline.mainLightShadowmapResolution = tier == 2 ? highShadowResolution : Mathf.Min(highShadowResolution, tier == 1 ? 1024 : 512);
            runtimePipeline.shadowCascadeCount = tier == 2 ? highCascades : tier == 1 ? Mathf.Min(highCascades, 2) : 1;
            runtimePipeline.maxAdditionalLightsCount = tier == 2 ? highAdditionalLights : Mathf.Min(highAdditionalLights, tier == 1 ? 2 : 1);
        }

        void OnApplicationPause(bool paused) { if (paused && key != null) PlayerPrefs.Save(); }
        void OnDestroy()
        {
            if (runtimePipeline != null)
            {
                if (GraphicsSettings.defaultRenderPipeline == runtimePipeline) GraphicsSettings.defaultRenderPipeline = originalDefault;
                if (QualitySettings.renderPipeline == runtimePipeline) QualitySettings.renderPipeline = originalQuality;
                Destroy(runtimePipeline);
            }
            if (key != null) PlayerPrefs.Save();
            if (Instance == this) Instance = null;
        }
    }
}

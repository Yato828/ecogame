using UnityEngine;

namespace Wattshift
{
    /// <summary>Presentation for source-derived window faces. Original facade materials stay intact.</summary>
    public sealed class BuildingEnergyView : MonoBehaviour
    {
        public string BuildingId;
        public Renderer WindowRenderer;
        public Color OnColor = new Color(1f, .77f, .38f);
        MaterialPropertyBlock properties;
        static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");

        public void Apply(BuildingMetrics metrics)
        {
            if (metrics == null || WindowRenderer == null) return;
            // Efficient lamps may draw fewer kW while maintaining illumination: do not dim
            // occupied windows in proportion to efficiency. A scheduled-off lighting load is dark.
            bool lit = metrics.LightingRatio > .0001;
            WindowRenderer.enabled = lit;
            if (!lit) return;
            if (properties == null) properties = new MaterialPropertyBlock();
            WindowRenderer.GetPropertyBlock(properties);
            properties.SetColor(BaseColor, OnColor);
            properties.SetColor(EmissionColor, OnColor * 1.2f);
            WindowRenderer.SetPropertyBlock(properties);
        }
    }
}

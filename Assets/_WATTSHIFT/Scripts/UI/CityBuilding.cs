using UnityEngine;

namespace Wattshift
{
    /// <summary>Presentation metadata for the G1 visual slice. Contains no simulation state.</summary>
    public sealed class CityBuilding : MonoBehaviour
    {
        public string SimulationId;
        public string Title = "Здание";
        public string Category = "Городской объект";
        [TextArea] public string Description = "";
        public bool Essential;

        Renderer[] renderers;
        Color[] originalColors;
        MaterialPropertyBlock properties;
        static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

        public void SetSelected(bool selected)
        {
            if (properties == null) properties = new MaterialPropertyBlock();
            if (renderers == null)
            {
                Renderer[] all = GetComponentsInChildren<Renderer>();
                BuildingEnergyView feedback = GetComponent<BuildingEnergyView>();
                var selectable = new System.Collections.Generic.List<Renderer>();
                foreach (Renderer renderer in all)
                    if (feedback == null || renderer != feedback.WindowRenderer) selectable.Add(renderer);
                renderers = selectable.ToArray();
                originalColors = new Color[renderers.Length];
                for (int i = 0; i < renderers.Length; i++)
                {
                    Material material = renderers[i].sharedMaterial;
                    originalColors[i] = material != null && material.HasProperty(BaseColor)
                        ? material.GetColor(BaseColor) : Color.white;
                }
            }
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null) continue;
                renderers[i].GetPropertyBlock(properties);
                properties.SetColor(BaseColor, selected
                    ? Color.Lerp(originalColors[i], new Color(0.6f, 1f, 0.85f), 0.18f)
                    : originalColors[i]);
                renderers[i].SetPropertyBlock(properties);
            }
        }
    }
}

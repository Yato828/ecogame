using System.Collections.Generic;
using UnityEngine;

namespace Wattshift
{
    public enum ScannerHighlightState { Off, Unexplored, Problem, Resolved }

    /// <summary>Combines scanner and selection tint on the approved source renderers.</summary>
    public sealed class SourceBuildingHighlight : MonoBehaviour
    {
        Renderer[] renderers;
        Color[] originals;
        MaterialPropertyBlock block;
        ScannerHighlightState state;
        bool selected;
        Coroutine pulse;
        float pulseAmount;
        ScannerOutline outline;
        public ScannerHighlightState ScannerState => state;
        static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

        void Cache()
        {
            if (block == null) block = new MaterialPropertyBlock();
            if (renderers != null) return;
            var window = GetComponent<BuildingEnergyView>();
            var targets = new List<Renderer>();
            foreach (Renderer renderer in GetComponentsInChildren<Renderer>())
                if (renderer.gameObject.name != "Scanner hull" && (window == null || renderer != window.WindowRenderer)) targets.Add(renderer);
            renderers = targets.ToArray();
            originals = new Color[renderers.Length];
            for (int i = 0; i < renderers.Length; i++)
            {
                Material material = renderers[i].sharedMaterial;
                originals[i] = material != null && material.HasProperty(BaseColor) ? material.GetColor(BaseColor) : Color.white;
            }
            outline = GetComponent<ScannerOutline>();
            if (outline == null) outline = gameObject.AddComponent<ScannerOutline>();
            outline.Bind(renderers);
        }

        public void SetSelected(bool value)
        {
            if (selected == value && renderers != null) return;
            selected = value; Apply();
        }

        public void SetScannerState(ScannerHighlightState value)
        {
            if (state == value && renderers != null) return;
            state = value; Apply();
        }

        public void PulseSuccess()
        {
            if (pulse != null) StopCoroutine(pulse);
            pulse = StartCoroutine(SuccessPulse());
        }

        System.Collections.IEnumerator SuccessPulse()
        {
            float elapsed = 0;
            while (elapsed < .75f)
            {
                pulseAmount = Mathf.Sin(Mathf.PI * elapsed / .75f) * .55f;
                Apply(); elapsed += Time.unscaledDeltaTime; yield return null;
            }
            pulseAmount = 0; pulse = null; Apply();
        }

        void Apply()
        {
            Cache();
            if (outline != null) outline.SetState(state);
            Color tint = Color.white;
            float amount = 0;
            switch (state)
            {
                case ScannerHighlightState.Unexplored: tint = new Color(.53f, .78f, 1); amount = .33f; break;
                case ScannerHighlightState.Problem: tint = new Color(1, .56f, .23f); amount = .42f; break;
                case ScannerHighlightState.Resolved: tint = new Color(.35f, 1, .65f); amount = .36f; break;
            }
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null) continue;
                Color color = Color.Lerp(originals[i], tint, amount);
                if (pulseAmount > 0) color = Color.Lerp(color, new Color(.3f, 1, .65f), pulseAmount);
                if (selected) color = Color.Lerp(color, new Color(.65f, 1, .86f), .2f);
                renderers[i].GetPropertyBlock(block);
                block.SetColor(BaseColor, color);
                renderers[i].SetPropertyBlock(block);
            }
        }

        void OnDisable()
        {
            if (pulse != null) StopCoroutine(pulse);
            pulse = null; pulseAmount = 0;
            state = ScannerHighlightState.Off; selected = false;
            if (renderers != null) Apply();
        }
    }
}

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Wattshift
{
    /// <summary>Inverted hull of the actual source mesh, enabled only for scanner targets.</summary>
    public sealed class ScannerOutline : MonoBehaviour
    {
        readonly List<MeshRenderer> hulls = new List<MeshRenderer>();
        Material material;
        MaterialPropertyBlock properties;
        public bool Visible { get; private set; }
        public void Bind(Renderer[] sources)
        {
            if (material != null) return;
            Shader shader = Resources.Load<Shader>("ScannerOutline");
            if (shader == null) throw new System.InvalidOperationException("Scanner outline shader is missing.");
            material = new Material(shader) { name = "Source-mesh scanner outline" };
            properties = new MaterialPropertyBlock();
            foreach (Renderer source in sources)
            {
                var filter = source.GetComponent<MeshFilter>();
                if (filter == null || filter.sharedMesh == null) continue;
                var child = new GameObject("Scanner hull") { hideFlags = HideFlags.DontSave };
                child.transform.SetParent(source.transform, false);
                child.AddComponent<MeshFilter>().sharedMesh = filter.sharedMesh;
                var hull = child.AddComponent<MeshRenderer>();
                hull.sharedMaterial = material; hull.shadowCastingMode = ShadowCastingMode.Off; hull.receiveShadows = false;
                hull.enabled = false; hulls.Add(hull);
            }
        }
        public void SetState(ScannerHighlightState state)
        {
            if (material == null) return;
            Visible = state != ScannerHighlightState.Off;
            Color color = state == ScannerHighlightState.Resolved ? new Color(.2f, 1, .5f)
                : state == ScannerHighlightState.Problem ? new Color(1, .47f, .06f) : new Color(.15f, .7f, 1);
            properties.SetColor("_OutlineColor", color);
            foreach (MeshRenderer hull in hulls) if (hull != null) { hull.enabled = Visible; hull.SetPropertyBlock(properties); }
        }
        void OnDestroy() { if (material != null) Destroy(material); }
    }
}

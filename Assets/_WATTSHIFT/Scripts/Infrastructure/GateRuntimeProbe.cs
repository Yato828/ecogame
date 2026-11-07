#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;

namespace Wattshift
{
    /// <summary>Editor-only evidence sampler; absent from player builds.</summary>
    public sealed class GateRuntimeProbe : MonoBehaviour
    {
        public static readonly List<float> Frames = new List<float>();
        public static int Width, Height;
        public static float Aspect, OrthographicSize;
        float started;
        void OnEnable() { Frames.Clear(); started = Time.realtimeSinceStartup; }
        void Update()
        {
            Width = Screen.width; Height = Screen.height;
            Aspect = Camera.main.aspect; OrthographicSize = Camera.main.orthographicSize;
            if (Time.realtimeSinceStartup - started > 8 && Time.unscaledDeltaTime > 0) Frames.Add(Time.unscaledDeltaTime);
        }
    }
}
#endif

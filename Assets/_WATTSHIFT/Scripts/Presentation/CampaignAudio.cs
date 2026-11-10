using System;
using System.Collections.Generic;
using UnityEngine;

namespace Wattshift.Presentation
{
    /// <summary>Small cached, project-authored UI tones; no external recordings or streaming.</summary>
    [DefaultExecutionOrder(-50)]
    public sealed class CampaignAudio : MonoBehaviour
    {
        public static CampaignAudio Instance { get; private set; }
        const int VoiceLimit = 4;
        readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>(StringComparer.Ordinal);
        readonly AudioSource[] voices = new AudioSource[VoiceLimit];
        readonly double[] voiceEnds = new double[VoiceLimit];
        GameSession session;
        string key;
        float volume = .65f;
        bool muted;
        double lastSoundAt = -10;
        string lastSound;

        public float Volume
        {
            get => volume;
            set
            {
                volume = float.IsNaN(value) || float.IsInfinity(value) ? .65f : Mathf.Clamp01(value);
                foreach (AudioSource voice in voices) if (voice != null) voice.volume = volume;
                if (key != null) { PlayerPrefs.SetFloat(key + ".volume", volume); PersistentSettings.Request(); }
            }
        }
        public bool Muted
        {
            get => muted;
            set
            {
                muted = value;
                if (muted) foreach (AudioSource voice in voices) if (voice != null) voice.Stop();
                if (key != null) { PlayerPrefs.SetInt(key + ".muted", value ? 1 : 0); PersistentSettings.Request(); }
            }
        }

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
            key = PresentationSettingsScope.Key("audio");
            volume = Mathf.Clamp01(PlayerPrefs.GetFloat(key + ".volume", .65f));
            if (float.IsNaN(volume) || float.IsInfinity(volume)) volume = .65f;
            muted = PlayerPrefs.GetInt(key + ".muted", 0) != 0;
            for (int i = 0; i < voices.Length; i++)
            {
                AudioSource voice = gameObject.AddComponent<AudioSource>();
                voice.playOnAwake = false; voice.loop = false; voice.spatialBlend = 0;
                voice.volume = volume; voice.priority = 32; voice.ignoreListenerPause = true;
                voices[i] = voice;
            }
            clips.Add("select", Tone("select", .07f, 880, 1046));
            clips.Add("scanner", Tone("scanner", .13f, 523, 784));
            clips.Add("inspect", Tone("inspect", .16f, 659, 988));
            clips.Add("purchase", Tone("purchase", .21f, 523, 659, 784));
            clips.Add("event", Tone("event", .16f, 740, 740));
            clips.Add("success", Tone("success", .38f, 523, 659, 784, 1046));
            clips.Add("error", Tone("error", .14f, 247, 196));
        }

        void OnEnable() { TryBind(); }
        void Start() { TryBind(); }
        void TryBind()
        {
            if (session != null || Instance != this) return;
            session = GameSession.Instance;
            if (session != null) session.Feedback += PlayFeedback;
        }

        public void PlayFeedback(string feedback)
        {
            if (muted || volume <= 0 || string.IsNullOrEmpty(feedback)) return;
            string sound = feedback.StartsWith("purchase:", StringComparison.Ordinal) ? "purchase" : feedback;
            if (!clips.TryGetValue(sound, out AudioClip clip)) return;
            double now = Time.realtimeSinceStartupAsDouble;
            if (now - lastSoundAt < .045 || (sound == lastSound && now - lastSoundAt < .1)) return;
            for (int i = 0; i < voices.Length; i++)
            {
                if (voices[i] == null || voiceEnds[i] > now) continue;
                // Each channel owns at most one one-shot, even after focus/audio-device changes.
                voices[i].Stop(); voices[i].PlayOneShot(clip);
                voiceEnds[i] = now + clip.length + .025;
                lastSound = sound; lastSoundAt = now;
                return;
            }
        }

        static AudioClip Tone(string name, float seconds, params float[] notes)
        {
            const int rate = 22050;
            int count = Mathf.CeilToInt(seconds * rate);
            var samples = new float[count];
            double noteLength = seconds / notes.Length;
            for (int i = 0; i < samples.Length; i++)
            {
                double time = (double)i / rate;
                int index = Math.Min(notes.Length - 1, (int)(time / noteLength));
                double local = time - index * noteLength;
                double attack = Math.Min(1, local / .005);
                double release = Math.Min(1, Math.Max(0, noteLength - local) / .018);
                double phase = 2 * Math.PI * notes[index] * local;
                samples[i] = (float)((Math.Sin(phase) + .12 * Math.Sin(phase * 2)) * attack * release * .16);
            }
            var clip = AudioClip.Create("WATTSHIFT authored " + name, count, 1, rate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        void OnApplicationPause(bool paused) { if (paused && key != null) PlayerPrefs.Save(); }
        void OnDestroy()
        {
            if (session != null) session.Feedback -= PlayFeedback;
            foreach (AudioSource voice in voices) if (voice != null) Destroy(voice);
            foreach (AudioClip clip in clips.Values) if (clip != null) Destroy(clip);
            clips.Clear();
            if (key != null) PlayerPrefs.Save();
            if (Instance == this) Instance = null;
        }
    }

    internal static class PresentationSettingsScope
    {
        public static string Key(string category)
        {
            string prefix = "WATTSHIFT.G3.";
            string scope = Array.IndexOf(Environment.GetCommandLineArgs(), "-wattshiftEvidence") >= 0 ? Application.temporaryCachePath + "/WattshiftValidationG4" : null;
#if UNITY_EDITOR
            if (!string.IsNullOrEmpty(GameSession.EditorTestSaveDirectory)) scope = GameSession.EditorTestSaveDirectory;
#endif
            if (!string.IsNullOrEmpty(scope))
            {
                uint hash = 2166136261;
                unchecked { foreach (char character in scope) { hash ^= character; hash *= 16777619; } }
                prefix += "Test." + hash.ToString("x8") + ".";
            }
            return prefix + category;
        }
    }
}

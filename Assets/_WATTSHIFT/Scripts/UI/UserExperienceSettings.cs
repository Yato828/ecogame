using System;
using UnityEngine;
using Wattshift.Presentation;

namespace Wattshift
{
    /// <summary>Device-local interface preferences. Uses the presentation test scope.</summary>
    public static class UserExperienceSettings
    {
        public static event Action Changed;
        static readonly int[] ScaleChoices = { 90, 100, 115, 130 };
        public static int ScalePercent
        {
            get
            {
                int value = PlayerPrefs.GetInt(PresentationSettingsScope.Key("ui.scale"), 100);
                return Array.IndexOf(ScaleChoices, value) >= 0 ? value : 100;
            }
            set
            {
                if (Array.IndexOf(ScaleChoices, value) < 0 || ScalePercent == value) return;
                PlayerPrefs.SetInt(PresentationSettingsScope.Key("ui.scale"), value);
                PlayerPrefs.Save(); Changed?.Invoke();
            }
        }
        public static float ScaleFactor => ScalePercent / 100f;
        public static bool ExpandedCard
        {
            get => PlayerPrefs.GetInt(PresentationSettingsScope.Key("ui.expandedCard"), 0) != 0;
            set
            {
                if (ExpandedCard == value) return;
                PlayerPrefs.SetInt(PresentationSettingsScope.Key("ui.expandedCard"), value ? 1 : 0);
                PlayerPrefs.Save(); Changed?.Invoke();
            }
        }
        public static float EffectiveScale => (Screen.width < Screen.height
            ? Mathf.Max(.9f, Screen.width / 430f)
            : Mathf.Clamp(Screen.width / 1600f, .7f, 2f)) * ScaleFactor;
    }
}

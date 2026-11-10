using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Wattshift.Tests
{
    public sealed class CityTaskTests
    {
        [Test] public void CatalogueHasSeventeenCoherentSituationsAndSevenRealUpgrades()
        {
            var c = CityTaskCampaign.Load();
            Assert.AreEqual(17, c.Tasks.Length);
            CollectionAssert.AreEqual(new[] { 5, 5, 4, 3 }, Enumerable.Range(0, 4).Select(ch => c.Tasks.Count(t => t.Chapter == ch)));
            Assert.AreEqual(17, c.Tasks.Select(t => t.Id).Distinct().Count());
            Assert.AreEqual(7, c.Tasks.Count(t => !string.IsNullOrEmpty(t.UpgradeId)));
            foreach (var t in c.Tasks)
            {
                Assert.AreEqual(3, t.Choices.Length, t.Id); Assert.AreEqual(3, t.Explanations.Length, t.Id);
                Assert.That(t.Best, Is.InRange(0, 2)); Assert.IsNotEmpty(t.Explanation);
                if (t.Kind == "find") { Assert.AreEqual(3, t.Sources.Length); Assert.That(t.SourceBest, Is.InRange(0, 2)); }
            }
        }
        [Test] public void StarsReflectMistakesWithoutBlockingCompletionAndMalformedProgressIsRejected()
        {
            var c = CityTaskCampaign.Load(); var s = new TaskProgress();
            Assert.IsTrue(c.Valid(s)); Assert.AreEqual(0, c.ChapterStars(s, 0));
            s.Completed = c.Tasks.Take(5).Select(t => t.Id).ToArray(); s.Index = 4;
            Assert.IsTrue(c.Valid(s)); Assert.AreEqual(3, c.ChapterStars(s, 0));
            s.Errors = s.Completed.Take(2).ToArray(); Assert.AreEqual(2, c.ChapterStars(s, 0));
            s.Errors = s.Completed; Assert.AreEqual(1, c.ChapterStars(s, 0));
            s.Index = 8; Assert.IsFalse(c.Valid(s)); s.Index = 4;
            s.Completed[0] = "unknown"; Assert.IsFalse(c.Valid(s));
        }
        [Test] public void AdditiveSaveFieldsLeaveLegacyContainersReadable()
        {
            var legacy = JsonUtility.FromJson<CampaignSave>("{\"Version\":3,\"MissionIndex\":1}");
            Assert.AreEqual(3, legacy.Version); Assert.AreEqual(1, legacy.MissionIndex); Assert.IsTrue(legacy.Tasks == null || legacy.Tasks.Index == 0 && (legacy.Tasks.Completed == null || legacy.Tasks.Completed.Length == 0));
            var s = new TaskProgress { TutorialDone = true };
            var restored = JsonUtility.FromJson<CampaignSave>(JsonUtility.ToJson(new CampaignSave { Tasks = s }));
            Assert.IsTrue(restored.Tasks.TutorialDone); Assert.IsTrue(CityTaskCampaign.Load().Valid(restored.Tasks));
        }
    }
}

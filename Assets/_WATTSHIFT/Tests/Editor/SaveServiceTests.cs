using System;
using System.IO;
using NUnit.Framework;

namespace Wattshift.Tests
{
    public sealed class SaveServiceTests
    {
        string directory;
        SaveService service;
        [SetUp] public void Setup()
        {
            directory = Path.Combine(Path.GetTempPath(), "wattshift-save-test-" + Guid.NewGuid().ToString("N"));
            service = new SaveService(Path.Combine(directory, "progress.json"));
        }
        [TearDown] public void Cleanup() { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
        [Test] public void SaveReadAndAtomicOverwriteKeepVersionedStateAndBackup()
        {
            var state = new CityState { Version = 1, MissionId = "test", Buildings = new[] { new BuildingState { BuildingId = "school", UpgradeIds = new[] { "lights" } } } };
            Assert.IsTrue(service.TrySave(state, out _));
            Assert.IsTrue(service.TryLoad(out CityState loaded, out _));
            Assert.AreEqual("lights", loaded.Buildings[0].UpgradeIds[0]);
            state.Completed = true;
            Assert.IsTrue(service.TrySave(state, out _));
            Assert.IsTrue(service.TryLoad(out loaded, out _)); Assert.IsTrue(loaded.Completed);
            Assert.IsTrue(File.Exists(service.FilePath + ".bak")); Assert.IsFalse(File.Exists(service.FilePath + ".tmp"));
        }
        [TestCase("not-json")]
        [TestCase("{broken")]
        [TestCase("{\"Version\":99,\"MissionId\":\"test\"}")]
        [TestCase("{\"MissionId\":\"test\"}")]
        public void InvalidFilesProduceExplicitFailureAndRemainAvailableForDiagnosis(string text)
        {
            Directory.CreateDirectory(directory); File.WriteAllText(service.FilePath, text);
            Assert.IsFalse(service.TryLoad(out _, out string message)); Assert.IsNotEmpty(message);
            Assert.AreEqual(text, File.ReadAllText(service.FilePath));
        }
        [Test] public void MissingSaveReturnsExplicitFailure()
        {
            Assert.IsFalse(service.TryLoad(out _, out string message)); Assert.IsNotEmpty(message);
        }
    }
}

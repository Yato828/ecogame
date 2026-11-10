using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Wattshift.Tests
{
    public sealed class G5PersistenceTests
    {
        string directory;
        CampaignSaveService service;
        GameObject callbackHost;
        Action<bool> completionHandler;
        bool originalPending;

        static void SetPending(bool value) => typeof(BrowserPersistence)
            .GetProperty(nameof(BrowserPersistence.Pending)).GetSetMethod(true)
            .Invoke(null, new object[] { value });

        [SetUp] public void Setup()
        {
            directory = Path.Combine(Path.GetTempPath(), "wattshift-g5-save-test-" + Guid.NewGuid().ToString("N"));
            service = new CampaignSaveService(Path.Combine(directory, "campaign.json"));
            originalPending = BrowserPersistence.Pending;
        }

        [TearDown] public void Cleanup()
        {
            if (completionHandler != null) BrowserPersistence.Completed -= completionHandler;
            if (callbackHost != null) UnityEngine.Object.DestroyImmediate(callbackHost);
            SetPending(originalPending);
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }

        [TestCase(-1)]
        [TestCase(2)]
        [TestCase(4)]
        public void RejectedWriteContainerPreservesCurrentAndBackup(int version)
        {
            Assert.That(service.Save(new CampaignSave { ScannerEnabled = true }, out string message), Is.True, message);
            Assert.That(service.Save(new CampaignSave { MissionIndex = 1 }, out message), Is.True, message);
            byte[] current = File.ReadAllBytes(service.FilePath);
            byte[] backup = File.ReadAllBytes(service.FilePath + ".bak");
            CampaignSave rejected = version == -1 ? null : new CampaignSave { Version = version };

            Assert.That(service.Save(rejected, out message), Is.False);
            Assert.That(message, Is.Not.Empty);
            Assert.That(File.ReadAllBytes(service.FilePath), Is.EqualTo(current));
            Assert.That(File.ReadAllBytes(service.FilePath + ".bak"), Is.EqualTo(backup));
            Assert.That(File.Exists(service.FilePath + ".tmp"), Is.False);
            Assert.That(service.Load(out var restored, out message), Is.True, message);
            Assert.That(restored.MissionIndex, Is.EqualTo(1));
        }

        [Test] public void FailedFinalReplacementKeepsCurrentReadableAndRemovesStagingFile()
        {
            Assert.That(service.Save(new CampaignSave { ScannerEnabled = true }, out string message), Is.True, message);
            byte[] current = File.ReadAllBytes(service.FilePath);
            // Unlike the existing staging-denial regression, serialization succeeds here;
            // the destination backup directory prevents committing the replacement.
            Directory.CreateDirectory(service.FilePath + ".bak");

            Assert.That(service.Save(new CampaignSave { ScannerEnabled = false }, out message), Is.False);
            Assert.That(message, Is.Not.Empty);
            Assert.That(File.ReadAllBytes(service.FilePath), Is.EqualTo(current));
            Assert.That(File.Exists(service.FilePath + ".tmp"), Is.False);
            Assert.That(service.Load(out var restored, out message), Is.True, message);
            Assert.That(restored.ScannerEnabled, Is.True);
        }

        [TestCase("ok", true)]
        [TestCase("error", false)]
        [TestCase("timeout", false)]
        public void OnlyCurrentBrowserTicketCompletesAndLateCallbacksCannotChangeResult(string status, bool success)
        {
            Assert.That(originalPending, Is.False, "An unrelated browser synchronization must not be disturbed.");
            callbackHost = new GameObject("G5 persistence callback test");
            var receiver = callbackHost.AddComponent<BrowserPersistence>();
            // Seed only the native-unavailable asynchronous boundary. Exercise the real
            // public callback protocol; this does not claim to exercise IDBFS or Request.
            typeof(BrowserPersistence).GetField("ticket", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(receiver, 42);
            SetPending(true);
            var completions = new List<bool>();
            completionHandler = value => completions.Add(value);
            BrowserPersistence.Completed += completionHandler;

            receiver.OnStorageSync("41:ok");
            receiver.OnStorageSync("43:error");
            receiver.OnStorageSync("malformed");
            receiver.OnStorageSync("invalid:ok");
            Assert.That(BrowserPersistence.Pending, Is.True);
            Assert.That(completions, Is.Empty);

            receiver.OnStorageSync("42:" + status);
            Assert.That(BrowserPersistence.Pending, Is.False);
            Assert.That(completions, Is.EqualTo(new[] { success }));
            receiver.OnStorageSync("42:ok");
            receiver.OnStorageSync("42:error");
            Assert.That(completions, Is.EqualTo(new[] { success }));
        }
    }
}

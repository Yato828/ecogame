using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
namespace Wattshift.Tests
{
    public sealed class G4MigrationTests
    {
        string folder;MissionDefinition[] oldMissions,newMissions;
        [SetUp]public void Setup(){folder=Path.Combine(Path.GetTempPath(),"WattshiftG4Migration-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);string[] names={"01-evening-peak","02-dark-streets","03-energy-of-life"};oldMissions=names.Select(n=>AssetDatabase.LoadAssetAtPath<MissionDefinition>("Assets/_WATTSHIFT/Data/Campaign/"+n+".asset")).ToArray();newMissions=names.Select(n=>AssetDatabase.LoadAssetAtPath<MissionDefinition>("Assets/_WATTSHIFT/Data/CampaignG4/"+n+".asset")).ToArray();}
        [TearDown]public void Clean(){Directory.Delete(folder,true);}
        [Test]public void ActualG3CampaignMigratesWithoutDestroyingAnyOldFileOrUnlocks()
        {
            string fixture=Path.Combine(Application.dataPath,"_WATTSHIFT/Tests/Editor/Fixtures/G3CompletedCampaign.json");
            string original=File.ReadAllText(fixture);string oldPath=Path.Combine(folder,"old.json");File.WriteAllText(oldPath,original);
            var read=new CampaignSaveService(oldPath);Assert.IsTrue(read.Load(out var oldSave,out _));
            foreach(var record in oldSave.Records)
            {
                var definition=oldMissions.Single(m=>m.Id==record.MissionId);var verifier=new CitySimulationService(definition);
                var restored=verifier.TryRestore(record.BestRun);Assert.IsTrue(restored.Success,definition.Id+": "+restored.Message);
            }
            var destination=new CampaignSaveService(Path.Combine(folder,"new.json"));
            Assert.IsTrue(CampaignUpgradeMigration.TryMigrate(oldPath,destination,oldMissions,newMissions,out string message),message);
            Assert.AreEqual(original,File.ReadAllText(oldPath));Assert.IsTrue(destination.Load(out var converted,out _));
            var campaign=new CampaignProgress(newMissions);Assert.IsTrue(campaign.TryRestore(converted.Records,out message),message);
            Assert.AreEqual(9,campaign.TotalStars);Assert.IsTrue(campaign.IsUnlocked(2));Assert.That(campaign.BestScore(0),Is.InRange(85,100));
            var sim=new CitySimulationService(newMissions[converted.MissionIndex]);Assert.IsTrue(sim.TryRestore(converted.City).Success);
        }
        [Test]public void CorruptOldProgressDoesNotCreateDestinationOrOverwritePreviousData()
        {
            string path=Path.Combine(folder,"old.json");File.WriteAllText(path,"{invalid");
            var destination=new CampaignSaveService(Path.Combine(folder,"new.json"));Assert.IsFalse(CampaignUpgradeMigration.TryMigrate(path,destination,oldMissions,newMissions,out _));Assert.IsFalse(destination.Exists);Assert.AreEqual("{invalid",File.ReadAllText(path));
        }
    }
}

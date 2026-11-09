using NUnit.Framework;
using UnityEditor;
namespace Wattshift.Tests
{
    public sealed class G4PreviewTests
    {
        [Test] public void PreviewMatchesActualRemainingEnergyAndDoesNotPurchaseOrSpend()
        {
            var m=AssetDatabase.LoadAssetAtPath<MissionDefinition>("Assets/_WATTSHIFT/Data/CampaignG4/01-evening-peak.asset");
            var before=new CitySimulationService(m);before.AdvanceTo(19);
            var preview=before.PreviewPurchase("school","school-lights");Assert.IsTrue(preview.Available);Assert.IsTrue(preview.Safe);Assert.Greater(preview.SavedEnergyKWh,0);
            Assert.IsFalse(before.IsPurchased("school","school-lights"));Assert.AreEqual(650,before.Evaluate(19).Budget);
            var after=new CitySimulationService(m);after.AdvanceTo(19);Assert.IsTrue(after.TryPurchase("school","school-lights").Success);
            before.AdvanceTo(23);after.AdvanceTo(23);Assert.That(before.Evaluate(23).AccumulatedEnergyKWh-after.Evaluate(23).AccumulatedEnergyKWh,Is.EqualTo(preview.SavedEnergyKWh).Within(.0001));
        }
        [Test] public void UnsafeAndUnaffordablePreviewsExposeActualValidationFailure()
        {
            var m=AssetDatabase.LoadAssetAtPath<MissionDefinition>("Assets/_WATTSHIFT/Data/CampaignG4/01-evening-peak.asset");var sim=new CitySimulationService(m);
            Assert.IsFalse(sim.PreviewPurchase("school","school-dark").Available);Assert.IsFalse(sim.PreviewPurchase("school","school-expensive").Available);
            Assert.AreEqual(650,sim.Evaluate(17).Budget);
        }
    }
}

namespace Wattshift
{
    public sealed class UpgradePreview
    {
        public bool Available, Safe;
        public string Reason;
        public double SavedEnergyKWh, CurrentPowerKW;
    }
}

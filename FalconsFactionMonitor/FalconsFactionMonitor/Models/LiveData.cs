namespace FalconsFactionMonitor.Models
{
    public class LiveData
    {
        public string SystemName { get; set; }
        public string FactionName { get; set; }
        public double InfluencePercent { get; set; }
        public string SecurityLevel { get; set; }
        public string EconomyLevel { get; set; }
        public bool IsPlayer { get; set; }
        public bool NativeFaction { get; set; }
        public string LastUpdated { get; set; }
    }
}

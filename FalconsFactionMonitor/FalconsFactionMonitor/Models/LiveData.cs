namespace FalconsFactionMonitor.Models
{
    public class LiveData
    {
        public string SystemName { get; set; }
        public string FactionName { get; set; }
        public decimal InfluencePercent { get; set; }
        public string State { get; set; }
        public bool IsPlayer { get; set; }
        public bool NativeFaction { get; set; }
        public string LastUpdated { get; set; }
    }
}

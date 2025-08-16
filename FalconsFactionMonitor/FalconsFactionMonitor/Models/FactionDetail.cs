namespace FalconsFactionMonitor.Models
{
    public class FactionDetail
    {
        public string SystemName { get; set; }
        public string FactionName { get; set; }
        public decimal InfluencePercent { get; set; }
        public decimal Difference { get; set; }
        public bool IsPlayer { get; set; }
        public string LastUpdated { get; set; }
    }
}

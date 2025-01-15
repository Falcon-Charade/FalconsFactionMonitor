namespace FalconsFactionMonitor.Models
{
    public class FactionDetail
    {
        public string SystemName { get; set; }
        public string FactionName { get; set; }
        public double InfluencePercent { get; set; }
        public double Difference { get; set; }
        public string LastUpdated { get; set; }
    }
}

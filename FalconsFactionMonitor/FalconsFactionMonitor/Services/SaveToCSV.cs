using FalconsFactionMonitor.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace FalconsFactionMonitor.Services
{
    internal class SaveToCSV
    {
        internal static void FactionSystems(List<FactionSystem> systems, string filePath)
        {
            using (var writer = new StreamWriter(filePath))
            {
                writer.WriteLine("System Name,Influence Percent,Last Updated");
                foreach (var system in systems)
                {
                    writer.WriteLine($"{system.SystemName},{system.InfluencePercent},{system.LastUpdated}");
                }
            }
        }
        internal static void SystemFactions(List<FactionDetail> factions, string filePath)
        {
            List<LiveData> allFactions = new List<LiveData>();
            foreach (var faction in factions)
            {
                DateTime parsedDate = DateTime.ParseExact(faction.LastUpdated, "d/M/yyyy h:m:s tt", CultureInfo.InvariantCulture);
                var lastUpdated = parsedDate.ToString("yyyy-MM-dd HH:mm:ss");
                allFactions.Add
                    (
                        new LiveData
                        {
                            SystemName = faction.SystemName,
                            FactionName = faction.FactionName,
                            InfluencePercent = faction.InfluencePercent,
                            State = "None",
                            IsPlayer = faction.IsPlayer,
                            LastUpdated = lastUpdated
                        }
                    );
            }
            DatabaseService dbService = new DatabaseService();
            dbService.SaveData(allFactions);
            using (var writer = new StreamWriter(filePath))
            {
                writer.WriteLine("System Name,Faction Name,Influence Percent,Difference,Player Faction,Last Updated");
                foreach (var faction in factions)
                {
                    writer.WriteLine($"{faction.SystemName},{faction.FactionName},{faction.InfluencePercent},{faction.Difference},{faction.IsPlayer},{faction.LastUpdated}");
                }
            }
        }
    }
}

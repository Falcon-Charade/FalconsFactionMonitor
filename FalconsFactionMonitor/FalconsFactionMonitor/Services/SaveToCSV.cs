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
            using var writer = new StreamWriter(filePath);
            writer.WriteLine("System Name,Influence Percent,Last Updated");
            foreach (var system in systems)
            {
                writer.WriteLine($"{system.SystemName},{system.InfluencePercent},{system.LastUpdated}");
            }
        }
        internal static void SystemFactions(List<FactionDetail> factions, string filePath)
        {
            using var writer = new StreamWriter(filePath);
            writer.WriteLine("System Name,Faction Name,Influence Percent,Difference,Player Faction,Last Updated");
            foreach (var faction in factions)
            {
                writer.WriteLine($"{faction.SystemName},{faction.FactionName},{faction.InfluencePercent},{faction.Difference},{faction.IsPlayer},{faction.LastUpdated}");
            }
        }
    }
}

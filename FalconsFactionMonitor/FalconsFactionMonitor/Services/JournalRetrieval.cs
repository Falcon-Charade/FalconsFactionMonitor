using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FalconsFactionMonitor.Services
{
    internal class JournalRetrievalService
    {
       internal void JournalRetrieval()
        {
            var monitor = new JournalMonitor();

            // Subscribe to the OnFSDJumpDetected event
            monitor.OnFSDJumpDetected += factions =>
            {
                Console.WriteLine("New FSD Jump detected! Processing factions...");
                foreach (var faction in factions)
                {
                    Console.WriteLine($"{faction.SystemName} - {faction.FactionName}: {faction.InfluencePercent}% influence");
                }
            };

            monitor.StartMonitoring();
            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
        }
    }
}

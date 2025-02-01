using System;
using System.Threading.Tasks;

namespace FalconsFactionMonitor.Services
{
    internal class JournalRetrievalService
    {
       internal Task JournalRetrieval()
        {
            try
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
                Console.WriteLine("\n\nProgram finished execution. Press any key to exit...");
                Console.ReadKey(); // Wait for user input before closing.
                return Task.CompletedTask;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred: {ex.Message}");
                return Task.FromException( ex );
            }
        }
    }
}

using System;
using System.Diagnostics;
using System.Linq;
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
                    foreach (var faction in factions.OrderByDescending(f => f.InfluencePercent))
                    {
                        Console.WriteLine($"{faction.SystemName} - {faction.FactionName}: {faction.InfluencePercent}% influence");
                    }
                };

                // Start monitoring
                monitor.StartMonitoring();

                // Keep the application running while EliteDangerous64.exe is still open
                while (IsEliteRunning())
                {
                    // Just sleep for a bit so we're not burning CPU
                    System.Threading.Thread.Sleep(2000);
                }

                // Once Elite closes, we can gracefully shut down (if you had any cleanup)
                Console.WriteLine("Elite Dangerous has closed. Stopping the journal monitor...");
                monitor.StopMonitoring(); // Optional if you build a StopMonitoring() method

                return Task.CompletedTask;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred: {ex.Message}");
                return Task.FromException( ex );
            }
        }
        private bool IsEliteRunning()
        {
            // Check if EliteDangerous64.exe is in the list of running processes
            var processes = Process.GetProcessesByName("EliteDangerous64");
            return processes.Length > 0;
        }
    }
}

using FalconsFactionMonitor.Helpers;
using System;
using System.Data.SqlClient;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace FalconsFactionMonitor.Services
{
internal class JournalRetrievalService
{
    internal async Task JournalRetrieval()
    {
        try
        {
            var monitor = new JournalMonitor();
            var solutionRoot = Directory.GetCurrentDirectory();
            string currentDirectory = AppDomain.CurrentDomain.BaseDirectory;

            if (currentDirectory.Contains(@"\bin\Debug") || currentDirectory.Contains(@"\bin\Release"))
            {
                solutionRoot = Directory.GetParent(currentDirectory)?.Parent?.Parent?.Parent?.FullName;
            }
            else
            {
                solutionRoot = currentDirectory;
            }

            string connectionString = DatabaseConnectionBuilder.BuildConnectionString();
            SqlConnection connection = new(connectionString);

            // Subscribe to the OnFSDJumpDetected event
            monitor.OnFSDJumpDetected += factions =>
            {
                DatabaseService dbService = new();
                DatabaseService.SaveData(factions);
            };

            // Start monitoring
            monitor.StartMonitoring();

            // Keep monitoring while EliteDangerous64.exe is running
            while (IsEliteRunning())
            {
                // Just wait for a bit so we're not burning CPU
                await Task.Delay(2000); // Use await instead of blocking sleep
            }

            // Once Elite closes, we can gracefully shut down (if you had any cleanup)
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("\n\nElite Dangerous has closed. Stopping the journal monitor...");
            Console.ResetColor();
            monitor.StopMonitoring(); // Optional if you build a StopMonitoring() method
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.DarkRed;
            Console.Write("An error occurred: ");
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine($"{ex.Message}");
            Console.ResetColor();
        }
    }
    private static bool IsEliteRunning()
    {
        // Check if EliteDangerous64.exe is in the list of running processes
        var processes = Process.GetProcessesByName("EliteDangerous64");
        return processes.Length > 0;
    }
}
}

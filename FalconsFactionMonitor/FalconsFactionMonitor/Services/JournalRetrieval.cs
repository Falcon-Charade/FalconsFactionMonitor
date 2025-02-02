using System;
using System.Configuration;
using System.Data.SqlClient;
using System.Diagnostics;
using System.IO;
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
                var solutionRoot = Directory.GetParent(AppDomain.CurrentDomain.BaseDirectory)?.Parent?.Parent?.FullName;
                string filePath = Path.Combine(solutionRoot, "Services", "StoredProcInsert.sql");
                string storedProc = File.ReadAllText(filePath);
                string connectionString = ConfigurationManager.ConnectionStrings["DefaultConnection"].ConnectionString;

                // Subscribe to the OnFSDJumpDetected event
                monitor.OnFSDJumpDetected += factions =>
                {
                    Console.WriteLine("New FSD Jump detected! Processing factions...");
                    using SqlConnection connection = new SqlConnection(connectionString);
                    {
                        connection.Open();
                        foreach (var faction in factions.OrderByDescending(f => f.InfluencePercent))
                        {
                            Console.ForegroundColor = ConsoleColor.Yellow;
                            Console.Write($"{faction.SystemName} - {faction.FactionName}: ");
                            Console.ForegroundColor = ConsoleColor.White;
                            Console.WriteLine($"{faction.InfluencePercent}% influence");


                            using SqlCommand command = new SqlCommand(storedProc, connection);
                            {
                                command.Parameters.AddWithValue("@SystemName", faction.SystemName);
                                command.Parameters.AddWithValue("@FactionName", faction.FactionName);
                                command.Parameters.AddWithValue("@Influence", faction.InfluencePercent);
                                command.Parameters.AddWithValue("@State", faction.State);
                                command.Parameters.AddWithValue("@PlayerFaction", faction.IsPlayer);
                                command.Parameters.AddWithValue("@LastUpdated", faction.LastUpdated);
                                command.ExecuteNonQuery();
                            }
                        }
                        connection.Close();
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
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine("Elite Dangerous has closed. Stopping the journal monitor...");
                Console.ResetColor();
                monitor.StopMonitoring(); // Optional if you build a StopMonitoring() method

                return Task.CompletedTask;
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.DarkRed;
                Console.Write("An error occurred: ");
                Console.ForegroundColor = ConsoleColor.White;
                Console.WriteLine($"{ex.Message}");
                Console.ResetColor();
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

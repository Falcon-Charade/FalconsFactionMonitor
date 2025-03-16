using FalconsFactionMonitor.Models;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Threading;

namespace FalconsFactionMonitor.Services
{
    class DatabaseService
    {
        public void SaveData(List<LiveData> factions)
        {
            var solutionRoot = Directory.GetCurrentDirectory();
            string currentDirectory = AppDomain.CurrentDomain.BaseDirectory;
            if (currentDirectory.Contains(@"\bin\Debug") || currentDirectory.Contains(@"\bin\Release"))
            {
                solutionRoot = Directory.GetParent(currentDirectory)?.Parent?.Parent?.FullName;
            }
            else
            {
                solutionRoot = currentDirectory;
            }
            string filePath = Path.Combine(solutionRoot, "Services", "StoredProcInsert.sql");
            string storedProc = File.ReadAllText(filePath);
            string connectionString = Registry.GetValue(@"HKEY_CURRENT_USER\SOFTWARE\Falcon Charade", "FalconsFactionMonitorDbConnection", null).ToString();
            SqlConnection connection = new SqlConnection(connectionString);
            for (int i = 1; i <= 5; i++)
            {
                try
                {
                    connection.Open();
                    break;
                }
                catch (Exception ex)
                {
                    if (ex.Message.StartsWith("Database 'FalconsFactionMonitor' on server 'falcons-sql.database.windows.net' is not currently available."))
                    {
                        if (i == 5)
                        {
                            throw;
                        }
                        Thread.Sleep(10000);
                    }
                    else
                    {
                        throw;
                    }
                }
            }
            foreach (var faction in factions.OrderByDescending(f => f.InfluencePercent))
            {
                if (faction.InfluencePercent > 0)
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
            }
            connection.Close();
        }
    }
}

using FalconsFactionMonitor.Models;
using System;
using System.Collections.Generic;
using System.IO;

namespace FalconsFactionMonitor.Services
{
    internal class WebRetrievalService
    {
        internal async void WebRetrieval()
        {
            Console.WriteLine("Do you wish to retrieve details from Inara? (Y/N)");
            string inaraParseCheck = Console.ReadLine();
            bool inaraParse = false;
            if (inaraParseCheck.ToUpper().StartsWith("Y"))
            {
                inaraParse = true;
            }

            Console.Clear();
            Console.Write("Enter the faction name: ");
            string factionName = Console.ReadLine();

            if (string.IsNullOrEmpty(factionName))
            {
                Console.WriteLine("Faction name cannot be empty.");
                return;
            }

            try
            {
                List<FactionSystem> systems = new List<FactionSystem>();
                var solutionRoot = Directory.GetParent(AppDomain.CurrentDomain.BaseDirectory)?.Parent?.Parent?.FullName;
                List<FactionDetail> previousFactions;
                if (solutionRoot == null)
                {
                    throw new Exception("Unable to determine solution root directory.");
                }

                var folderPath = Path.Combine(solutionRoot, "Output");
                Directory.CreateDirectory(folderPath);

                var sanitizedFactionName = string.Join("", factionName.Split(Path.GetInvalidFileNameChars())).Replace(" ", "_");
                var datestamp = DateTime.Now.ToString("yyyyMMdd-");
                var filePath = Path.Combine(folderPath, $"{datestamp}{sanitizedFactionName}-Systems.csv");


                if (inaraParse)
                {
                    systems = await GetData.GetFactionSystems(factionName);
                    SaveToCSV.FactionSystems(systems, filePath);
                    Console.WriteLine($"The faction systems have been saved to '{filePath}'.");
                }
                else
                {
                    systems = GetData.GetLatestSystemsCsv(folderPath, sanitizedFactionName, factionName);
                }

                // Retrieve the latest CSV file for comparison
                string latestFilePath = GetData.GetLatestCsvFile(folderPath, sanitizedFactionName, factionName);
                if (latestFilePath != null)
                {
                    previousFactions = GetData.ReadFactionsFromCsv(latestFilePath);
                }
                else
                {
                    previousFactions = null;
                }

                var factionsFilePath = Path.Combine(folderPath, $"{datestamp}{sanitizedFactionName}-Systems-Factions.csv");
                var allFactions = await GetData.GetFactionsInSystems(systems);

                // Calculate the difference
                foreach (var faction in allFactions)
                {
                    if (previousFactions != null)
                    {
                        var previousFaction = previousFactions.Find(f => f.SystemName == faction.SystemName && f.FactionName == faction.FactionName);
                        if (previousFaction != null)
                        {
                            faction.Difference = faction.InfluencePercent - previousFaction.InfluencePercent;
                        }
                        else
                        {
                            faction.Difference = -1;
                        }
                    }
                    else
                    {
                        faction.Difference = -1;
                    }
                }

                SaveToCSV.SystemFactions(allFactions, factionsFilePath);
                Console.WriteLine($"The factions in systems have been saved to '{factionsFilePath}'.");

            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred: {ex.Message}");
            }
            finally
            {
                Console.WriteLine("\n\nProgram finished execution. Press any key to exit...");
                Console.ReadKey(); // Wait for user input before closing.
            }
        }
    }
}

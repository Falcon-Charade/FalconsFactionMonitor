using FalconsFactionMonitor.Models;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Threading.Tasks;

namespace FalconsFactionMonitor.Services
{
    internal class WebRetrievalService
    {
        internal async Task WebRetrieval(string factionName, bool inaraParse = false, bool CSVSave = false)
        {
            if (string.IsNullOrEmpty(factionName) || factionName.ToUpper().Replace(" ","") == "FACTIONNAME")
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("\nFaction name cannot be empty.");
                return;
            }
            if (factionName.ToUpper() == "USC")
            {
                factionName = "United Systems Commonwealth";
            }

            try
            {
                List<FactionSystem> systems = new List<FactionSystem>();
                var solutionRoot = GetSavePath();
                List<FactionDetail> previousFactions;
                if (solutionRoot == null)
                {
                    throw new Exception("Unable to determine directory.");
                }

                var folderPath = Path.Combine(solutionRoot, "Output");
                Directory.CreateDirectory(folderPath);

                var sanitizedFactionName = string.Join("", factionName.Split(Path.GetInvalidFileNameChars())).Replace(" ", "_");
                var datestamp = DateTime.Now.ToString("yyyyMMdd-");
                var filePath = Path.Combine(folderPath, $"{datestamp}{sanitizedFactionName}-Systems.csv");

                //Get System Data and Save to CSV, if inaraParse is true
                if (inaraParse)
                {
                    systems = await GetData.GetFactionSystems(factionName);
                    if (CSVSave)
                    {
                        SaveToCSV.FactionSystems(systems, filePath);
                        Console.ForegroundColor = ConsoleColor.Cyan;
                        Console.Write("\n[INFO] ");
                        Console.ForegroundColor = ConsoleColor.White;
                        Console.WriteLine($"The systems {factionName} can be found in has been saved to '{filePath}'.");
                    }
                }
                else
                {
                    string latestSystemsFileName = GetData.GetLatestCsvFile(folderPath, sanitizedFactionName, factionName, totalList: false);
                    systems = GetData.ReadSystemsFromCsv(latestSystemsFileName);
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
                DatabaseService.WebServicePublish(allFactions);
                if (CSVSave)
                {
                    SaveToCSV.SystemFactions(allFactions, factionsFilePath);
                    Console.ForegroundColor = ConsoleColor.Cyan;
                    Console.Write("\n[INFO] ");
                    Console.ForegroundColor = ConsoleColor.White;
                    Console.WriteLine($"The full faction list for systems {factionName} can be found in has been saved to '{factionsFilePath}'.");
                };

            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.DarkRed;
                Console.Write("\n[ERROR] ");
                Console.ForegroundColor = ConsoleColor.White;
                Console.WriteLine($" {ex.Message}");
            }
            finally
            {
                Console.WriteLine("\n\nProgram finished execution.");
            }
        }

        // Get save path from App.config
        public string GetSavePath()
        {
            string path = ConfigurationManager.AppSettings["CsvSavePath"];

            if (!string.IsNullOrEmpty(path))
            {
                path = Environment.ExpandEnvironmentVariables(path); // Resolve %LOCALAPPDATA%
            }

            return string.IsNullOrEmpty(path) ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "YourManufacturer", "YourProduct") : path;
        }
    }
}

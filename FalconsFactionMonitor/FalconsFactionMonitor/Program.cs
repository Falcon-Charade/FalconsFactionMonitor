using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using FalconsFactionMonitor.Models;
using FalconsFactionMonitor.Services;
using System.Globalization;

class Program
{
    static async Task Main(string[] args)
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
        if (factionName.ToUpper() == "USC")
        {
            factionName = "United Systems Commonwealth";
        }

        try
        {
            //Set Variables and Create Output Directory
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

            //Get System Data and Save to CSV, if inaraParse is true
            List<FactionSystem> systems = new List<FactionSystem>();
            if (inaraParse)
            {
                systems = await GetData.GetFactionSystems(factionName);
                SaveToCSV.FactionSystems(systems, filePath);
                Console.WriteLine($"The faction systems have been saved to '{filePath}'.");
            }

            // Retrieve the latest CSV file for comparison
            string latestSystemsFilePath = GetData.GetLatestCsvFile(folderPath, sanitizedFactionName, factionName, false);
            string latestFactionsFilePath = GetData.GetLatestCsvFile(folderPath, sanitizedFactionName, factionName);
            List<FactionSystem> systemsFromFile = GetData.GetSystemsFromFile(latestSystemsFilePath);
            if (latestFactionsFilePath != null)
            {
                previousFactions = ReadFactionsFromCsv(latestFactionsFilePath);
            }
            else
            {
                previousFactions = null;
            }

            var factionsFilePath = Path.Combine(folderPath, $"{datestamp}{sanitizedFactionName}-Systems-Factions.csv");
            var allFactions = await GetData.GetFactionsInSystems(systemsFromFile);

            // Calculate the difference in influence
            foreach (var faction in allFactions)
            {
                if (previousFactions != null)
                {
                    //var previousFaction = previousFactions.Find(f => f.SystemName == faction.SystemName && f.FactionName == faction.FactionName);
                    dynamic previousFaction = null;
                    foreach (var f in previousFactions)
                    {
                        if (f.SystemName == faction.SystemName && f.FactionName == faction.FactionName)
                        {
                            previousFaction = f;
                            break;
                        }
                    }

                    if (previousFaction != null)
                    {
                        faction.Difference = Math.Round(faction.InfluencePercent - previousFaction.InfluencePercent,2);
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

    private static List<FactionDetail> ReadFactionsFromCsv(string filePath)
    {
        var factions = new List<FactionDetail>();

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"CSV file not found: {filePath}");
        }

        using var reader = new StreamReader(filePath);
        string headerLine = reader.ReadLine(); // Skip header

        string line;
        while ((line = reader.ReadLine()) != null)
        {
            var columns = line.Split(',');
            if (columns.Length < 5) continue;

            if (double.TryParse(columns[2], NumberStyles.Any, CultureInfo.InvariantCulture, out double influencePercent) &&
                double.TryParse(columns[3], NumberStyles.Any, CultureInfo.InvariantCulture, out double difference))
            {
                factions.Add(new FactionDetail
                {
                    SystemName = columns[0],
                    FactionName = columns[1],
                    InfluencePercent = influencePercent,
                    Difference = difference,
                    LastUpdated = columns[4]
                });
            }
        }

        return factions;
    }
}
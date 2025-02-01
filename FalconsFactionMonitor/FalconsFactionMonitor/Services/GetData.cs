using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using FalconsFactionMonitor.Models;
using HtmlAgilityPack;

namespace FalconsFactionMonitor.Services
{
    internal static class GetData
    {
        internal static async Task<List<FactionSystem>> GetFactionSystems(string factionName)
        {
            using var client = new HttpClient();

            string url = $"https://inara.cz/elite/minorfaction/?search={Uri.EscapeDataString(factionName)}";

            var response = await client.GetAsync(url);

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"API call failed with status code: {response.StatusCode}");
            }

            var responseContent = await response.Content.ReadAsStringAsync();

            var htmlDoc = new HtmlDocument();
            htmlDoc.LoadHtml(responseContent);

            var systemsTable = htmlDoc.DocumentNode.SelectSingleNode("//table[contains(@class, 'tablesortercollapsed')]");
            if (systemsTable == null)
            {
                throw new Exception("Failed to find systems table in the HTML response.");
            }

            var rows = systemsTable.SelectNodes(".//tr");
            if (rows == null || rows.Count == 0)
            {
                throw new Exception("No data rows found in the systems table.");
            }

            var systems = new List<FactionSystem>();
            foreach (var row in rows)
            {
                var cells = row.SelectNodes(".//td");
                if (cells == null || cells.Count < 4) continue;

                var systemName = cells[0].InnerText.Trim();
                var influenceText = cells[1].InnerText.Trim();
                var lastUpdatedText = cells[3].InnerText.Trim();

                if (double.TryParse(influenceText.TrimEnd('%'), out double influence))
                {
                    systems.Add(new FactionSystem
                    {
                        SystemName = systemName,
                        InfluencePercent = influence,
                        LastUpdated = lastUpdatedText
                    });
                }
            }

            return systems;
        }

        internal static async Task<List<FactionDetail>> GetFactionsInSystems(List<FactionSystem> systems)
        {
            var allFactions = new List<FactionDetail>();
            using var client = new HttpClient();

            foreach (var system in systems)
            {
                string url = $"https://inara.cz/elite/starsystem/?search={Uri.EscapeDataString(system.SystemName)}";
                var response = await client.GetAsync(url);

                if (!response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"Failed to fetch data for system: {system.SystemName}");
                    continue;
                }

                var responseContent = await response.Content.ReadAsStringAsync();
                var htmlDoc = new HtmlDocument();
                htmlDoc.LoadHtml(responseContent);

                var factionsTable = htmlDoc.DocumentNode.SelectSingleNode("//table[@class='tablesorter']");
                if (factionsTable == null)
                {
                    Console.WriteLine($"No factions table found for system: {system.SystemName}");
                    continue;
                }

                var rows = factionsTable.SelectNodes(".//tr");
                if (rows == null || rows.Count == 0)
                {
                    Console.WriteLine($"No factions data found for system: {system.SystemName}");
                    continue;
                }

                foreach (var row in rows)
                {
                    var cells = row.SelectNodes(".//td");
                    if (cells == null || cells.Count < 6) continue;

                    var factionName = cells[0].InnerText.Trim();
                    var influenceText = cells[5].InnerText.Trim();
                    var lastUpdatedText = cells[3].InnerText.Trim();

                    if (double.TryParse(influenceText.TrimEnd('%'), out double influence))
                    {
                        allFactions.Add(new FactionDetail
                        {
                            SystemName = system.SystemName,
                            FactionName = factionName,
                            InfluencePercent = influence,
                            LastUpdated = lastUpdatedText
                        });
                    }
                }
            }

            return allFactions;
        }
        internal static string GetLatestCsvFile(string directoryPath, string sanitizedFactionName, string factionName)
        {
            if (!Directory.Exists(directoryPath))
            {
                Console.WriteLine($"Directory not found: {directoryPath}");
                return null;
            }

            var pattern = $"*-{sanitizedFactionName}-Systems-Factions.csv";
            var csvFiles = Directory.GetFiles(directoryPath, pattern);
            if (csvFiles.Length == 0)
            {
                Console.WriteLine($"No CSV files found for faction \"{factionName}\" in the directory.");
                return null;
            }

            var latestFile = csvFiles
                .Select(file => new { FileName = file, DatePrefix = Path.GetFileName(file).Split('-')[0] })
                .Where(f => DateTime.TryParseExact(f.DatePrefix, "yyyyMMdd", null, DateTimeStyles.None, out _))
                .OrderByDescending(f => f.DatePrefix)
                .FirstOrDefault();

            if (latestFile == null)
            {
                Console.WriteLine($"No valid datestamp-prefixed CSV files found for faction \"{factionName}\".");
                return null;
            }

            return latestFile.FileName;
        }
        internal static List<FactionSystem> GetLatestSystemsCsv(string directoryPath, string sanitizedFactionName, string factionName)
        {
            if (!Directory.Exists(directoryPath))
            {
                Console.WriteLine($"Directory not found: {directoryPath}");
                return null;
            }

            var pattern = $"*{sanitizedFactionName}-Systems.csv";
            var csvFiles = Directory.GetFiles(directoryPath, pattern);
            if (csvFiles.Length == 0)
            {
                Console.WriteLine($"No CSV files found for faction \"{factionName}\" in the directory.");
                return null;
            }

            var latestFile = csvFiles
                .Select(file => new { FileName = file, DatePrefix = Path.GetFileName(file).Split('-')[0] })
                .Where(f => DateTime.TryParseExact(f.DatePrefix, "yyyyMMdd", null, DateTimeStyles.None, out _))
                .OrderByDescending(f => f.DatePrefix)
                .FirstOrDefault();

            if (latestFile == null)
            {
                Console.WriteLine($"No valid datestamp-prefixed CSV files found for faction \"{factionName}\".");
                return null;
            }
            else
            {
                string latestFileName = latestFile.FileName;
                List<FactionSystem> systems = ReadSystemsFromCsv(latestFileName);
                return systems;
            }
        }
        internal static List<FactionDetail> ReadFactionsFromCsv(string filePath)
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
        internal static List<FactionSystem> ReadSystemsFromCsv(string filePath)
        {
            var systems = new List<FactionSystem>();

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
                if (columns.Length < 3) continue;

                if (double.TryParse(columns[1], NumberStyles.Any, CultureInfo.InvariantCulture, out double influencePercent))
                {
                    systems.Add(new FactionSystem
                    {
                        SystemName = columns[0],
                        InfluencePercent = influencePercent,
                        LastUpdated = columns[2]
                    });
                }
            }

            return systems;
        }
    }
}
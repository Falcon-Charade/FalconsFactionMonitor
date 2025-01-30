using FalconsFactionMonitor.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Newtonsoft.Json.Linq;

namespace FalconsFactionMonitor.Services
{
    internal class JournalMonitor
    {
        private readonly string journalDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                                            "Saved Games", "Frontier Developments");//, "Elite Dangerous");
        private string latestJournalFile;
        private long lastFilePosition = 0;
        private FileSystemWatcher watcher;

        public event Action<List<LiveData>> OnFSDJumpDetected;

        public void StartMonitoring()
        {
            latestJournalFile = GetLatestJournalFile();
            if (latestJournalFile == null)
            {
                Console.WriteLine("No journal file found.");
                return;
            }

            // Initialize FileSystemWatcher to detect file updates
            watcher = new FileSystemWatcher(journalDirectory, Path.GetFileName(latestJournalFile))
            {
                NotifyFilter = NotifyFilters.LastWrite
            };

            watcher.Changed += (s, e) => ProcessNewJournalEntries();
            watcher.EnableRaisingEvents = true;

            Console.WriteLine($"Monitoring journal: {latestJournalFile}");

            // Initial read in case there are existing entries
            ProcessNewJournalEntries();
        }

        private string GetLatestJournalFile()
        {
            var journalFiles = Directory.GetFiles(journalDirectory, "Journal.*.log")
                                        .OrderByDescending(f => f)
                                        .ToList();

            return journalFiles.FirstOrDefault();
        }

        private void ProcessNewJournalEntries()
        {
            if (latestJournalFile == null) return;

            try
            {
                using (var fs = new FileStream(latestJournalFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var reader = new StreamReader(fs))
                {
                    fs.Seek(lastFilePosition, SeekOrigin.Begin);

                    string line;
                    var factionDetails = new List<LiveData>();

                    while ((line = reader.ReadLine()) != null)
                    {
                        var json = JObject.Parse(line);

                        if (json["event"]?.ToString() == "FSDJump")
                        {
                            string systemName = json["StarSystem"]?.ToString();
                            string security = json["SystemSecurity_Localised"]?.ToString() ?? "Unknown";
                            string economy = json["SystemEconomy_Localised"]?.ToString() ?? "Unknown";
                            string lastUpdated = json["timestamp"]?.ToString();

                            var factions = json["Factions"] as JArray;
                            if (factions != null)
                            {
                                foreach (var faction in factions)
                                {
                                    double influence = faction["Influence"]?.ToObject<double>() ?? 0.0;
                                    influence = Math.Round(influence * 100, 2);  // Convert from 0.x to xx.xx

                                    bool isPlayer = faction["SquadronFaction"]?.ToObject<bool>() ?? false;
                                    bool nativeFaction = faction["HomeSystem"]?.ToObject<bool>() ?? false;
                                    factionDetails.Add(new LiveData
                                    {
                                        SystemName = systemName,
                                        FactionName = faction["Name"]?.ToString(),
                                        InfluencePercent = influence,
                                        SecurityLevel = security,
                                        EconomyLevel = economy,
                                        IsPlayer = isPlayer,
                                        NativeFaction = nativeFaction,
                                        LastUpdated = lastUpdated
                                    });
                                }
                            }
                        }
                    }

                    lastFilePosition = fs.Position;

                    if (factionDetails.Count > 0)
                    {
                        OnFSDJumpDetected?.Invoke(factionDetails);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing journal: {ex.Message}");
            }
        }
    }
}

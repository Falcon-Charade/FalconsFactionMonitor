using FalconsFactionMonitor.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace FalconsFactionMonitor.Services
{
    internal class JournalMonitor
    {
        private readonly string journalDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "Saved Games",
            "Frontier Developments",
            "Elite Dangerous"
        );
        private string latestJournalFile;
        private long lastFilePosition = 0;
        private FileSystemWatcher watcher;

        public event Action<List<LiveData>> OnFSDJumpDetected;

        public void StartMonitoring()
        {
            // Initialize directory watcher to see creation/changes to *any* journal files
            watcher = new FileSystemWatcher(journalDirectory)
            {
                Filter = "Journal.????-??-??T*.log",
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite
            };

            // Wire up events:
            watcher.Changed += OnFileChanged;
            watcher.Created += OnFileCreated;
            watcher.EnableRaisingEvents = true;

            // Set the initial "latest journal" and read any existing lines
            latestJournalFile = GetLatestJournalFile();
            if (latestJournalFile != null)
            {
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.Write("\nMonitoring journal:");
                Console.ForegroundColor = ConsoleColor.White;
                Console.WriteLine($" {latestJournalFile}");
                ProcessNewJournalEntries();
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine("No journal file found initially.");
                Console.ForegroundColor = ConsoleColor.White;
            }
        }

        // Optional cleanup if you want to stop the watcher externally
        public void StopMonitoring()
        {
            watcher?.Dispose();
        }

        private string GetLatestJournalFile()
        {
            var journalFiles = Directory.GetFiles(journalDirectory, "Journal.????-??-??T*.log")
                                        .OrderByDescending(f => f)
                                        .ToList();

            return journalFiles.FirstOrDefault();
        }

        private void OnFileChanged(object sender, FileSystemEventArgs e)
        {
            // If the changed file is the file we are currently monitoring, process it
            if (e.FullPath == latestJournalFile)
            {
                ProcessNewJournalEntries();
            }
        }

        private void OnFileCreated(object sender, FileSystemEventArgs e)
        {
            // A new file was created. Check if it's 'newer' than the one we're watching
            // Typically, if Elite spawns a new file, that new file is the one we want to watch going forward.
            // But only switch once the old file is "fully processed" (i.e. we've read everything).

            string newFile = e.FullPath;
            // Compare the file names or creation times to see if it's indeed more recent
            // Generally the newest file has a larger suffix, but you can also compare timestamps:
            // e.g., if "newFile" is definitely newer than "latestJournalFile" then switch.

            if (IsFileNewer(newFile, latestJournalFile))
            {
                // First, finalize reading the old file (in case there's a last chunk)
                ProcessNewJournalEntries();

                // Switch to the new file
                latestJournalFile = newFile;
                lastFilePosition = 0; // reset
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.Write("Switching to new journal file:");
                Console.ForegroundColor = ConsoleColor.White;
                Console.WriteLine($" {latestJournalFile}");

                // Immediately process anything that's in the new file at creation
                ProcessNewJournalEntries();
            }
        }

        private bool IsFileNewer(string fileA, string fileB)
        {
            // A simple check can be comparing the file names directly:
            // "Journal.230120XXXX.01.log" etc. If the file name is lexically greater, it should be newer.
            // Or we can compare last write times.

            if (string.IsNullOrEmpty(fileB)) return true;  // If no old file, new one is definitely "newer".

            var fileACreation = File.GetLastWriteTime(fileA);
            var fileBCreation = File.GetLastWriteTime(fileB);

            return fileACreation > fileBCreation;
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

                    while ((line = reader.ReadLine()) != null)
                    {
                        var json = JObject.Parse(line);

                        if ((json["event"]?.ToString() == "FSDJump") || (json["event"]?.ToString() == "Location"))
                        {
                            string systemName = json["StarSystem"]?.ToString();
                            string security = json["SystemSecurity_Localised"]?.ToString() ?? "Unknown";
                            string economy = json["SystemEconomy_Localised"]?.ToString() ?? "Unknown";
                            var lastUpdated = ((DateTime)json["timestamp"]).ToString("yyyy-MM-dd HH:mm:ss");

                            if (json["event"]?.ToString() == "Location")
                            {
                                Console.ForegroundColor = ConsoleColor.Magenta;
                                Console.Write("\n[INFO]");
                                Console.ForegroundColor = ConsoleColor.White;
                                Console.WriteLine($" Game load detected! System: {systemName}, Security: {security}, Economy: {economy}, time: {lastUpdated}");
                            }
                            else
                            {
                                Console.ForegroundColor = ConsoleColor.Magenta;
                                Console.Write("\n[INFO]");
                                Console.ForegroundColor = ConsoleColor.White;
                                Console.WriteLine($" FSDJump detected! System: {systemName}, Security: {security}, Economy: {economy}, time: {lastUpdated}");
                            }

                            // We'll collect all faction details in a list and pass them via the event
                            var factionDetails = new List<LiveData>();

                            var factions = json["Factions"] as JArray;
                            if (factions != null)
                            {
                                foreach (var faction in factions)
                                {
                                    double influence = faction["Influence"]?.ToObject<double>() ?? 0.0;
                                    influence = Math.Round(influence * 100, 2);  // Convert from 0.x to xx.xx

                                    bool isPlayer = faction["SquadronFaction"]?.ToObject<bool>() ?? false;
                                    string state = faction["FactionState"]?.ToString();
                                    bool nativeFaction = faction["HomeSystem"]?.ToObject<bool>() ?? false;

                                    factionDetails.Add(new LiveData
                                    {
                                        SystemName = systemName,
                                        FactionName = faction["Name"]?.ToString(),
                                        InfluencePercent = influence,
                                        State = state,
                                        IsPlayer = isPlayer,
                                        NativeFaction = nativeFaction,
                                        LastUpdated = lastUpdated
                                    });
                                }
                                Console.ForegroundColor = ConsoleColor.Magenta;
                                Console.Write("\n[INFO]");
                                Console.ForegroundColor = ConsoleColor.White;
                                Console.WriteLine($" {factionDetails.Count} factions processed for {systemName}.");
                                //foreach (var faction in factionDetails)
                                //{
                                //    Console.WriteLine($"{faction.SystemName} - {faction.FactionName}: {faction.InfluencePercent}% influence");
                                //}

                                // Fire the event
                                OnFSDJumpDetected?.Invoke(factionDetails);
                            }
                        }
                    }

                    lastFilePosition = fs.Position;
                }
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.DarkRed;
                Console.Write($"\n[ERROR]");
                Console.ForegroundColor = ConsoleColor.White;
                Console.WriteLine(" Error processing journal: {ex.Message}");
            }
        }
    }
}

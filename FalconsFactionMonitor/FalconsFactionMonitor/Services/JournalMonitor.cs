using FalconsFactionMonitor.Helpers;
using FalconsFactionMonitor.Models;
using Microsoft.Win32.SafeHandles;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace FalconsFactionMonitor.Services
{
    internal class JournalMonitor
    {
        private string journalDirectory;

        public JournalMonitor()
        {
            string defaultPath = FolderInteractions.GetSavePath("Journal");

            journalDirectory = ResolveLinkTarget(defaultPath) ?? defaultPath;
        }

        // Constants for CreateFile flags
        private const int FILE_FLAG_BACKUP_SEMANTICS = 0x02000000;

        // Import necessary Windows API functions
        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern SafeFileHandle CreateFile(
            string lpFileName,                                          // The name of the file to open
            int dwDesiredAccess,                                        // Desired access to the file (0 means no access, just open)
            FileShare dwShareMode,                                      // Sharing mode (allow read/write and delete access)
            IntPtr lpSecurityAttributes,                                // Security attributes (null means default)
            FileMode dwCreationDisposition,                             // How to create the file (open existing)
            int dwFlagsAndAttributes,                                   // Flags and attributes (FILE_FLAG_BACKUP_SEMANTICS allows opening directories)
            IntPtr hTemplateFile);                                      // Template file handle (null means no template)

        // Import the GetFinalPathNameByHandle function to resolve the final path of a file or directory
        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern int GetFinalPathNameByHandle(
            IntPtr hFile,                                               // Handle to the file or directory
            [Out] StringBuilder lpszFilePath,                           // Output buffer for the final path
            int cchFilePath,                                            // Size of the output buffer
            int dwFlags);                                               // Flags for the function (0 means normal path format)

        private string ResolveLinkTarget(string path)
        {
            if (!Directory.Exists(path)) return null;

            try
            {
                // Use CreateFile with FILE_FLAG_BACKUP_SEMANTICS to open the directory
                var handle = CreateFile(                        // Use CreateFile to open the directory
                    path,
                    0,                                          // No access required, just need to open the directory
                    FileShare.ReadWrite | FileShare.Delete,     // Allow read/write and delete access
                    IntPtr.Zero,                                // No security attributes
                    FileMode.Open,                              // Open the directory
                    FILE_FLAG_BACKUP_SEMANTICS,                 // This flag allows opening directories
                    IntPtr.Zero);                               // No template file

                // CreateFile returns a SafeFileHandle, which we can use to get the final path
                if (handle.IsInvalid)
                    return null;

                // Use GetFinalPathNameByHandle to get the actual path
                var buffer = new StringBuilder(512);
                int result = GetFinalPathNameByHandle(
                    handle.DangerousGetHandle(),                // Get the handle from SafeFileHandle
                    buffer,                                     // Output buffer for the path
                    buffer.Capacity,                            // Size of the buffer
                    0);                                         // dwFlags, 0 means we want the path in normal format

                // Close the handle to release resources
                handle.Close();

                // Check if the result is valid
                if (result < 0 || buffer.Length == 0) return null;

                // Convert the StringBuilder to a string
                string rawPath = buffer.ToString();

                // Strip prefix like \\?\ if present
                const string prefix = @"\\?\";
                return rawPath.StartsWith(prefix) ? rawPath.Substring(prefix.Length) : rawPath;
            }
            catch
            {
                return null;
            }
        }

        private string latestJournalFile;
        private long lastFilePosition = 0;
        private FileSystemWatcher watcher;

        public event Action<List<LiveData>> OnFSDJumpDetected;

        public void StartMonitoring()
        {
            if (IsSymlink(journalDirectory))
            {
                Console.ForegroundColor = ConsoleColor.Magenta;
                Console.Write("\n[INFO] ");
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("Journal directory is a symbolic link or junction.");
                Console.ForegroundColor = ConsoleColor.White;
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Magenta;
                Console.Write("\n[INFO] ");
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("Using direct journal path.");
                Console.ForegroundColor = ConsoleColor.White;
            }
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
                using var fs = new FileStream(latestJournalFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var reader = new StreamReader(fs);
                {
                    fs.Seek(lastFilePosition, SeekOrigin.Begin);

                    string line;

                    while ((line = reader.ReadLine()) != null)
                    {
                        var json = JObject.Parse(line);

                        if (
                                (json["event"]?.ToString() == "FSDJump")
                             || (json["event"]?.ToString() == "Location")
                             || (json["event"]?.ToString() == "CarrierJump")
                           )
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
                            else if (json["event"]?.ToString() == "FSDJump")
                            {
                                Console.ForegroundColor = ConsoleColor.Magenta;
                                Console.Write("\n[INFO]");
                                Console.ForegroundColor = ConsoleColor.White;
                                Console.WriteLine($" FSDJump detected! System: {systemName}, Security: {security}, Economy: {economy}, time: {lastUpdated}");
                            }
                            else if (json["event"]?.ToString() == "CarrierJump")
                            {
                                Console.ForegroundColor = ConsoleColor.Magenta;
                                Console.Write("\n[INFO]");
                                Console.ForegroundColor = ConsoleColor.White;
                                Console.WriteLine($" Carrier Jump detected! System: {systemName}, Security: {security}, Economy: {economy}, time: {lastUpdated}");
                            }

                            // We'll collect all faction details in a list and pass them via the event
                            var factionDetails = new List<LiveData>();

                            if (json["Factions"] is JArray factions)
                            {
                                foreach (var faction in factions)
                                {
                                    decimal influence = faction["Influence"]?.ToObject<decimal>() ?? 0.0m;
                                    influence = Math.Round(influence * 100m, 2);  // Convert from 0.x to xx.xx

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
                Console.Write("\n[ERROR]");
                Console.ForegroundColor = ConsoleColor.White;
                Console.WriteLine($" Error processing journal: {ex.Message}");
            }
        }
        private bool IsSymlink(string path)
        {
            try
            {
                var attr = File.GetAttributes(path);
                return attr.HasFlag(FileAttributes.ReparsePoint);
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.DarkRed;
                Console.Write("\n[ERROR]");
                Console.ForegroundColor = ConsoleColor.White;
                Console.WriteLine($" Failed to check symlink status: {ex.Message}");
                return false;
            }
        }

    }
}

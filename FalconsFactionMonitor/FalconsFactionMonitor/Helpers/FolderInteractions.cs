using System;
using System.Configuration;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Forms;

namespace FalconsFactionMonitor.Helpers
{
    internal class FolderInteractions
    {
        internal static string Logic(string interactionType)
        {
            if (string.IsNullOrEmpty(interactionType) || 
                (interactionType != "CSV" && interactionType != "Journal"))
            {
                System.Windows.MessageBox.Show("Invalid interaction type specified.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return "Error";
            }
            var dialog = new FolderBrowserDialog()
            {
                Description = interactionType switch
                {
                    "CSV" => "Select Folder to Save CSV Files",
                    "Journal" => "Select Folder Where Journal Files are Saved",
                    _ => throw new InvalidOperationException("Invalid interaction type specified.")
                },
                SelectedPath = GetSavePath(interactionType)
            };
            string outcome;
            switch (interactionType)
            {
                case "CSV":
                    outcome = CSVFolder(dialog, interactionType);
                    return outcome;
                case "Journal":
                    outcome = JournalFolder(dialog, interactionType);
                    return outcome;
                default:
                    System.Windows.MessageBox.Show("Invalid interaction type specified.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return "Error";
            }
        }
        internal static string CSVFolder(FolderBrowserDialog dialog, string interactionType)
        {
            if (dialog.ShowDialog() == DialogResult.OK)
            {
                string newPath = dialog.SelectedPath;
                if (newPath.EndsWith("Output"))
                    newPath = newPath[..^7];
                SetSavePath(interactionType, newPath);
                System.Windows.MessageBox.Show("Save location updated successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                return "Changed";
            }
            else
            {
                System.Windows.MessageBox.Show("No folder selected. Save location remains unchanged.", "Information", MessageBoxButton.OK, MessageBoxImage.Information);
                return "Same";
            }
        }
        internal static string JournalFolder(FolderBrowserDialog dialog, string interactionType)
        {
            if (dialog.ShowDialog() == DialogResult.OK)
            {
                string newPath = dialog.SelectedPath;
                SetSavePath(interactionType, newPath);
                System.Windows.MessageBox.Show("Journal location updated successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                return "Changed";
            }
            else
            {
                System.Windows.MessageBox.Show("No folder selected. Journal location remains unchanged.", "Information", MessageBoxButton.OK, MessageBoxImage.Information);
                return "Same";
            }
        }
        internal static string GetSavePath(string interactionType)
        {
            string path;
            switch (interactionType)
            {
                case "CSV":
                    path = ConfigurationManager.AppSettings["CsvSavePath"];
                    return string.IsNullOrEmpty(path)
                        ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "YourManufacturer", "YourProduct")
                        : Environment.ExpandEnvironmentVariables(path);
                case "Journal":
                    path = ConfigurationManager.AppSettings["JournalSavePath"];
                    if (string.IsNullOrEmpty(path))
                    {
                        // If the path is not set in the config, use the default saved games folder
                        string savedGamesRoot = GetSavedGamesFolder();
                        string defaultPath = Path.Combine(savedGamesRoot, "Frontier Developments", "Elite Dangerous");
                        return Environment.ExpandEnvironmentVariables(defaultPath);
                    }
                    else
                    {
                        // If the path is set in the config, use it directly
                        return Environment.ExpandEnvironmentVariables(path);
                    }
                default:
                    System.Windows.MessageBox.Show("Invalid interaction type specified.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return string.Empty;
            };
        }
        internal static void SetSavePath(string interactionType, string newPath)
        {
            Configuration config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
            switch (interactionType)
            {
                case "CSV":
                    config.AppSettings.Settings["CsvSavePath"].Value = newPath;
                    break;
                case "Journal":
                    config.AppSettings.Settings["JournalSavePath"].Value = newPath;
                    break;
                default:
                    System.Windows.MessageBox.Show("Invalid interaction type specified.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
            }
            config.Save(ConfigurationSaveMode.Modified);
            ConfigurationManager.RefreshSection("appSettings");
        }

        // Constants for folder IDs
        private static readonly Guid FolderId_SavedGames = new("4C5C32FF-BB9D-43b0-B5B4-2D72E54EAAA4");

        // Import the SHGetKnownFolderPath function from shell32.dll

        //      !!!! This line is commented out due to the security risk of LibraryImport needing unsafe code !!!!
        //      LibraryImport replaces DllImport.
        //      If you want to use LibraryImport, you need to enable unsafe code in your project settings.
        //[LibraryImport("shell32.dll", StringMarshalling = StringMarshalling.Utf16, SetLastError = true)]
        //      !!!! This line is commented out due to the security risk of LibraryImport needing unsafe code !!!!

        [DllImport("shell32.dll", CharSet = CharSet.Unicode, SetLastError = true)]

        private static extern int SHGetKnownFolderPath(             // The function retrieves the path of a known folder
            [MarshalAs(UnmanagedType.LPStruct)] Guid rfid,          // The ID of the known folder to retrieve
            uint dwFlags,                                           // Flags for the function (usually 0)
            IntPtr hToken,                                          // Handle to the user token (usually IntPtr.Zero for current user)
            out IntPtr ppszPath);                                   // The output parameter that receives the path of the known folder

        public static string GetSavedGamesFolder()
        {
            int hr = SHGetKnownFolderPath(FolderId_SavedGames, 0, IntPtr.Zero, out nint outPathPtr);    // Call the function to get the path

            if (hr != 0 || outPathPtr == IntPtr.Zero)      //                  //                  // Check if the function call was successful and the pointer is not null
            {
                return null;           //        -         //        -         //        -         // If not successful, return null
            }

            string folderPath = Marshal.PtrToStringUni(outPathPtr);           //                  // Convert the pointer to a string
            Marshal.FreeCoTaskMem(outPathPtr);// -         //        -         //        -         // Free the memory allocated for the path string
            return folderPath;         //        -         //        -         //        -         // Return the folder path
        }
    }
}

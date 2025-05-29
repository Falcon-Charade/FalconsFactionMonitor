using Microsoft.Win32;

namespace FalconsFactionMonitor.Helpers
{
    public static class AppSettings
    {
        private const string RegistryRoot = @"Software\FalconCharade\FalconsFactionMonitor";

        public static string Get(string key, string defaultValue = "")
        {
            using (var keyHandle = Registry.CurrentUser.OpenSubKey(RegistryRoot))
            {
                return keyHandle?.GetValue(key)?.ToString() ?? defaultValue;
            }
        }

        public static void Set(string key, string value)
        {
            using (var keyHandle = Registry.CurrentUser.CreateSubKey(RegistryRoot))
            {
                keyHandle?.SetValue(key, value);
            }
        }
    }
}
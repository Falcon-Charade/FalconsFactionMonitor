using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FalconsFactionMonitor.Helpers
{
    internal class RegistryHelper
    {
        private const string RegistryPath = @"HKEY_CURRENT_USER\Software\FalconCharade\FalconsFactionMonitor";

        public static void Set(string key, string value)
        {
            Registry.SetValue(RegistryPath, key, value);
        }

        public static string Get(string key, string defaultValue = "")
        {
            return Registry.GetValue(RegistryPath, key, defaultValue)?.ToString();
        }
    }
}

using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FalconsFactionMonitor.Helpers
{
    internal class DatabaseConnectionBuilder
    {
        /// The registry path for the theme settings
        private const string RegistryPath = @"HKEY_CURRENT_USER\Software\FalconCharade\FalconsFactionMonitor";
        internal static string BuildConnectionString()
        {
            string userId = RegistryHelper.Get("UserId");//Registry.GetValue(RegistryPath, "UserId", null).ToString();
            string password = RegistryHelper.Get("Password");//Registry.GetValue(RegistryPath, "Password", null).ToString();
            if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(password))
            {
                throw new ArgumentException("Login not found, please provide login via options.");
            }
            return $"Server=falcons-sql.database.windows.net;Database=FalconsFactionMonitor;Persist Security Info=True;User Id={userId};Password={password};Encrypt=True;TrustServerCertificate=True;Connection Timeout=60;";
        }
    }
}

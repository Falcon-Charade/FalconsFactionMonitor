using System;

namespace FalconsFactionMonitor.Helpers
{
    internal class DatabaseConnectionBuilder
    {
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

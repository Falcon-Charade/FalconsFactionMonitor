using System;
using System.Data;
using System.Threading.Tasks;

namespace FalconsFactionMonitor.Helpers
{
    internal class DatabaseHelper
    {
        internal static string BuildConnectionString()
        {
            string userId = RegistryHelper.Get("UserId");//Registry.GetValue(RegistryPath, "UserId", null).ToString();
            string password = RegistryHelper.Get("Password");//Registry.GetValue(RegistryPath, "Password", null).ToString();
            if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(password))
            {
                throw new ArgumentException("Login not found, please provide login via options.");
            }
            return $"Server=falcons-sql.database.windows.net;Database=FalconsFactionMonitor;Persist Security Info=False;User Id={userId};Password={password};Encrypt=True;TrustServerCertificate=True;Connection Timeout=60;";
        }
    }
    public class ConnectionStringBuilder : IConnectionStringBuilder
    {
        public string Build()
        {
            // Original logic from DatabaseConnectionBuilder
            var sb = new System.Data.SqlClient.SqlConnectionStringBuilder
            {
                DataSource = "falcons-sql.database.windows.net",
                InitialCatalog = "FalconsFactionMonitor",
                PersistSecurityInfo = false,
                UserID = RegistryHelper.Get("UserId"), // Registry.GetValue(RegistryPath, "UserId", null).ToString(),
                Password = RegistryHelper.Get("Password"), // Registry.GetValue(RegistryPath, "Password", null).ToString(),
                Encrypt = true,
                TrustServerCertificate = true,
                ConnectTimeout = 60
            };
            return sb.ConnectionString;
        }
    }
    public class DatabaseExecutor : IDatabaseExecutor
    {
        public IDbConnection CreateConnection(string connectionString)
        {
            return new System.Data.SqlClient.SqlConnection(connectionString);
        }

        public async Task ExecuteNonQueryAsync(IDbConnection connection, IDbCommand command)
        {
            if (connection.State != ConnectionState.Open)
                connection.Open();

            command.Connection = connection;
            await Task.Run(() => command.ExecuteNonQuery());
        }
    }
}

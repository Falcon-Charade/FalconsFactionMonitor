using System.Data;
using System.IO;
using System.Threading.Tasks;


namespace FalconsFactionMonitor.Helpers
{
    public class FileReader : IFileReader
    {
        public string ReadAllText(string path) => File.ReadAllText(path);
    }
    public interface IFileReader
    {
        string ReadAllText(string path);
    }
    public interface IConnectionStringBuilder
    {
        string Build();
    }
    public interface IDatabaseExecutor
    {
        IDbConnection CreateConnection(string connectionString);
        Task ExecuteNonQueryAsync(IDbConnection connection, IDbCommand command);
    }
}
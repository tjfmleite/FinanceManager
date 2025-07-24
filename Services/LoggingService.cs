using System;
using System.IO;
using System.Threading.Tasks;

namespace FinanceManager.Services
{
    public static class LoggingService
    {
        private static readonly string LogPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "FinanceManager", "logs");

        static LoggingService()
        {
            if (!Directory.Exists(LogPath))
                Directory.CreateDirectory(LogPath);
        }

        public static void LogError(string message, Exception? exception = null)
        {
            var logMessage = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [ERROR] {message}";
            if (exception != null)
                logMessage += $"\nException: {exception}";
            WriteToFile(logMessage);
            System.Diagnostics.Debug.WriteLine(logMessage);
        }

        public static void LogInfo(string message)
        {
            var logMessage = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [INFO] {message}";
            WriteToFile(logMessage);
            System.Diagnostics.Debug.WriteLine(logMessage);
        }

        public static void LogWarning(string message)
        {
            var logMessage = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [WARNING] {message}";
            WriteToFile(logMessage);
            System.Diagnostics.Debug.WriteLine(logMessage);
        }

        // ADICIONADO: Método LogDebug que estava em falta
        public static void LogDebug(string message)
        {
            var logMessage = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [DEBUG] {message}";
            WriteToFile(logMessage);
            System.Diagnostics.Debug.WriteLine(logMessage);
        }

        private static void WriteToFile(string message)
        {
            try
            {
                var fileName = $"finance-{DateTime.Now:yyyy-MM-dd}.log";
                var filePath = Path.Combine(LogPath, fileName);
                File.AppendAllText(filePath, message + Environment.NewLine);
            }
            catch
            {
                // Falha silenciosa para evitar loops infinitos
            }
        }

        // ADICIONADO: Método CleanOldLogs que estava em falta
        public static void CleanOldLogs()
        {
            try
            {
                if (!Directory.Exists(LogPath)) return;

                var files = Directory.GetFiles(LogPath, "finance-*.log");
                var cutoffDate = DateTime.Now.AddDays(-30);

                foreach (var file in files)
                {
                    try
                    {
                        var fileInfo = new FileInfo(file);
                        if (fileInfo.CreationTime < cutoffDate)
                        {
                            File.Delete(file);
                        }
                    }
                    catch
                    {
                        // Se não conseguir eliminar, continua
                    }
                }
            }
            catch
            {
                // Se falhar a limpeza, não é crítico
            }
        }

        // ADICIONADO: Método GetLogStatistics que estava em falta
        public static LogStatistics GetLogStatistics()
        {
            try
            {
                if (!Directory.Exists(LogPath))
                    return new LogStatistics();

                var files = Directory.GetFiles(LogPath, "finance-*.log");
                long totalSize = 0;
                int totalFiles = files.Length;

                foreach (var file in files)
                {
                    totalSize += new FileInfo(file).Length;
                }

                return new LogStatistics
                {
                    TotalFiles = totalFiles,
                    TotalSizeBytes = totalSize,
                    TotalSizeMB = Math.Round(totalSize / (1024.0 * 1024.0), 2),
                    CurrentLogFile = $"finance-{DateTime.Now:yyyy-MM-dd}.log",
                    LogDirectory = LogPath
                };
            }
            catch
            {
                return new LogStatistics();
            }
        }

        // ADICIONADO: Método para obter caminho do ficheiro de log atual
        public static string GetCurrentLogFilePath()
        {
            var fileName = $"finance-{DateTime.Now:yyyy-MM-dd}.log";
            return Path.Combine(LogPath, fileName);
        }
    }

    // ADICIONADO: Classe LogStatistics que estava em falta
    public class LogStatistics
    {
        public int TotalFiles { get; set; }
        public long TotalSizeBytes { get; set; }
        public double TotalSizeMB { get; set; }
        public string CurrentLogFile { get; set; } = "";
        public string LogDirectory { get; set; } = "";
    }
}
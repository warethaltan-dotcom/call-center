using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CommunicationAssistantPro.Models;
using CommunicationAssistantPro.Services;

namespace CommunicationAssistantPro.Utilities
{
    /// <summary>
    /// Error logging service for application diagnostics
    /// </summary>
    public class ErrorLogService
    {
        private readonly string _logDirectory;
        private readonly string _logFilePath;
        private readonly object _lockObject = new object();

        public ErrorLogService()
        {
            // Set log directory in user's local application data
            _logDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "CommunicationAssistantPro",
                "Logs"
            );

            // Ensure log directory exists
            if (!Directory.Exists(_logDirectory))
            {
                Directory.CreateDirectory(_logDirectory);
            }

            // Set daily log file path
            var today = DateTime.Now.ToString("yyyyMMdd");
            _logFilePath = Path.Combine(_logDirectory, $"error_log_{today}.txt");
        }

        /// <summary>
        /// Log an error message
        /// </summary>
        /// <param name="exception">Exception object</param>
        /// <param name="context">Optional context information</param>
        /// <param name="userId">Optional user ID</param>
        public void LogError(Exception exception, string? context = null, int? userId = null)
        {
            try
            {
                var logEntry = CreateLogEntry(LogLevel.Error, exception.Message, context, userId, exception);
                WriteLogEntry(logEntry);
                
                // Also log to database for better tracking
                Task.Run(() => LogToDatabase(LogLevel.Error, exception.Message, context, userId, exception));
            }
            catch (Exception ex)
            {
                // Fallback logging to console if file logging fails
                System.Diagnostics.Debug.WriteLine($"خطأ في تسجيل الخطأ: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"الخطأ الأصلي: {exception.Message}");
            }
        }

        /// <summary>
        /// Log a warning message
        /// </summary>
        public void LogWarning(string message, string? context = null, int? userId = null)
        {
            try
            {
                var logEntry = CreateLogEntry(LogLevel.Warning, message, context, userId);
                WriteLogEntry(logEntry);
                
                Task.Run(() => LogToDatabase(LogLevel.Warning, message, context, userId));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"خطأ في تسجيل التحذير: {ex.Message}");
            }
        }

        /// <summary>
        /// Log an info message
        /// </summary>
        public void LogInfo(string message, string? context = null, int? userId = null)
        {
            try
            {
                var logEntry = CreateLogEntry(LogLevel.Info, message, context, userId);
                WriteLogEntry(logEntry);
                
                Task.Run(() => LogToDatabase(LogLevel.Info, message, context, userId));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"خطأ في تسجيل المعلومات: {ex.Message}");
            }
        }

        /// <summary>
        /// Log a debug message
        /// </summary>
        public void LogDebug(string message, string? context = null, int? userId = null)
        {
            try
            {
                var logEntry = CreateLogEntry(LogLevel.Debug, message, context, userId);
                WriteLogEntry(logEntry);
                
                Task.Run(() => LogToDatabase(LogLevel.Debug, message, context, userId));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"خطأ في تسجيل التصحيح: {ex.Message}");
            }
        }

        /// <summary>
        /// Log a critical error
        /// </summary>
        public void LogCritical(string message, string? context = null, int? userId = null, Exception? exception = null)
        {
            try
            {
                var logEntry = CreateLogEntry(LogLevel.Critical, message, context, userId, exception);
                WriteLogEntry(logEntry);
                
                Task.Run(() => LogToDatabase(LogLevel.Critical, message, context, userId, exception));
                
                // For critical errors, also try to send notification or alert
                NotifyCriticalError(message, context);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"خطأ في تسجيل الخطأ الحرج: {ex.Message}");
            }
        }

        /// <summary>
        /// Create log entry
        /// </summary>
        private string CreateLogEntry(LogLevel level, string message, string? context, int? userId, Exception? exception = null)
        {
            var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            var logLevel = level.ToString().ToUpper();
            var threadId = System.Threading.Thread.CurrentThread.ManagedThreadId;
            var processId = System.Diagnostics.Process.GetCurrentProcess().Id;
            
            var builder = new StringBuilder();
            builder.AppendLine($"[{timestamp}] [{logLevel}] [Thread:{threadId}] [Process:{processId}]");

            if (userId.HasValue)
            {
                builder.AppendLine($"[UserId: {userId.Value}]");
            }

            if (!string.IsNullOrEmpty(context))
            {
                builder.AppendLine($"[Context: {context}]");
            }

            builder.AppendLine($"Message: {message}");

            if (exception != null)
            {
                builder.AppendLine($"Exception: {exception.GetType().Name}");
                builder.AppendLine($"Exception Message: {exception.Message}");
                builder.AppendLine($"Stack Trace: {exception.StackTrace}");

                if (exception.InnerException != null)
                {
                    builder.AppendLine($"Inner Exception: {exception.InnerException.Message}");
                    builder.AppendLine($"Inner Stack Trace: {exception.InnerException.StackTrace}");
                }
            }

            builder.AppendLine(new string('-', 80));
            builder.AppendLine();

            return builder.ToString();
        }

        /// <summary>
        /// Write log entry to file
        /// </summary>
        private void WriteLogEntry(string logEntry)
        {
            lock (_lockObject)
            {
                try
                {
                    // Ensure current log file exists and get current date
                    var currentLogFile = GetCurrentLogFilePath();
                    
                    // If date changed, update log file path
                    if (_logFilePath != currentLogFile)
                    {
                        // Clean up old log files based on retention policy
                        CleanupOldLogs();
                    }
                    
                    File.AppendAllText(_logFilePath, logEntry);
                }
                catch (Exception ex)
                {
                    // Fallback to console if file writing fails
                    System.Diagnostics.Debug.WriteLine($"خطأ في كتابة ملف السجل: {ex.Message}");
                    System.Diagnostics.Debug.WriteLine(logEntry);
                }
            }
        }

        /// <summary>
        /// Get current log file path based on today's date
        /// </summary>
        private string GetCurrentLogFilePath()
        {
            var today = DateTime.Now.ToString("yyyyMMdd");
            return Path.Combine(_logDirectory, $"error_log_{today}.txt");
        }

        /// <summary>
        /// Clean up old log files based on retention policy
        /// </summary>
        private void CleanupOldLogs()
        {
            try
            {
                var settingsService = new SettingsService();
                var settings = settingsService.GetSettings();
                var retentionDays = settings.LogRetentionDays;

                var cutoffDate = DateTime.Now.AddDays(-retentionDays);
                var logFiles = Directory.GetFiles(_logDirectory, "error_log_*.txt");

                foreach (var logFile in logFiles)
                {
                    try
                    {
                        var fileInfo = new FileInfo(logFile);
                        if (fileInfo.LastWriteTime < cutoffDate)
                        {
                            File.Delete(logFile);
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"خطأ في حذف ملف السجل القديم: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"خطأ في تنظيف ملفات السجل القديمة: {ex.Message}");
            }
        }

        /// <summary>
        /// Log to database for better tracking
        /// </summary>
        private void LogToDatabase(LogLevel level, string message, string? context, int? userId, Exception? exception = null)
        {
            try
            {
                using var databaseService = new DatabaseService();
                var logDescription = context ?? message;
                if (exception != null)
                {
                    logDescription = $"{message} - Exception: {exception.GetType().Name}";
                }
                
                databaseService.LogActivity(userId, $"{level} Log", 
                    level == LogLevel.Error || level == LogLevel.Critical ? ActivityType.Error : ActivityType.System, 
                    logDescription);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"خطأ في تسجيل السجل في قاعدة البيانات: {ex.Message}");
            }
        }

        /// <summary>
        /// Notify about critical errors
        /// </summary>
        private void NotifyCriticalError(string message, string? context)
        {
            try
            {
                // This could be extended to send emails, SMS, or other notifications
                // For now, we'll just log to the event log or create a system notification
                
                var fullMessage = $"خطأ حرج في نظام الاتصالات: {message}";
                if (!string.IsNullOrEmpty(context))
                {
                    fullMessage += $"\nالسياق: {context}";
                }

                // Log to Windows Event Log if available
                try
                {
                    using var eventLog = new System.Diagnostics.EventLog("Application", Environment.MachineName, "CommunicationAssistantPro");
                    eventLog.Source = "CommunicationAssistantPro";
                    eventLog.WriteEntry(fullMessage, System.Diagnostics.EventLogEntryType.Error);
                }
                catch
                {
                    // Ignore if event log is not accessible
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"خطأ في إرسال إشعار الخطأ الحرج: {ex.Message}");
            }
        }

        /// <summary>
        /// Get log entries for a specific period
        /// </summary>
        public string[] GetLogEntries(DateTime startDate, DateTime endDate, LogLevel? level = null)
        {
            try
            {
                var logs = new System.Collections.Generic.List<string>();
                
                // Get all log files for the period
                var logFiles = Directory.GetFiles(_logDirectory, "error_log_*.txt");
                
                foreach (var logFile in logFiles)
                {
                    try
                    {
                        var fileInfo = new FileInfo(logFile);
                        if (fileInfo.LastWriteTime >= startDate.Date && fileInfo.LastWriteTime <= endDate.Date)
                        {
                            var content = File.ReadAllText(logFile);
                            
                            if (level.HasValue)
                            {
                                // Filter by log level
                                var levelString = level.Value.ToString().ToUpper();
                                var lines = content.Split('\n');
                                var filteredLines = new System.Collections.Generic.List<string>();
                                
                                foreach (var line in lines)
                                {
                                    if (line.Contains($"[{levelString}]") || string.IsNullOrWhiteSpace(line) || line.StartsWith("---"))
                                    {
                                        filteredLines.Add(line);
                                    }
                                }
                                
                                content = string.Join("\n", filteredLines);
                            }
                            
                            logs.Add(content);
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"خطأ في قراءة ملف السجل: {ex.Message}");
                    }
                }
                
                return logs.ToArray();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"خطأ في الحصول على سجلات الفترة الزمنية: {ex.Message}");
                return new string[0];
            }
        }

        /// <summary>
        /// Get log statistics
        /// </summary>
        public Dictionary<string, int> GetLogStatistics(DateTime startDate, DateTime endDate)
        {
            var statistics = new Dictionary<string, int>
            {
                [LogLevel.Error.ToString()] = 0,
                [LogLevel.Warning.ToString()] = 0,
                [LogLevel.Info.ToString()] = 0,
                [LogLevel.Debug.ToString()] = 0,
                [LogLevel.Critical.ToString()] = 0,
                [LogLevel.Trace.ToString()] = 0
            };

            try
            {
                var logEntries = GetLogEntries(startDate, endDate);
                var allContent = string.Join("\n", logEntries);

                foreach (var level in statistics.Keys.ToArray())
                {
                    var count = CountOccurrences(allContent, $"[{level}]");
                    statistics[level] = count;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"خطأ في حساب إحصائيات السجل: {ex.Message}");
            }

            return statistics;
        }

        /// <summary>
        /// Count occurrences of a pattern in text
        /// </summary>
        private int CountOccurrences(string text, string pattern)
        {
            int count = 0;
            int index = 0;
            
            while ((index = text.IndexOf(pattern, index, StringComparison.Ordinal)) != -1)
            {
                count++;
                index += pattern.Length;
            }
            
            return count;
        }

        /// <summary>
        /// Export logs to a file
        /// </summary>
        public string ExportLogs(DateTime startDate, DateTime endDate, LogLevel? level = null, string? outputPath = null)
        {
            try
            {
                if (string.IsNullOrEmpty(outputPath))
                {
                    var exportDirectory = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        "CommunicationAssistantPro",
                        "Exports"
                    );
                    
                    if (!Directory.Exists(exportDirectory))
                    {
                        Directory.CreateDirectory(exportDirectory);
                    }
                    
                    var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                    outputPath = Path.Combine(exportDirectory, $"logs_export_{timestamp}.txt");
                }

                var logEntries = GetLogEntries(startDate, endDate, level);
                var content = string.Join("\n", logEntries);
                
                File.WriteAllText(outputPath, content);
                return outputPath;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"خطأ في تصدير السجلات: {ex.Message}", ex);
            }
        }
    }
}
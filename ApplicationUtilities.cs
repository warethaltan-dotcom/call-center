using System;
using System.Linq;
using System.Windows;

namespace CommunicationAssistantPro.Utilities
{
    /// <summary>
    /// Window utility methods for common window operations
    /// </summary>
    public static class WindowUtilities
    {
        /// <summary>
        /// Center a window on the screen
        /// </summary>
        public static void CenterOnScreen(Window window)
        {
            window.Left = (SystemParameters.PrimaryScreenWidth - window.Width) / 2;
            window.Top = (SystemParameters.PrimaryScreenHeight - window.Height) / 2;
        }

        /// <summary>
        /// Center a window on its owner
        /// </summary>
        public static void CenterOnOwner(Window window)
        {
            if (window.Owner != null)
            {
                window.Left = window.Owner.Left + (window.Owner.Width - window.Width) / 2;
                window.Top = window.Owner.Top + (window.Owner.Height - window.Height) / 2;
            }
            else
            {
                CenterOnScreen(window);
            }
        }

        /// <summary>
        /// Minimize window to system tray
        /// </summary>
        public static void MinimizeToTray(Window window)
        {
            window.ShowInTaskbar = false;
            window.Visibility = Visibility.Hidden;
        }

        /// <summary>
        /// Restore window from system tray
        /// </summary>
        public static void RestoreFromTray(Window window)
        {
            window.ShowInTaskbar = true;
            window.Visibility = Visibility.Visible;
            window.Activate();
        }

        /// <summary>
        /// Make window always on top
        /// </summary>
        public static void SetAlwaysOnTop(Window window, bool alwaysOnTop)
        {
            window.Topmost = alwaysOnTop;
        }

        /// <summary>
        /// Fade in a window with animation
        /// </summary>
        public static void FadeIn(Window window, int duration = 300)
        {
            window.Opacity = 0;
            window.Visibility = Visibility.Visible;

            var animation = new System.Windows.Media.Animation.DoubleAnimation
            {
                From = 0,
                To = 1,
                Duration = TimeSpan.FromMilliseconds(duration)
            };

            window.BeginAnimation(Window.OpacityProperty, animation);
        }

        /// <summary>
        /// Fade out a window with animation
        /// </summary>
        public static void FadeOut(Window window, int duration = 300)
        {
            var animation = new System.Windows.Media.Animation.DoubleAnimation
            {
                From = 1,
                To = 0,
                Duration = TimeSpan.FromMilliseconds(duration)
            };

            animation.Completed += (s, e) => window.Visibility = Visibility.Hidden;
            window.BeginAnimation(Window.OpacityProperty, animation);
        }
    }

    /// <summary>
    /// Network utilities for connection testing and monitoring
    /// </summary>
    public static class NetworkUtilities
    {
        /// <summary>
        /// Check if a port is open
        /// </summary>
        public static bool IsPortOpen(string host, int port, int timeout = 5000)
        {
            try
            {
                using var client = new System.Net.Sockets.TcpClient();
                var connectTask = client.ConnectAsync(host, port);
                
                var completedTask = System.Threading.Tasks.Task.WhenAny(
                    connectTask, 
                    System.Threading.Tasks.Task.Delay(timeout)
                );

                return completedTask == connectTask && client.Connected;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Get local IP addresses
        /// </summary>
        public static string[] GetLocalIPAddresses()
        {
            var addresses = System.Net.Dns.GetHostAddresses(System.Net.Dns.GetHostName())
                .Where(ip => ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                .Select(ip => ip.ToString())
                .ToArray();

            return addresses;
        }

        /// <summary>
        /// Ping a host
        /// </summary>
        public static bool PingHost(string host, int timeout = 5000)
        {
            try
            {
                using var ping = new System.Net.NetworkInformation.Ping();
                var reply = ping.Send(host, timeout);
                return reply.Status == System.Net.NetworkInformation.IPStatus.Success;
            }
            catch
            {
                return false;
            }
        }
    }

    /// <summary>
    /// File system utilities for application data management
    /// </summary>
    public static class FileSystemUtilities
    {
        /// <summary>
        /// Ensure directory exists
        /// </summary>
        public static void EnsureDirectory(string path)
        {
            if (!string.IsNullOrEmpty(path) && !System.IO.Directory.Exists(path))
            {
                System.IO.Directory.CreateDirectory(path);
            }
        }

        /// <summary>
        /// Get application data directory
        /// </summary>
        public static string GetAppDataDirectory(string subfolder = "")
        {
            var basePath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var appPath = System.IO.Path.Combine(basePath, "CommunicationAssistantPro");
            
            if (!string.IsNullOrEmpty(subfolder))
            {
                appPath = System.IO.Path.Combine(appPath, subfolder);
            }

            EnsureDirectory(appPath);
            return appPath;
        }

        /// <summary>
        /// Get temp directory
        /// </summary>
        public static string GetTempDirectory()
        {
            var tempPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "CommunicationAssistantPro");
            EnsureDirectory(tempPath);
            return tempPath;
        }

        /// <summary>
        /// Clean old files from directory
        /// </summary>
        public static void CleanOldFiles(string directory, int retentionDays)
        {
            try
            {
                if (!System.IO.Directory.Exists(directory)) return;

                var cutoffDate = DateTime.Now.AddDays(-retentionDays);
                var files = System.IO.Directory.GetFiles(directory);

                foreach (var file in files)
                {
                    try
                    {
                        var fileInfo = new System.IO.FileInfo(file);
                        if (fileInfo.LastWriteTime < cutoffDate)
                        {
                            System.IO.File.Delete(file);
                        }
                    }
                    catch
                    {
                        // Ignore errors for individual files
                    }
                }
            }
            catch
            {
                // Ignore errors in cleanup
            }
        }

        /// <summary>
        /// Get file size in human readable format
        /// </summary>
        public static string GetFileSize(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            double len = bytes;
            int order = 0;
            
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }

            return $"{len:0.##} {sizes[order]}";
        }
    }

    /// <summary>
    /// Validation utilities for input validation
    /// </summary>
    public static class ValidationUtilities
    {
        /// <summary>
        /// Validate IP address
        /// </summary>
        public static bool IsValidIPAddress(string ipAddress)
        {
            if (string.IsNullOrWhiteSpace(ipAddress))
                return false;

            var parts = ipAddress.Split('.');
            if (parts.Length != 4)
                return false;

            foreach (var part in parts)
            {
                if (!int.TryParse(part, out var number))
                    return false;

                if (number < 0 || number > 255)
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Validate port number
        /// </summary>
        public static bool IsValidPort(int port)
        {
            return port > 0 && port <= 65535;
        }

        /// <summary>
        /// Validate email address
        /// </summary>
        public static bool IsValidEmail(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
                return false;

            try
            {
                var addr = new System.Net.Mail.MailAddress(email);
                return addr.Address == email;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Validate username
        /// </summary>
        public static bool IsValidUsername(string username)
        {
            if (string.IsNullOrWhiteSpace(username))
                return false;

            // Username should be 3-50 characters, alphanumeric and underscores only
            return username.Length >= 3 && 
                   username.Length <= 50 && 
                   System.Text.RegularExpressions.Regex.IsMatch(username, @"^[a-zA-Z0-9_]+$");
        }

        /// <summary>
        /// Validate extension number
        /// </summary>
        public static bool IsValidExtension(string extension)
        {
            if (string.IsNullOrWhiteSpace(extension))
                return true; // Extension is optional

            // Extension should be 3-10 digits
            return extension.Length >= 3 && 
                   extension.Length <= 10 && 
                   System.Text.RegularExpressions.Regex.IsMatch(extension, @"^\d+$");
        }

        /// <summary>
        /// Validate file path
        /// </summary>
        public static bool IsValidFilePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return false;

            try
            {
                // Check if path contains invalid characters
                var invalidChars = System.IO.Path.GetInvalidPathChars();
                if (path.IndexOfAny(invalidChars) >= 0)
                    return false;

                // Check if directory can be created
                var directory = System.IO.Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory))
                {
                    System.IO.Directory.CreateDirectory(directory);
                }

                return true;
            }
            catch
            {
                return false;
            }
        }
    }

    /// <summary>
    /// Date and time utilities for formatting and calculations
    /// </summary>
    public static class DateTimeUtilities
    {
        /// <summary>
        /// Format duration in human readable format
        /// </summary>
        public static string FormatDuration(TimeSpan duration)
        {
            if (duration.TotalDays >= 1)
            {
                return $"{duration.Days} يوم {duration.Hours} ساعة {duration.Minutes} دقيقة";
            }
            else if (duration.TotalHours >= 1)
            {
                return $"{duration.Hours} ساعة {duration.Minutes} دقيقة {duration.Seconds} ثانية";
            }
            else if (duration.TotalMinutes >= 1)
            {
                return $"{duration.Minutes} دقيقة {duration.Seconds} ثانية";
            }
            else
            {
                return $"{duration.Seconds} ثانية";
            }
        }

        /// <summary>
        /// Get start of day
        /// </summary>
        public static DateTime GetStartOfDay(DateTime date)
        {
            return date.Date;
        }

        /// <summary>
        /// Get end of day
        /// </summary>
        public static DateTime GetEndOfDay(DateTime date)
        {
            return date.Date.AddDays(1).AddTicks(-1);
        }

        /// <summary>
        /// Get start of week (Sunday)
        /// </summary>
        public static DateTime GetStartOfWeek(DateTime date)
        {
            var diff = date.DayOfWeek - DayOfWeek.Sunday;
            return date.AddDays(-diff).Date;
        }

        /// <summary>
        /// Get end of week (Saturday)
        /// </summary>
        public static DateTime GetEndOfWeek(DateTime date)
        {
            return GetStartOfWeek(date).AddDays(6).AddTicks(-1);
        }

        /// <summary>
        /// Get start of month
        /// </summary>
        public static DateTime GetStartOfMonth(DateTime date)
        {
            return new DateTime(date.Year, date.Month, 1);
        }

        /// <summary>
        /// Get end of month
        /// </summary>
        public static DateTime GetEndOfMonth(DateTime date)
        {
            return GetStartOfMonth(date).AddMonths(1).AddTicks(-1);
        }

        /// <summary>
        /// Get relative time string (e.g., "2 hours ago", "in 3 days")
        /// </summary>
        public static string GetRelativeTime(DateTime dateTime)
        {
            var timeSpan = DateTime.Now - dateTime;

            if (timeSpan.TotalDays >= 365)
            {
                var years = (int)(timeSpan.TotalDays / 365);
                return $"منذ {years} سنة";
            }
            else if (timeSpan.TotalDays >= 30)
            {
                var months = (int)(timeSpan.TotalDays / 30);
                return $"منذ {months} شهر";
            }
            else if (timeSpan.TotalDays >= 1)
            {
                var days = (int)timeSpan.TotalDays;
                return $"منذ {days} يوم";
            }
            else if (timeSpan.TotalHours >= 1)
            {
                var hours = (int)timeSpan.TotalHours;
                return $"منذ {hours} ساعة";
            }
            else if (timeSpan.TotalMinutes >= 1)
            {
                var minutes = (int)timeSpan.TotalMinutes;
                return $"منذ {minutes} دقيقة";
            }
            else
            {
                return "الآن";
            }
        }
    }
}
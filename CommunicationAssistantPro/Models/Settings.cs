using System;
using System.ComponentModel.DataAnnotations;

namespace CommunicationAssistantPro.Models
{
    /// <summary>
    /// Application settings configuration
    /// </summary>
    public class AppSettings
    {
        [Key]
        public int Id { get; set; } = 1; // Single instance

        // PBX Configuration
        [StringLength(50)]
        public string PBX_IP { get; set; } = "192.168.1.100";

        public int PBX_Port { get; set; } = 5038;

        [StringLength(50)]
        public string PBX_Username { get; set; } = "admin";

        [StringLength(100)]
        public string PBX_Password { get; set; } = string.Empty;

        public bool PBX_UseAMI { get; set; } = true; // true for AMI, false for HTTP

        // Call Status File Configuration
        public bool EnableCallStatusFile { get; set; } = true;

        [StringLength(500)]
        public string CallStatusFilePath { get; set; } = @"C:\Temp\CaCallstatus.dat";

        public int CallStatusFileTimeout { get; set; } = 3; // seconds

        // Database Configuration
        [StringLength(255)]
        public string DatabasePath { get; set; } = string.Empty;

        public int DatabaseBackupInterval { get; set; } = 24; // hours

        // Application Settings
        public bool AutoLogin { get; set; } = false;

        public bool RememberMe { get; set; } = false;

        [StringLength(20)]
        public string Language { get; set; } = "ar"; // ar, en

        public bool RTLSupport { get; set; } = true;

        public bool EnableSounds { get; set; } = true;

        public bool EnableNotifications { get; set; } = true;

        public int NotificationTimeout { get; set; } = 5; // seconds

        // Security Settings
        public bool EnableEncryption { get; set; } = true;

        public int SessionTimeout { get; set; } = 480; // minutes (8 hours)

        public bool RequirePasswordForLogout { get; set; } = false;

        public int MaxFailedLoginAttempts { get; set; } = 3;

        public int LockoutDuration { get; set; } = 15; // minutes

        // Logging Settings
        public bool EnableLogging { get; set; } = true;

        public LogLevel MinimumLogLevel { get; set; } = LogLevel.Info;

        public int LogRetentionDays { get; set; } = 30;

        public bool EnableCallRecording { get; set; } = false;

        // UI Settings
        public bool ShowSystemTray { get; set; } = true;

        public bool MinimizeToTray { get; set; } = true;

        public bool StartMinimized { get; set; } = false;

        public bool EnableDarkTheme { get; set; } = false;

        public int AutoRefreshInterval { get; set; } = 5; // seconds

        // Report Settings
        public bool AutoGenerateReports { get; set; } = true;

        public bool EmailReports { get; set; } = false;

        [StringLength(255)]
        public string ReportEmailAddress { get; set; } = string.Empty;

        public ReportPeriod DefaultReportPeriod { get; set; } = ReportPeriod.Daily;

        public DateTime LastSettingsUpdate { get; set; } = DateTime.Now;

        public int SettingsVersion { get; set; } = 1;
    }

    /// <summary>
    /// Log levels for application logging
    /// </summary>
    public enum LogLevel
    {
        Trace = 1,
        Debug = 2,
        Info = 3,
        Warning = 4,
        Error = 5,
        Critical = 6
    }

    /// <summary>
    /// Report periods for scheduling
    /// </summary>
    public enum ReportPeriod
    {
        Hourly = 1,
        Daily = 2,
        Weekly = 3,
        Monthly = 4
    }

    /// <summary>
    /// System configuration backup
    /// </summary>
    public class SettingsBackup
    {
        [Key]
        public int Id { get; set; }

        [StringLength(100)]
        public string Name { get; set; } = string.Empty;

        [StringLength(255)]
        public string BackupPath { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public string? EncryptedSettings { get; set; }

        public int SettingsVersion { get; set; }
    }

    /// <summary>
    /// Network configuration for PBX connection
    /// </summary>
    public class NetworkConfig
    {
        public string IPAddress { get; set; } = string.Empty;
        public int Port { get; set; }
        public int Timeout { get; set; } = 30;
        public bool UseSSL { get; set; } = false;
        public int RetryAttempts { get; set; } = 3;
        public int RetryDelay { get; set; } = 5000; // milliseconds
    }

    /// <summary>
    /// User preferences for personalization
    /// </summary>
    public class UserPreferences
    {
        [Key]
        public int Id { get; set; }

        public int UserId { get; set; }

        public bool ShowNotificationPopup { get; set; } = true;

        public bool PlayNotificationSound { get; set; } = true;

        public int AutoRefreshRate { get; set; } = 5;

        public bool ShowCallDuration { get; set; } = true;

        public bool ShowCallerInfo { get; set; } = true;

        [StringLength(100)]
        public string PreferredTheme { get; set; } = "Light";

        public bool EnableAutoAnswer { get; set; } = false;

        public string? CustomNotificationMessage { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public DateTime UpdatedAt { get; set; } = DateTime.Now;

        // Navigation property
        public User User { get; set; } = null!;
    }
}
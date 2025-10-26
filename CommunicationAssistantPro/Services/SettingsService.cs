using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using CommunicationAssistantPro.Models;

namespace CommunicationAssistantPro.Services
{
    /// <summary>
    /// Settings service for managing application configuration
    /// </summary>
    public class SettingsService
    {
        private readonly DatabaseService _databaseService;
        private readonly string _settingsFilePath;
        private AppSettings? _cachedSettings;

        public SettingsService()
        {
            _databaseService = new DatabaseService();
            
            // Set settings file path in user's local application data
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var settingsDirectory = Path.Combine(appDataPath, "CommunicationAssistantPro", "Config");
            
            if (!Directory.Exists(settingsDirectory))
            {
                Directory.CreateDirectory(settingsDirectory);
            }

            _settingsFilePath = Path.Combine(settingsDirectory, "appsettings.json");
        }

        /// <summary>
        /// Load settings from database and cache them
        /// </summary>
        public AppSettings GetSettings()
        {
            try
            {
                if (_cachedSettings == null)
                {
                    LoadSettings();
                }
                return _cachedSettings!;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"خطأ في تحميل الإعدادات: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Load settings from database
        /// </summary>
        public void LoadSettings()
        {
            try
            {
                // First try to load from database
                _cachedSettings = _databaseService.GetSettings();

                // If database settings are not available, try to load from JSON file
                if (_cachedSettings == null && File.Exists(_settingsFilePath))
                {
                    var json = File.ReadAllText(_settingsFilePath);
                    _cachedSettings = JsonSerializer.Deserialize<AppSettings>(json);
                }

                // If still no settings, create default settings
                if (_cachedSettings == null)
                {
                    _cachedSettings = CreateDefaultSettings();
                    _databaseService.UpdateSettings(_cachedSettings);
                }
            }
            catch (Exception ex)
            {
                // Fallback to default settings on error
                _cachedSettings = CreateDefaultSettings();
            }
        }

        /// <summary>
        /// Update settings and save to database
        /// </summary>
        public void UpdateSettings(AppSettings settings)
        {
            try
            {
                // Validate settings
                ValidateSettings(settings);

                // Update in database
                _databaseService.UpdateSettings(settings);

                // Update cache
                _cachedSettings = settings;

                // Save backup to JSON file
                SaveSettingsBackup(settings);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"خطأ في تحديث الإعدادات: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Update specific setting values
        /// </summary>
        public void UpdateSetting(string propertyName, object value)
        {
            try
            {
                var settings = GetSettings();
                var property = typeof(AppSettings).GetProperty(propertyName);
                
                if (property != null && property.CanWrite)
                {
                    property.SetValue(settings, value);
                    UpdateSettings(settings);
                }
                else
                {
                    throw new ArgumentException($"خاصية غير صحيحة: {propertyName}");
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"خطأ في تحديث الخاصية {propertyName}: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Validate settings values
        /// </summary>
        private void ValidateSettings(AppSettings settings)
        {
            // Validate PBX settings
            if (string.IsNullOrWhiteSpace(settings.PBX_IP))
                throw new ArgumentException("عنوان IP للـ PBX مطلوب");

            if (settings.PBX_Port < 1 || settings.PBX_Port > 65535)
                throw new ArgumentException("رقم المنفذ للـ PBX يجب أن يكون بين 1 و 65535");

            if (string.IsNullOrWhiteSpace(settings.PBX_Username))
                throw new ArgumentException("اسم المستخدم للـ PBX مطلوب");

            // Validate file paths
            if (settings.EnableCallStatusFile)
            {
                if (string.IsNullOrWhiteSpace(settings.CallStatusFilePath))
                    throw new ArgumentException("مسار ملف حالة المكالمات مطلوب");

                try
                {
                    var directory = Path.GetDirectoryName(settings.CallStatusFilePath);
                    if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                    {
                        Directory.CreateDirectory(directory);
                    }
                }
                catch (Exception ex)
                {
                    throw new ArgumentException($"مسار ملف حالة المكالمات غير صحيح: {ex.Message}");
                }
            }

            // Validate numeric values
            if (settings.CallStatusFileTimeout < 1 || settings.CallStatusFileTimeout > 300)
                throw new ArgumentException("مهلة حذف ملف حالة المكالمات يجب أن تكون بين 1 و 300 ثانية");

            if (settings.DatabaseBackupInterval < 1 || settings.DatabaseBackupInterval > 168)
                throw new ArgumentException("فترة النسخ الاحتياطي يجب أن تكون بين 1 و 168 ساعة");

            if (settings.SessionTimeout < 5 || settings.SessionTimeout > 1440)
                throw new ArgumentException("مهلة الجلسة يجب أن تكون بين 5 و 1440 دقيقة");

            if (settings.MaxFailedLoginAttempts < 1 || settings.MaxFailedLoginAttempts > 10)
                throw new ArgumentException("عدد محاولات تسجيل الدخول المسموحة يجب أن يكون بين 1 و 10");

            // Validate database path
            if (string.IsNullOrWhiteSpace(settings.DatabasePath))
            {
                settings.DatabasePath = GetDefaultDatabasePath();
            }
        }

        /// <summary>
        /// Get default database path
        /// </summary>
        private string GetDefaultDatabasePath()
        {
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var databaseDirectory = Path.Combine(appDataPath, "CommunicationAssistantPro", "Database");
            
            if (!Directory.Exists(databaseDirectory))
            {
                Directory.CreateDirectory(databaseDirectory);
            }

            return Path.Combine(databaseDirectory, "CommunicationAssistant.db");
        }

        /// <summary>
        /// Create default settings
        /// </summary>
        private AppSettings CreateDefaultSettings()
        {
            return new AppSettings
            {
                PBX_IP = "192.168.1.100",
                PBX_Port = 5038,
                PBX_Username = "admin",
                EnableCallStatusFile = true,
                CallStatusFilePath = @"C:\Temp\CaCallstatus.dat",
                CallStatusFileTimeout = 3,
                DatabasePath = GetDefaultDatabasePath(),
                DatabaseBackupInterval = 24,
                Language = "ar",
                RTLSupport = true,
                EnableSounds = true,
                EnableNotifications = true,
                NotificationTimeout = 5,
                EnableEncryption = true,
                SessionTimeout = 480,
                EnableLogging = true,
                MinimumLogLevel = LogLevel.Info,
                LogRetentionDays = 30,
                ShowSystemTray = true,
                MinimizeToTray = true,
                AutoRefreshInterval = 5,
                LastSettingsUpdate = DateTime.Now,
                SettingsVersion = 1
            };
        }

        /// <summary>
        /// Save settings backup to JSON file
        /// </summary>
        private void SaveSettingsBackup(AppSettings settings)
        {
            try
            {
                var options = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                };

                var json = JsonSerializer.Serialize(settings, options);
                File.WriteAllText(_settingsFilePath, json);
            }
            catch (Exception ex)
            {
                // Log error but don't throw - backup is not critical
                System.Diagnostics.Debug.WriteLine($"خطأ في حفظ نسخة احتياطية من الإعدادات: {ex.Message}");
            }
        }

        /// <summary>
        /// Export settings to file
        /// </summary>
        public string ExportSettings(string? exportPath = null)
        {
            try
            {
                if (string.IsNullOrEmpty(exportPath))
                {
                    var exportDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), 
                        "CommunicationAssistantPro", "Exports");
                    if (!Directory.Exists(exportDirectory))
                    {
                        Directory.CreateDirectory(exportDirectory);
                    }
                    exportPath = Path.Combine(exportDirectory, $"settings_export_{DateTime.Now:yyyyMMdd_HHmmss}.json");
                }

                var settings = GetSettings();
                var options = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                };

                var json = JsonSerializer.Serialize(settings, options);
                File.WriteAllText(exportPath, json);

                return exportPath;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"خطأ في تصدير الإعدادات: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Import settings from file
        /// </summary>
        public void ImportSettings(string importPath)
        {
            try
            {
                if (!File.Exists(importPath))
                    throw new FileNotFoundException($"ملف الإعدادات غير موجود: {importPath}");

                var json = File.ReadAllText(importPath);
                var options = new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                };

                var importedSettings = JsonSerializer.Deserialize<AppSettings>(json, options);
                if (importedSettings == null)
                    throw new InvalidOperationException("لم يتم العثور على إعدادات صالحة في الملف");

                // Validate imported settings
                ValidateSettings(importedSettings);

                // Update settings
                UpdateSettings(importedSettings);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"خطأ في استيراد الإعدادات: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Reset settings to default values
        /// </summary>
        public void ResetSettings()
        {
            try
            {
                var defaultSettings = CreateDefaultSettings();
                UpdateSettings(defaultSettings);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"خطأ في إعادة تعيين الإعدادات: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Get PBX configuration
        /// </summary>
        public (string ip, int port, string username, string password, bool useAMI) GetPBXConfiguration()
        {
            var settings = GetSettings();
            return (settings.PBX_IP, settings.PBX_Port, settings.PBX_Username, settings.PBX_Password, settings.PBX_UseAMI);
        }

        /// <summary>
        /// Update PBX configuration
        /// </summary>
        public void UpdatePBXConfiguration(string ip, int port, string username, string password, bool useAMI = true)
        {
            try
            {
                var settings = GetSettings();
                settings.PBX_IP = ip;
                settings.PBX_Port = port;
                settings.PBX_Username = username;
                settings.PBX_Password = password;
                settings.PBX_UseAMI = useAMI;

                UpdateSettings(settings);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"خطأ في تحديث إعدادات PBX: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Get call status file configuration
        /// </summary>
        public (bool enabled, string path, int timeout) GetCallStatusFileConfig()
        {
            var settings = GetSettings();
            return (settings.EnableCallStatusFile, settings.CallStatusFilePath, settings.CallStatusFileTimeout);
        }

        /// <summary>
        /// Update call status file configuration
        /// </summary>
        public void UpdateCallStatusFileConfig(bool enabled, string path, int timeout)
        {
            try
            {
                var settings = GetSettings();
                settings.EnableCallStatusFile = enabled;
                settings.CallStatusFilePath = path;
                settings.CallStatusFileTimeout = timeout;

                UpdateSettings(settings);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"خطأ في تحديث إعدادات ملف حالة المكالمات: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Test PBX connection with current settings
        /// </summary>
        public async Task<bool> TestPBXConnectionAsync()
        {
            try
            {
                var (ip, port, username, password, useAMI) = GetPBXConfiguration();
                
                if (useAMI)
                {
                    return await TestAMIConnectionAsync(ip, port, username, password);
                }
                else
                {
                    return await TestHTTPConnectionAsync(ip, port);
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"خطأ في اختبار الاتصال: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Test AMI connection
        /// </summary>
        private async Task<bool> TestAMIConnectionAsync(string ip, int port, string username, string password)
        {
            try
            {
                using var client = new System.Net.Sockets.TcpClient();
                var connectTask = client.ConnectAsync(ip, port);
                
                // Wait for connection with timeout
                var completedTask = await Task.WhenAny(connectTask, Task.Delay(5000));
                
                if (completedTask != connectTask)
                {
                    return false; // Timeout
                }

                return client.Connected;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Test HTTP connection
        /// </summary>
        private async Task<bool> TestHTTPConnectionAsync(string ip, int port)
        {
            try
            {
                using var httpClient = new System.Net.Http.HttpClient();
                httpClient.Timeout = TimeSpan.FromSeconds(5);
                
                var response = await httpClient.GetAsync($"http://{ip}:{port}/api/status");
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Check if settings need to be reconfigured
        /// </summary>
        public bool NeedsConfiguration()
        {
            try
            {
                var settings = GetSettings();
                
                // Check if PBX is configured
                if (string.IsNullOrWhiteSpace(settings.PBX_IP) || 
                    string.IsNullOrWhiteSpace(settings.PBX_Username) ||
                    settings.PBX_Port <= 0)
                {
                    return true;
                }

                // Check if essential paths are configured
                if (settings.EnableCallStatusFile && string.IsNullOrWhiteSpace(settings.CallStatusFilePath))
                {
                    return true;
                }

                return false;
            }
            catch
            {
                return true;
            }
        }

        /// <summary>
        /// Get settings version
        /// </summary>
        public int GetSettingsVersion()
        {
            return GetSettings().SettingsVersion;
        }

        /// <summary>
        /// Get last settings update time
        /// </summary>
        public DateTime GetLastSettingsUpdate()
        {
            return GetSettings().LastSettingsUpdate;
        }
    }
}
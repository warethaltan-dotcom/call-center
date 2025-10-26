using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using CommunicationAssistantPro.Services;
using CommunicationAssistantPro.Views;
using CommunicationAssistantPro.Utilities;

namespace CommunicationAssistantPro
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// Basra Delight Call Center - مركز الاتصال
    /// Basra Delight Group - البصرة - العراق
    /// </summary>
    public partial class App : Application
    {
        private DatabaseService _databaseService;
        private PBXService _pbxService;
        private SettingsService _settingsService;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            
            try
            {
                // Initialize application directories
                InitializeDirectories();
                
                // Initialize database
                _databaseService = new DatabaseService();
                _databaseService.InitializeDatabase();
                
                // Initialize settings
                _settingsService = new SettingsService();
                _settingsService.LoadSettings();
                
                // Initialize PBX service
                _pbxService = new PBXService(_settingsService);
                
                // Set up unhandled exception handling
                SetupExceptionHandling();
                
                // Start the application
                ShowLoginWindow();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"خطأ في بدء التشغيل: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown();
            }
        }

        private void InitializeDirectories()
        {
            // Create necessary directories
            var directories = new[]
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BasraDelightCallCenter", "Logs"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BasraDelightCallCenter", "Data"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BasraDelightCallCenter", "Temp")
            };

            foreach (var dir in directories)
            {
                if (!Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }
            }
        }

        private void SetupExceptionHandling()
        {
            // Unhandled exception handling
            this.DispatcherUnhandledException += (s, e) =>
            {
                var errorLog = new ErrorLogService();
                errorLog.LogError(e.Exception);
                MessageBox.Show($"حدث خطأ غير متوقع: {e.Exception.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
                e.Handled = true;
            };

            // Unobserved task exception handling
            TaskScheduler.UnobservedTaskException += (s, e) =>
            {
                var errorLog = new ErrorLogService();
                errorLog.LogError(e.Exception);
                MessageBox.Show($"خطأ في المهمة الخلفية: {e.Exception.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Warning);
                e.SetObserved();
            };
        }

        private void ShowLoginWindow()
        {
            var loginWindow = new LoginWindow();
            loginWindow.Show();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            try
            {
                // Clean up services
                _pbxService?.Disconnect();
                _databaseService?.Dispose();
            }
            catch (Exception ex)
            {
                // Log error but don't throw during exit
                var errorLog = new ErrorLogService();
                errorLog.LogError(ex);
            }
            finally
            {
                base.OnExit(e);
            }
        }

        /// <summary>
        /// Get current user session information
        /// </summary>
        public static Models.User CurrentUser { get; private set; }
        
        /// <summary>
        /// Set current user session
        /// </summary>
        public static void SetCurrentUser(Models.User user)
        {
            CurrentUser = user;
        }
    }
}
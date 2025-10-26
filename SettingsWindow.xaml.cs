using System;
using System.ComponentModel;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Win32;
using CommunicationAssistantPro.Models;
using CommunicationAssistantPro.Services;
using CommunicationAssistantPro.Utilities;
using CommunicationAssistantPro.ViewModels;

namespace CommunicationAssistantPro.Views
{
    /// <summary>
    /// Interaction logic for SettingsWindow.xaml
    /// نافذة الإعدادات - Communication Assistant Pro
    /// </summary>
    public partial class SettingsWindow : Window
    {
        private readonly SettingsViewModel _viewModel;
        private readonly SettingsService _settingsService;
        private readonly ErrorLogService _errorLog;

        public SettingsWindow()
        {
            InitializeComponent();
            
            // Initialize services
            _settingsService = new SettingsService();
            _errorLog = new ErrorLogService();
            _viewModel = new SettingsViewModel(_settingsService);
            
            DataContext = _viewModel;
            
            // Set up event handlers
            Loaded += SettingsWindow_Loaded;
            Closing += SettingsWindow_Closing;
            
            // Subscribe to view model events
            _viewModel.PropertyChanged += ViewModel_PropertyChanged;
        }

        private void SettingsWindow_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                // Load current settings
                LoadCurrentSettings();
                
                // Show general settings by default
                ShowSettingsPanel("General");
                
                _errorLog.LogInfo("تم تحميل نافذة الإعدادات", "SettingsWindow_Loaded");
            }
            catch (Exception ex)
            {
                _errorLog.LogError(ex, "SettingsWindow_Loaded");
                MessageBox.Show($"خطأ في تحميل الإعدادات: {ex.Message}", "خطأ", 
                              MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void LoadCurrentSettings()
        {
            try
            {
                var settings = _settingsService.GetSettings();
                
                // General Settings
                LanguageComboBox.SelectedIndex = settings.Language == "ar" ? 0 : 1;
                RTLSupportToggle.IsChecked = settings.RTLSupport;
                NotificationsToggle.IsChecked = settings.EnableNotifications;
                SoundsToggle.IsChecked = settings.EnableSounds;
                AutoRefreshTextBox.Text = settings.AutoRefreshInterval.ToString();
                ThemeComboBox.SelectedIndex = settings.EnableDarkTheme ? 1 : 0;
                SessionTimeoutTextBox.Text = settings.SessionTimeout.ToString();
                AutoLoginToggle.IsChecked = settings.AutoLogin;
                
                // PBX Settings
                PBXIpTextBox.Text = settings.PBX_IP;
                PBXPortTextBox.Text = settings.PBX_Port.ToString();
                PBXUsernameTextBox.Text = settings.PBX_Username;
                
                if (settings.PBX_UseAMI)
                {
                    AMIRadioButton.IsChecked = true;
                    HTTPRadioButton.IsChecked = false;
                }
                else
                {
                    AMIRadioButton.IsChecked = false;
                    HTTPRadioButton.IsChecked = true;
                }
                
                // Call File Settings
                EnableCallFileToggle.IsChecked = settings.EnableCallStatusFile;
                CallFilePathTextBox.Text = settings.CallStatusFilePath;
                CallFileTimeoutTextBox.Text = settings.CallStatusFileTimeout.ToString();
                
                // Security Settings
                EnableEncryptionToggle.IsChecked = settings.EnableEncryption;
                MaxLoginAttemptsTextBox.Text = settings.MaxFailedLoginAttempts.ToString();
                LockoutDurationTextBox.Text = settings.LockoutDuration.ToString();
                RequirePasswordLogoutToggle.IsChecked = settings.RequirePasswordForLogout;
                LogRetentionTextBox.Text = settings.LogRetentionDays.ToString();
                BackupIntervalTextBox.Text = settings.DatabaseBackupInterval.ToString();
                
                // Update file preview
                UpdateFilePreview();
            }
            catch (Exception ex)
            {
                _errorLog.LogError(ex, "LoadCurrentSettings");
            }
        }

        private void ShowSettingsPanel(string panelName)
        {
            try
            {
                // Hide all panels
                GeneralSettingsPanel.Visibility = Visibility.Collapsed;
                PBXSettingsPanel.Visibility = Visibility.Collapsed;
                CallFileSettingsPanel.Visibility = Visibility.Collapsed;
                SecuritySettingsPanel.Visibility = Visibility.Collapsed;
                
                // Show selected panel
                switch (panelName.ToLower())
                {
                    case "general":
                        GeneralSettingsPanel.Visibility = Visibility.Visible;
                        UpdateButtonStates("general");
                        break;
                    case "pbx":
                        PBXSettingsPanel.Visibility = Visibility.Visible;
                        UpdateButtonStates("pbx");
                        break;
                    case "callfile":
                        CallFileSettingsPanel.Visibility = Visibility.Visible;
                        UpdateButtonStates("callfile");
                        break;
                    case "security":
                        SecuritySettingsPanel.Visibility = Visibility.Visible;
                        UpdateButtonStates("security");
                        break;
                }
            }
            catch (Exception ex)
            {
                _errorLog.LogError(ex, $"ShowSettingsPanel_{panelName}");
            }
        }

        private void UpdateButtonStates(string activeButton)
        {
            try
            {
                // Reset all buttons to outlined style
                GeneralSettingsButton.Style = (Style)FindResource("MaterialDesignOutlinedButton");
                PBXSettingsButton.Style = (Style)FindResource("MaterialDesignOutlinedButton");
                CallFileSettingsButton.Style = (Style)FindResource("MaterialDesignOutlinedButton");
                SecuritySettingsButton.Style = (Style)FindResource("MaterialDesignOutlinedButton");
                UserManagementButton.Style = (Style)FindResource("MaterialDesignOutlinedButton");

                // Set active button to raised style
                switch (activeButton.ToLower())
                {
                    case "general":
                        GeneralSettingsButton.Style = (Style)FindResource("MaterialDesignRaisedButton");
                        break;
                    case "pbx":
                        PBXSettingsButton.Style = (Style)FindResource("MaterialDesignRaisedButton");
                        break;
                    case "callfile":
                        CallFileSettingsButton.Style = (Style)FindResource("MaterialDesignRaisedButton");
                        break;
                    case "security":
                        SecuritySettingsButton.Style = (Style)FindResource("MaterialDesignRaisedButton");
                        break;
                    case "user":
                        UserManagementButton.Style = (Style)FindResource("MaterialDesignRaisedButton");
                        break;
                }
            }
            catch (Exception ex)
            {
                _errorLog.LogError(ex, "UpdateButtonStates");
            }
        }

        private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(SettingsViewModel.ConnectionStatus))
            {
                Dispatcher.Invoke(() =>
                {
                    PBXConnectionStatusTextBlock.Text = _viewModel.ConnectionStatus;
                });
            }
        }

        private async void TestConnection()
        {
            try
            {
                PBXConnectionStatusTextBlock.Text = "جاري الاختبار...";
                PBXConnectionStatusTextBlock.Foreground = System.Windows.Media.Brushes.Orange;
                
                var connected = await _settingsService.TestPBXConnectionAsync();
                
                if (connected)
                {
                    PBXConnectionStatusTextBlock.Text = "متصل بنجاح";
                    PBXConnectionStatusTextBlock.Foreground = System.Windows.Media.Brushes.Green;
                    
                    _errorLog.LogInfo("تم اختبار الاتصال بـ PBX بنجاح", "TestConnection");
                }
                else
                {
                    PBXConnectionStatusTextBlock.Text = "فشل الاتصال";
                    PBXConnectionStatusTextBlock.Foreground = System.Windows.Media.Brushes.Red;
                    
                    _errorLog.LogWarning("فشل اختبار الاتصال بـ PBX", "TestConnection");
                }
            }
            catch (Exception ex)
            {
                PBXConnectionStatusTextBlock.Text = "خطأ في الاختبار";
                PBXConnectionStatusTextBlock.Foreground = System.Windows.Media.Brushes.Red;
                
                _errorLog.LogError(ex, "TestConnection");
            }
        }

        private async void SaveSettings()
        {
            try
            {
                SaveButton.IsEnabled = false;
                
                // Validate and save settings
                var settings = _settingsService.GetSettings();
                
                // General Settings
                settings.Language = (LanguageComboBox.SelectedItem as ComboBoxItem)?.Content.ToString() == "العربية" ? "ar" : "en";
                settings.RTLSupport = RTLSupportToggle.IsChecked == true;
                settings.EnableNotifications = NotificationsToggle.IsChecked == true;
                settings.EnableSounds = SoundsToggle.IsChecked == true;
                settings.AutoRefreshInterval = int.TryParse(AutoRefreshTextBox.Text, out var refreshRate) ? refreshRate : 5;
                settings.EnableDarkTheme = ThemeComboBox.SelectedIndex == 1;
                settings.SessionTimeout = int.TryParse(SessionTimeoutTextBox.Text, out var sessionTimeout) ? sessionTimeout : 480;
                settings.AutoLogin = AutoLoginToggle.IsChecked == true;
                
                // PBX Settings
                settings.PBX_IP = PBXIpTextBox.Text;
                settings.PBX_Port = int.TryParse(PBXPortTextBox.Text, out var port) ? port : 5038;
                settings.PBX_Username = PBXUsernameTextBox.Text;
                settings.PBX_Password = PBXPasswordBox.Password;
                settings.PBX_UseAMI = AMIRadioButton.IsChecked == true;
                
                // Call File Settings
                settings.EnableCallStatusFile = EnableCallFileToggle.IsChecked == true;
                settings.CallStatusFilePath = CallFilePathTextBox.Text;
                settings.CallStatusFileTimeout = int.TryParse(CallFileTimeoutTextBox.Text, out var timeout) ? timeout : 3;
                
                // Security Settings
                settings.EnableEncryption = EnableEncryptionToggle.IsChecked == true;
                settings.MaxFailedLoginAttempts = int.TryParse(MaxLoginAttemptsTextBox.Text, out var maxAttempts) ? maxAttempts : 3;
                settings.LockoutDuration = int.TryParse(LockoutDurationTextBox.Text, out var lockoutDuration) ? lockoutDuration : 15;
                settings.RequirePasswordForLogout = RequirePasswordLogoutToggle.IsChecked == true;
                settings.LogRetentionDays = int.TryParse(LogRetentionTextBox.Text, out var logRetention) ? logRetention : 30;
                settings.DatabaseBackupInterval = int.TryParse(BackupIntervalTextBox.Text, out var backupInterval) ? backupInterval : 24;
                
                // Save settings
                _settingsService.UpdateSettings(settings);
                
                _errorLog.LogInfo("تم حفظ الإعدادات بنجاح", "SaveSettings");
                
                MessageBox.Show("تم حفظ الإعدادات بنجاح", "نجح", 
                              MessageBoxButton.OK, MessageBoxImage.Information);
                
                // Close window after save
                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                _errorLog.LogError(ex, "SaveSettings");
                MessageBox.Show($"خطأ في حفظ الإعدادات: {ex.Message}", "خطأ", 
                              MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                SaveButton.IsEnabled = true;
            }
        }

        private void Cancel()
        {
            try
            {
                var result = MessageBox.Show("هل تريد إلغاء التغييرات؟", "تأكيد الإلغاء", 
                                            MessageBoxButton.YesNo, MessageBoxImage.Question);
                
                if (result == MessageBoxResult.Yes)
                {
                    DialogResult = false;
                    Close();
                }
            }
            catch (Exception ex)
            {
                _errorLog.LogError(ex, "Cancel");
            }
        }

        private void UpdateFilePreview()
        {
            try
            {
                if (File.Exists(CallFilePathTextBox.Text))
                {
                    var content = File.ReadAllText(CallFilePathTextBox.Text);
                    FilePreviewTextBlock.Text = string.IsNullOrWhiteSpace(content) ? 
                        "الملف فارغ" : content;
                }
                else
                {
                    FilePreviewTextBlock.Text = "الملف غير موجود";
                }
            }
            catch (Exception ex)
            {
                FilePreviewTextBlock.Text = $"خطأ في قراءة الملف: {ex.Message}";
            }
        }

        #region Event Handlers

        private void GeneralSettingsButton_Click(object sender, RoutedEventArgs e)
        {
            ShowSettingsPanel("general");
        }

        private void PBXSettingsButton_Click(object sender, RoutedEventArgs e)
        {
            ShowSettingsPanel("pbx");
        }

        private void CallFileSettingsButton_Click(object sender, RoutedEventArgs e)
        {
            ShowSettingsPanel("callfile");
        }

        private void SecuritySettingsButton_Click(object sender, RoutedEventArgs e)
        {
            ShowSettingsPanel("security");
        }

        private void UserManagementButton_Click(object sender, RoutedEventArgs e)
        {
            // TODO: Implement user management view
            MessageBox.Show("إدارة المستخدمين قيد التطوير", "قريباً", 
                          MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void TestConnectionButton_Click(object sender, RoutedEventArgs e)
        {
            TestConnection();
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            SaveSettings();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            Cancel();
        }

        private void SettingsWindow_Closing(object? sender, CancelEventArgs e)
        {
            // Check for unsaved changes
            if (_viewModel.HasUnsavedChanges)
            {
                var result = MessageBox.Show("يوجد تغييرات غير محفوظة. هل تريد حفظها؟", "تأكيد الإغلاق", 
                                            MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
                
                switch (result)
                {
                    case MessageBoxResult.Yes:
                        SaveSettings();
                        break;
                    case MessageBoxResult.Cancel:
                        e.Cancel = true;
                        break;
                }
            }
        }

        private void BrowseFilePathButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var openFileDialog = new OpenFileDialog
                {
                    Title = "اختر مسار ملف حالة المكالمات",
                    Filter = "Dat files (*.dat)|*.dat|All files (*.*)|*.*",
                    FileName = "CaCallstatus.dat"
                };

                if (openFileDialog.ShowDialog() == true)
                {
                    CallFilePathTextBox.Text = openFileDialog.FileName;
                    UpdateFilePreview();
                }
            }
            catch (Exception ex)
            {
                _errorLog.LogError(ex, "BrowseFilePathButton_Click");
                MessageBox.Show($"خطأ في اختيار الملف: {ex.Message}", "خطأ", 
                              MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion
    }

    /// <summary>
    /// View model for settings window
    /// </summary>
    public class SettingsViewModel : INotifyPropertyChanged
    {
        private readonly SettingsService _settingsService;
        private readonly ErrorLogService _errorLog;
        
        private string _connectionStatus = "غير متصل";
        private bool _hasUnsavedChanges;

        public event PropertyChangedEventHandler? PropertyChanged;
        public event EventHandler<string>? SettingsChanged;

        public SettingsViewModel(SettingsService settingsService)
        {
            _settingsService = settingsService;
            _errorLog = new ErrorLogService();
        }

        public Models.AppSettings Settings => _settingsService.GetSettings();

        public string ConnectionStatus
        {
            get => _connectionStatus;
            set
            {
                _connectionStatus = value;
                OnPropertyChanged(nameof(ConnectionStatus));
            }
        }

        public bool HasUnsavedChanges
        {
            get => _hasUnsavedChanges;
            set
            {
                _hasUnsavedChanges = value;
                OnPropertyChanged(nameof(HasUnsavedChanges));
            }
        }

        public async Task<bool> TestPBXConnectionAsync()
        {
            try
            {
                ConnectionStatus = "جاري الاختبار...";
                var result = await _settingsService.TestPBXConnectionAsync();
                ConnectionStatus = result ? "متصل" : "غير متصل";
                return result;
            }
            catch (Exception ex)
            {
                _errorLog.LogError(ex, "SettingsViewModel_TestPBXConnection");
                ConnectionStatus = "خطأ في الاتصال";
                return false;
            }
        }

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            
            if (propertyName != nameof(HasUnsavedChanges))
            {
                HasUnsavedChanges = true;
            }
        }
    }
}
using System;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using CommunicationAssistantPro.Models;
using CommunicationAssistantPro.Services;
using CommunicationAssistantPro.Utilities;
using CommunicationAssistantPro.ViewModels;

namespace CommunicationAssistantPro.Views
{
    /// <summary>
    /// Interaction logic for LoginWindow.xaml
    /// نظام تسجيل الدخول - Communication Assistant Pro
    /// </summary>
    public partial class LoginWindow : Window
    {
        private readonly LoginViewModel _viewModel;
        private readonly DatabaseService _databaseService;
        private readonly ErrorLogService _errorLog;

        public LoginWindow()
        {
            InitializeComponent();
            
            _viewModel = new LoginViewModel();
            _databaseService = new DatabaseService();
            _errorLog = new ErrorLogService();
            
            DataContext = _viewModel;
            
            // Set up event handlers
            Loaded += LoginWindow_Loaded;
            KeyDown += LoginWindow_KeyDown;
            
            // Subscribe to view model events
            _viewModel.LoginSuccessful += ViewModel_LoginSuccessful;
            _viewModel.LoginFailed += ViewModel_LoginFailed;
            _viewModel.PropertyChanged += ViewModel_PropertyChanged;
        }

        private void LoginWindow_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                // Load remembered credentials if available
                LoadRememberedCredentials();
                
                // Focus on username field
                UsernameTextBox.Focus();
                
                _errorLog.LogInfo("تم تحميل نافذة تسجيل الدخول", "LoginWindow");
            }
            catch (Exception ex)
            {
                _errorLog.LogError(ex, "LoginWindow_Loaded");
                ShowStatusMessage("خطأ في تحميل نافذة تسجيل الدخول", true);
            }
        }

        private void LoginWindow_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                TryLogin();
            }
        }

        private void ViewModel_LoginSuccessful(object? sender, EventArgs e)
        {
            try
            {
                // Update UI on login success
                Dispatcher.Invoke(() =>
                {
                    ShowStatusMessage("تم تسجيل الدخول بنجاح", false);
                    
                    // Store session information
                    App.SetCurrentUser(_viewModel.CurrentUser);
                    
                    // Show main window
                    var mainWindow = new MainWindow();
                    mainWindow.Show();
                    
                    // Close login window
                    Close();
                });
            }
            catch (Exception ex)
            {
                _errorLog.LogError(ex, "ViewModel_LoginSuccessful");
            }
        }

        private void ViewModel_LoginFailed(object? sender, string errorMessage)
        {
            Dispatcher.Invoke(() =>
            {
                ShowStatusMessage(errorMessage, true);
                
                // Focus on password field for retry
                PasswordBox.Focus();
                PasswordBox.SelectAll();
            });
        }

        private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(LoginViewModel.IsBusy))
            {
                Dispatcher.Invoke(() =>
                {
                    LoginButton.IsEnabled = !_viewModel.IsBusy;
                    UsernameTextBox.IsEnabled = !_viewModel.IsBusy;
                    PasswordBox.IsEnabled = !_viewModel.IsBusy;
                    ExtensionTextBox.IsEnabled = !_viewModel.IsBusy;
                });
            }
        }

        private void TryLogin()
        {
            try
            {
                // Validate input
                if (string.IsNullOrWhiteSpace(UsernameTextBox.Text))
                {
                    UsernameTextBox.Focus();
                    ShowStatusMessage("يرجى إدخال اسم المستخدم", true);
                    return;
                }

                if (string.IsNullOrWhiteSpace(PasswordBox.Password))
                {
                    PasswordBox.Focus();
                    ShowStatusMessage("يرجى إدخال كلمة المرور", true);
                    return;
                }

                // Attempt login
                _viewModel.Login(UsernameTextBox.Text.Trim(), PasswordBox.Password, ExtensionTextBox.Text.Trim());
            }
            catch (Exception ex)
            {
                _errorLog.LogError(ex, "TryLogin");
                ShowStatusMessage($"خطأ في تسجيل الدخول: {ex.Message}", true);
            }
        }

        private void ShowStatusMessage(string message, bool isError)
        {
            StatusTextBlock.Text = message;
            StatusTextBlock.Visibility = Visibility.Visible;
            StatusTextBlock.Foreground = isError ? 
                Application.Current.Resources["MaterialDesignBodyLight"] as Brush : 
                Application.Current.Resources["MaterialDesignBody"] as Brush;
            
            // Auto-hide message after 5 seconds if not error
            if (!isError)
            {
                _ = Task.Delay(5000).ContinueWith(_ =>
                {
                    Dispatcher.Invoke(() => StatusTextBlock.Visibility = Visibility.Collapsed);
                });
            }
        }

        private void LoadRememberedCredentials()
        {
            try
            {
                var settingsService = new SettingsService();
                var settings = settingsService.GetSettings();
                
                if (settings.RememberMe)
                {
                    // Load from secure storage (implementation needed)
                    // For now, we'll just set the remember me checkbox
                    RememberMeCheckBox.IsChecked = true;
                    
                    // Note: In a real implementation, you would decrypt and load saved credentials
                    // from secure storage or encrypted settings file
                }
            }
            catch (Exception ex)
            {
                _errorLog.LogWarning($"خطأ في تحميل بيانات الاعتماد المحفوظة: {ex.Message}");
            }
        }

        private void SaveRememberedCredentials()
        {
            try
            {
                if (RememberMeCheckBox.IsChecked == true)
                {
                    var settingsService = new SettingsService();
                    var settings = settingsService.GetSettings();
                    
                    settings.RememberMe = true;
                    settings.AutoLogin = true;
                    
                    // Save credentials securely (implementation needed)
                    // For now, we just update the settings
                    settingsService.UpdateSettings(settings);
                    
                    _errorLog.LogInfo("تم حفظ بيانات الاعتماد للتذكر", "LoginWindow");
                }
                else
                {
                    var settingsService = new SettingsService();
                    var settings = settingsService.GetSettings();
                    
                    settings.RememberMe = false;
                    settings.AutoLogin = false;
                    
                    settingsService.UpdateSettings(settings);
                    
                    _errorLog.LogInfo("تم حذف بيانات الاعتماد المحفوظة", "LoginWindow");
                }
            }
            catch (Exception ex)
            {
                _errorLog.LogError(ex, "SaveRememberedCredentials");
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            try
            {
                // Save remembered credentials if any
                SaveRememberedCredentials();
                
                // Clean up resources
                _databaseService?.Dispose();
            }
            catch (Exception ex)
            {
                _errorLog.LogError(ex, "LoginWindow_OnClosed");
            }
            finally
            {
                base.OnClosed(e);
            }
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            // This method handles the LoginButton click
            TryLogin();
        }

        private void OpenSettingsCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            try
            {
                var settingsWindow = new SettingsWindow();
                settingsWindow.ShowDialog();
            }
            catch (Exception ex)
            {
                _errorLog.LogError(ex, "OpenSettingsCommand_Executed");
                ShowStatusMessage($"خطأ في فتح الإعدادات: {ex.Message}", true);
            }
        }
    }

    /// <summary>
    /// Command execution for settings button
    /// </summary>
    public class SettingsCommand : ICommand
    {
        public event EventHandler? CanExecuteChanged;

        public bool CanExecute(object? parameter)
        {
            return true;
        }

        public void Execute(object? parameter)
        {
            try
            {
                var settingsWindow = new SettingsWindow();
                settingsWindow.ShowDialog();
            }
            catch (Exception ex)
            {
                var errorLog = new ErrorLogService();
                errorLog.LogError(ex, "SettingsCommand_Execute");
                MessageBox.Show($"خطأ في فتح الإعدادات: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
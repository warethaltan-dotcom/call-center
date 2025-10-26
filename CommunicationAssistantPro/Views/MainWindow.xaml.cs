using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Threading.Tasks;
using CommunicationAssistantPro.Models;
using CommunicationAssistantPro.Services;
using CommunicationAssistantPro.Utilities;
using CommunicationAssistantPro.ViewModels;

namespace CommunicationAssistantPro.Views
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// النافذة الرئيسية - Communication Assistant Pro
    /// Basra Delight Group - البصرة، العراق
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly MainWindowViewModel _viewModel;
        private readonly DatabaseService _databaseService;
        private readonly ErrorLogService _errorLog;
        private readonly PBXService _pbxService;
        private System.Windows.Threading.DispatcherTimer _timer;

        public MainWindow()
        {
            InitializeComponent();
            
            // Initialize services
            _databaseService = new DatabaseService();
            _errorLog = new ErrorLogService();
            _viewModel = new MainWindowViewModel(_databaseService);
            
            // Set up PBX service
            var settingsService = new Services.SettingsService();
            _pbxService = new PBXService(settingsService);
            
            DataContext = _viewModel;
            
            // Set up timer for live updates
            _timer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(30)
            };
            _timer.Tick += Timer_Tick;
            
            // Set up event handlers
            Loaded += MainWindow_Loaded;
            Closing += MainWindow_Closing;
            
            // Subscribe to view model events
            _viewModel.PropertyChanged += ViewModel_PropertyChanged;
            
            // Subscribe to PBX events
            _pbxService.CallReceived += PBXService_CallReceived;
            _pbxService.CallAnswered += PBXService_CallAnswered;
            _pbxService.CallEnded += PBXService_CallEnded;
            _pbxService.UserStatusChanged += PBXService_UserStatusChanged;
            _pbxService.ConnectionStatusChanged += PBXService_ConnectionStatusChanged;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                // Load initial data
                _viewModel.LoadDashboardData();
                
                // Connect to PBX
                ConnectToPBXAsync();
                
                // Start timer
                _timer.Start();
                
                _errorLog.LogInfo($"تم تحميل النافذة الرئيسية للمستخدم: {App.CurrentUser?.FullName}", "MainWindow_Loaded");
            }
            catch (Exception ex)
            {
                _errorLog.LogError(ex, "MainWindow_Loaded");
                MessageBox.Show($"خطأ في تحميل النافذة الرئيسية: {ex.Message}", "خطأ", 
                              MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private async void ConnectToPBXAsync()
        {
            try
            {
                var settingsService = new Services.SettingsService();
                var settings = settingsService.GetSettings();
                
                if (!string.IsNullOrWhiteSpace(settings.PBX_IP))
                {
                    var connected = await _pbxService.ConnectAsync();
                    if (connected)
                    {
                        _errorLog.LogInfo("تم الاتصال بـ PBX بنجاح من النافذة الرئيسية", "ConnectToPBXAsync");
                    }
                    else
                    {
                        _errorLog.LogWarning("فشل في الاتصال بـ PBX من النافذة الرئيسية", "ConnectToPBXAsync");
                    }
                }
            }
            catch (Exception ex)
            {
                _errorLog.LogError(ex, "ConnectToPBXAsync");
            }
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            try
            {
                // Update time display
                _viewModel.UpdateCurrentTime();
                
                // Refresh dashboard data periodically
                if (_viewModel.ShouldRefreshDashboard)
                {
                    _viewModel.LoadDashboardData();
                }
            }
            catch (Exception ex)
            {
                _errorLog.LogError(ex, "Timer_Tick");
            }
        }

        private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(MainWindowViewModel.IsAdmin))
            {
                Dispatcher.Invoke(() =>
                {
                    // Update admin section visibility
                    AdminSectionText.Visibility = _viewModel.IsAdmin ? Visibility.Visible : Visibility.Collapsed;
                    ReportsButton.Visibility = _viewModel.IsAdmin ? Visibility.Visible : Visibility.Collapsed;
                });
            }
            
            if (e.PropertyName == nameof(MainWindowViewModel.ConnectionStatus))
            {
                Dispatcher.Invoke(() =>
                {
                    // Update connection status display
                    // Note: ConnectionStatusTextBlock element should be added to MainWindow.xaml if needed
                    // if (ConnectionStatusTextBlock != null)
                    //     ConnectionStatusTextBlock.Text = _viewModel.ConnectionStatus;
                });
            }
        }

        #region PBX Event Handlers

        private void PBXService_CallReceived(object? sender, CallEventArgs e)
        {
            try
            {
                Dispatcher.Invoke(() =>
                {
                    _errorLog.LogInfo($"تم استقبال مكالمة من: {e.Call.CallerId} للامتداد: {e.Call.Extension}", "PBXService_CallReceived");
                    
                    // Show notification
                    ShowCallNotification("مكالمة واردة", $"مكالمة من: {e.Call.CallerId}", e.Call);
                    
                    // Refresh dashboard if needed
                    if (_viewModel.CurrentView == "Dashboard")
                    {
                        _viewModel.LoadDashboardData();
                    }
                });
            }
            catch (Exception ex)
            {
                _errorLog.LogError(ex, "PBXService_CallReceived");
            }
        }

        private void PBXService_CallAnswered(object? sender, CallEventArgs e)
        {
            try
            {
                Dispatcher.Invoke(() =>
                {
                    _errorLog.LogInfo($"تم الرد على المكالمة من: {e.Call.CallerId}", "PBXService_CallAnswered");
                    
                    // Update call status
                    ShowCallNotification("تم الرد على المكالمة", $"تم الرد على مكالمة من: {e.Call.CallerId}", e.Call);
                    
                    // Refresh dashboard
                    if (_viewModel.CurrentView == "Dashboard")
                    {
                        _viewModel.LoadDashboardData();
                    }
                });
            }
            catch (Exception ex)
            {
                _errorLog.LogError(ex, "PBXService_CallAnswered");
            }
        }

        private void PBXService_CallEnded(object? sender, CallEventArgs e)
        {
            try
            {
                Dispatcher.Invoke(() =>
                {
                    _errorLog.LogInfo($"انتهت المكالمة من: {e.Call.CallerId} - المدة: {e.Call.Duration?.ToString()}", "PBXService_CallEnded");
                    
                    // Show call ended notification
                    ShowCallNotification("انتهت المكالمة", 
                        $"انتهت المكالمة من: {e.Call.CallerId} - المدة: {e.Call.Duration?.ToString()}", e.Call);
                    
                    // Refresh dashboard
                    if (_viewModel.CurrentView == "Dashboard")
                    {
                        _viewModel.LoadDashboardData();
                    }
                });
            }
            catch (Exception ex)
            {
                _errorLog.LogError(ex, "PBXService_CallEnded");
            }
        }

        private void PBXService_UserStatusChanged(object? sender, UserStatusEventArgs e)
        {
            try
            {
                Dispatcher.Invoke(() =>
                {
                    _errorLog.LogInfo($"تغيرت حالة المستخدم ID: {e.UserId} إلى: {e.Status}", "PBXService_UserStatusChanged");
                    
                    // Refresh dashboard data if on dashboard view
                    if (_viewModel.CurrentView == "Dashboard")
                    {
                        _viewModel.LoadDashboardData();
                    }
                });
            }
            catch (Exception ex)
            {
                _errorLog.LogError(ex, "PBXService_UserStatusChanged");
            }
        }

        private void PBXService_ConnectionStatusChanged(object? sender, PBXEventArgs e)
        {
            try
            {
                Dispatcher.Invoke(() =>
                {
                    _viewModel.UpdateConnectionStatus(e.IsConnected, e.Message);
                    
                    if (e.IsConnected)
                    {
                        _errorLog.LogInfo($"حالة الاتصال: {e.Message}", "PBXService_ConnectionStatusChanged");
                    }
                    else
                    {
                        _errorLog.LogWarning($"انقطع الاتصال: {e.Message}", "PBXService_ConnectionStatusChanged");
                    }
                });
            }
            catch (Exception ex)
            {
                _errorLog.LogError(ex, "PBXService_ConnectionStatusChanged");
            }
        }

        #endregion

        #region Navigation and UI Methods

        private void NavigateTo(string viewName)
        {
            try
            {
                switch (viewName.ToLower())
                {
                    case "dashboard":
                        ShowDashboard();
                        break;
                    case "calls":
                        ShowCallsView();
                        break;
                    case "users":
                        ShowUsersView();
                        break;
                    case "reports":
                        if (_viewModel.IsAdmin)
                        {
                            ShowReportsView();
                        }
                        else
                        {
                            MessageBox.Show("ليس لديك صلاحية للوصول إلى التقارير", "صلاحيات", 
                                          MessageBoxButton.OK, MessageBoxImage.Information);
                        }
                        break;
                    case "settings":
                        ShowSettingsView();
                        break;
                    default:
                        ShowDashboard();
                        break;
                }
            }
            catch (Exception ex)
            {
                _errorLog.LogError(ex, $"NavigateTo_{viewName}");
                MessageBox.Show($"خطأ في التنقل إلى: {viewName}", "خطأ", 
                              MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ShowDashboard()
        {
            try
            {
                _viewModel.CurrentView = "Dashboard";
                _viewModel.LoadDashboardData();
                
                // Update button states
                UpdateNavigationButtonStates("dashboard");
                
                _errorLog.LogInfo("تم عرض لوحة التحكم", "ShowDashboard");
            }
            catch (Exception ex)
            {
                _errorLog.LogError(ex, "ShowDashboard");
            }
        }

        private void ShowCallsView()
        {
            try
            {
                _viewModel.CurrentView = "Calls";
                // Load calls data
                // TODO: Implement calls view
                
                UpdateNavigationButtonStates("calls");
                _errorLog.LogInfo("تم عرض إدارة المكالمات", "ShowCallsView");
            }
            catch (Exception ex)
            {
                _errorLog.LogError(ex, "ShowCallsView");
            }
        }

        private void ShowUsersView()
        {
            try
            {
                _viewModel.CurrentView = "Users";
                // Load users data
                // TODO: Implement users view
                
                UpdateNavigationButtonStates("users");
                _errorLog.LogInfo("تم عرض إدارة المستخدمين", "ShowUsersView");
            }
            catch (Exception ex)
            {
                _errorLog.LogError(ex, "ShowUsersView");
            }
        }

        private void ShowReportsView()
        {
            try
            {
                _viewModel.CurrentView = "Reports";
                // Load reports data
                // TODO: Implement reports view
                
                UpdateNavigationButtonStates("reports");
                _errorLog.LogInfo("تم عرض التقارير", "ShowReportsView");
            }
            catch (Exception ex)
            {
                _errorLog.LogError(ex, "ShowReportsView");
            }
        }

        private void ShowSettingsView()
        {
            try
            {
                _viewModel.CurrentView = "Settings";
                var settingsWindow = new SettingsWindow();
                settingsWindow.ShowDialog();
                
                // Refresh dashboard data after settings change
                ShowDashboard();
                
                _errorLog.LogInfo("تم عرض الإعدادات", "ShowSettingsView");
            }
            catch (Exception ex)
            {
                _errorLog.LogError(ex, "ShowSettingsView");
            }
        }

        private void UpdateNavigationButtonStates(string activeButton)
        {
            try
            {
                // Reset all buttons to outlined style
                DashboardButton.Style = (Style)FindResource("MaterialDesignOutlinedButton");
                CallsButton.Style = (Style)FindResource("MaterialDesignOutlinedButton");
                UsersButton.Style = (Style)FindResource("MaterialDesignOutlinedButton");
                ReportsButton.Style = (Style)FindResource("MaterialDesignOutlinedButton");
                SettingsButton.Style = (Style)FindResource("MaterialDesignOutlinedButton");

                // Set active button to raised style
                switch (activeButton.ToLower())
                {
                    case "dashboard":
                        DashboardButton.Style = (Style)FindResource("MaterialDesignRaisedButton");
                        break;
                    case "calls":
                        CallsButton.Style = (Style)FindResource("MaterialDesignRaisedButton");
                        break;
                    case "users":
                        UsersButton.Style = (Style)FindResource("MaterialDesignRaisedButton");
                        break;
                    case "reports":
                        ReportsButton.Style = (Style)FindResource("MaterialDesignRaisedButton");
                        break;
                    case "settings":
                        SettingsButton.Style = (Style)FindResource("MaterialDesignRaisedButton");
                        break;
                }
            }
            catch (Exception ex)
            {
                _errorLog.LogError(ex, "UpdateNavigationButtonStates");
            }
        }

        private void ShowCallNotification(string title, string message, Call call)
        {
            try
            {
                // Create a simple notification
                var notificationWindow = new NotificationWindow(title, message, call);
                notificationWindow.Show();
                
                // Auto-close after 5 seconds
                _ = Task.Run(async () =>
                {
                    await System.Threading.Tasks.Task.Delay(5000);
                    Dispatcher.Invoke(() => notificationWindow.Close());
                });
            }
            catch (Exception ex)
            {
                _errorLog.LogError(ex, "ShowCallNotification");
            }
        }

        #endregion

        private void Logout()
        {
            try
            {
                var result = MessageBox.Show("هل أنت متأكد من تسجيل الخروج؟", "تأكيد", 
                                            MessageBoxButton.YesNo, MessageBoxImage.Question);
                
                if (result == MessageBoxResult.Yes)
                {
                    _errorLog.LogInfo($"تسجيل خروج المستخدم: {App.CurrentUser?.FullName}", "Logout");
                    
                    // Logout view model
                    _viewModel.Logout();
                    
                    // Disconnect from PBX
                    _pbxService.Disconnect();
                    
                    // Close main window and show login window
                    var loginWindow = new LoginWindow();
                    loginWindow.Show();
                    Close();
                }
            }
            catch (Exception ex)
            {
                _errorLog.LogError(ex, "Logout");
                MessageBox.Show($"خطأ في تسجيل الخروج: {ex.Message}", "خطأ", 
                              MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void MainWindow_Closing(object? sender, CancelEventArgs e)
        {
            try
            {
                // Save any pending changes
                _viewModel.SavePendingChanges();
                
                // Disconnect from PBX
                _pbxService?.Disconnect();
                
                // Stop timer
                _timer?.Stop();
                
                _errorLog.LogInfo("تم إغلاق النافذة الرئيسية", "MainWindow_Closing");
            }
            catch (Exception ex)
            {
                _errorLog.LogError(ex, "MainWindow_Closing");
            }
        }

        #region Event Handlers

        private void DashboardButton_Click(object sender, RoutedEventArgs e)
        {
            NavigateTo("dashboard");
        }

        private void CallsButton_Click(object sender, RoutedEventArgs e)
        {
            NavigateTo("calls");
        }

        private void UsersButton_Click(object sender, RoutedEventArgs e)
        {
            NavigateTo("users");
        }

        private void ReportsButton_Click(object sender, RoutedEventArgs e)
        {
            NavigateTo("reports");
        }

        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            NavigateTo("settings");
        }

        private void LogoutButton_Click(object sender, RoutedEventArgs e)
        {
            Logout();
        }

        #endregion
    }

    /// <summary>
    /// Simple notification window for call events
    /// </summary>
    public class NotificationWindow : Window
    {
        public NotificationWindow(string title, string message, Call call)
        {
            Title = title;
            Width = 400;
            Height = 150;
            WindowStyle = WindowStyle.ToolWindow;
            ShowInTaskbar = false;
            Topmost = true;
            
            // Position in top-right corner
            var workingArea = SystemParameters.WorkArea;
            Left = workingArea.Right - Width - 20;
            Top = workingArea.Top + 20;
            
            var grid = new Grid
            {
                Background = System.Windows.Media.Brushes.White,
                Margin = new Thickness(10)
            };
            
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(30) });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(30) });
            
            // Title
            var titleTextBlock = new TextBlock
            {
                Text = title,
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                Foreground = System.Windows.Media.Brushes.Black
            };
            Grid.SetRow(titleTextBlock, 0);
            grid.Children.Add(titleTextBlock);
            
            // Message
            var messageTextBlock = new TextBlock
            {
                Text = message,
                FontSize = 14,
                Foreground = System.Windows.Media.Brushes.DarkGray,
                TextWrapping = TextWrapping.Wrap,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetRow(messageTextBlock, 1);
            grid.Children.Add(messageTextBlock);
            
            // Call details
            var detailsTextBlock = new TextBlock
            {
                Text = $"من: {call.CallerId} | الوقت: {DateTime.Now:HH:mm:ss}",
                FontSize = 12,
                Foreground = System.Windows.Media.Brushes.Gray
            };
            Grid.SetRow(detailsTextBlock, 2);
            grid.Children.Add(detailsTextBlock);
            
            Content = grid;
        }
    }
}
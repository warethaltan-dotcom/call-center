using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunicationAssistantPro.Models;
using CommunicationAssistantPro.Services;
using CommunicationAssistantPro.Utilities;
using LiveCharts;
using LiveCharts.Wpf;
using LiveCharts.Defaults;

namespace CommunicationAssistantPro.ViewModels
{
    /// <summary>
    /// View model for main window dashboard
    /// </summary>
    public class MainWindowViewModel : INotifyPropertyChanged
    {
        private readonly DatabaseService _databaseService;
        private readonly ErrorLogService _errorLog;
        private readonly SettingsService _settingsService;
        
        private User _currentUser = App.CurrentUser!;
        private string _currentView = "Dashboard";
        private string _connectionStatus = "غير متصل";
        private DateTime _currentTime = DateTime.Now;
        private bool _isAdmin;
        private bool _shouldRefreshDashboard = true;
        
        // Statistics
        private CallStatistics _todayStatistics = new CallStatistics();
        private int _onlineUsersCount;
        
        // Data collections
        private ObservableCollection<User> _onlineUsers = new();
        private ObservableCollection<ActivityLog> _recentActivities = new();
        private SeriesCollection _callStatisticsSeries = new();
        
        public event PropertyChangedEventHandler? PropertyChanged;
        public event EventHandler<string>? NavigationRequested;
        public event EventHandler CallStatisticsUpdated;
        public event EventHandler UserStatusUpdated;

        public MainWindowViewModel(DatabaseService databaseService)
        {
            _databaseService = databaseService;
            _errorLog = new ErrorLogService();
            _settingsService = new SettingsService();
            
            // Initialize properties
            IsAdmin = CanAccessAdminFeatures();
            
            // Initialize commands
            NavigateCommand = new RelayCommand<string>(NavigateTo);
            LogoutCommand = new RelayCommand(_ => Logout());
            RefreshDataCommand = new RelayCommand(_ => RefreshData());
            ExportReportCommand = new RelayCommand<string>(ExportReport);
            
            // Initialize data
            Task.Run(() => LoadDashboardData());
        }

        #region Properties

        public User CurrentUser
        {
            get => _currentUser;
            set
            {
                _currentUser = value;
                OnPropertyChanged(nameof(CurrentUser));
                IsAdmin = CanAccessAdminFeatures();
            }
        }

        public string CurrentView
        {
            get => _currentView;
            set
            {
                _currentView = value;
                OnPropertyChanged(nameof(CurrentView));
            }
        }

        public string ConnectionStatus
        {
            get => _connectionStatus;
            set
            {
                _connectionStatus = value;
                OnPropertyChanged(nameof(ConnectionStatus));
            }
        }

        public DateTime CurrentTime
        {
            get => _currentTime;
            set
            {
                _currentTime = value;
                OnPropertyChanged(nameof(CurrentTime));
            }
        }

        public bool IsAdmin
        {
            get => _isAdmin;
            private set
            {
                _isAdmin = value;
                OnPropertyChanged(nameof(IsAdmin));
            }
        }

        public bool ShouldRefreshDashboard
        {
            get => _shouldRefreshDashboard;
            set
            {
                _shouldRefreshDashboard = value;
                OnPropertyChanged(nameof(ShouldRefreshDashboard));
            }
        }

        #endregion

        #region Statistics Properties

        public CallStatistics TodayStatistics
        {
            get => _todayStatistics;
            private set
            {
                _todayStatistics = value;
                OnPropertyChanged(nameof(TodayStatistics));
            }
        }

        public int OnlineUsersCount
        {
            get => _onlineUsersCount;
            private set
            {
                _onlineUsersCount = value;
                OnPropertyChanged(nameof(OnlineUsersCount));
            }
        }

        #endregion

        #region Data Collections

        public ObservableCollection<User> OnlineUsers
        {
            get => _onlineUsers;
            private set
            {
                _onlineUsers = value;
                OnPropertyChanged(nameof(OnlineUsers));
            }
        }

        public ObservableCollection<ActivityLog> RecentActivities
        {
            get => _recentActivities;
            private set
            {
                _recentActivities = value;
                OnPropertyChanged(nameof(RecentActivities));
            }
        }

        public SeriesCollection CallStatisticsSeries
        {
            get => _callStatisticsSeries;
            private set
            {
                _callStatisticsSeries = value;
                OnPropertyChanged(nameof(CallStatisticsSeries));
            }
        }

        #endregion

        #region Commands

        public ICommand NavigateCommand { get; }
        public ICommand LogoutCommand { get; }
        public ICommand RefreshDataCommand { get; }
        public ICommand ExportReportCommand { get; }

        #endregion

        #region Data Loading Methods

        /// <summary>
        /// Load dashboard data
        /// </summary>
        public async Task LoadDashboardData()
        {
            try
            {
                ShouldRefreshDashboard = false;
                
                await Task.Run(() =>
                {
                    // Load today's statistics
                    var today = DateTime.Today;
                    LoadTodayStatistics(today);
                    
                    // Load online users
                    LoadOnlineUsers();
                    
                    // Load recent activities
                    LoadRecentActivities();
                    
                    // Generate chart data
                    GenerateCallStatisticsChart();
                    
                    _errorLog.LogDebug("تم تحميل بيانات لوحة التحكم", "LoadDashboardData");
                });
                
                CallStatisticsUpdated?.Invoke(this, EventArgs.Empty);
                UserStatusUpdated?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                _errorLog.LogError(ex, "LoadDashboardData");
            }
            finally
            {
                ShouldRefreshDashboard = true;
            }
        }

        /// <summary>
        /// Load today's call statistics
        /// </summary>
        private void LoadTodayStatistics(DateTime date)
        {
            try
            {
                var todayStart = date.Date;
                var todayEnd = date.Date.AddDays(1);
                
                var todayCalls = _databaseService.GetUserCalls(-1, todayStart, todayEnd);
                
                TodayStatistics = new CallStatistics
                {
                    TotalCalls = todayCalls.Count,
                    IncomingCalls = todayCalls.Count(c => c.Direction == CallDirection.Incoming),
                    OutgoingCalls = todayCalls.Count(c => c.Direction == CallDirection.Outgoing),
                    MissedCalls = todayCalls.Count(c => c.Direction == CallDirection.Missed),
                    Date = todayStart.Date
                };
                
                var answeredCalls = todayCalls.Where(c => c.Status != CallStatus.Offline);
                TodayStatistics.AnsweredCalls = answeredCalls.Count();
                TodayStatistics.TotalDuration = new TimeSpan(answeredCalls.Sum(c => c.Duration?.Ticks ?? 0));
                
                if (TodayStatistics.TotalCalls > 0)
                {
                    TodayStatistics.AverageDuration = new TimeSpan(TodayStatistics.TotalDuration.Ticks / TodayStatistics.TotalCalls);
                    TodayStatistics.AnswerRate = (double)TodayStatistics.AnsweredCalls / TodayStatistics.TotalCalls * 100;
                }
                else
                {
                    TodayStatistics.AverageDuration = TimeSpan.Zero;
                    TodayStatistics.AnswerRate = 0;
                }
            }
            catch (Exception ex)
            {
                _errorLog.LogError(ex, "LoadTodayStatistics");
                TodayStatistics = new CallStatistics(); // Reset to default
            }
        }

        /// <summary>
        /// Load online users
        /// </summary>
        private void LoadOnlineUsers()
        {
            try
            {
                var users = _databaseService.GetAllUsers();
                var onlineUsers = users.Where(u => u.IsOnline).ToList();
                
                OnlineUsersCount = onlineUsers.Count;
                OnlineUsers = new ObservableCollection<User>(onlineUsers);
            }
            catch (Exception ex)
            {
                _errorLog.LogError(ex, "LoadOnlineUsers");
                OnlineUsers = new ObservableCollection<User>();
            }
        }

        /// <summary>
        /// Load recent activities
        /// </summary>
        private void LoadRecentActivities()
        {
            try
            {
                var activities = _databaseService.GetActivityLogs(null, DateTime.Now.AddHours(-24), null, 20);
                RecentActivities = new ObservableCollection<ActivityLog>(activities);
                
                // Add icons for activities
                foreach (var activity in RecentActivities)
                {
                    activity.Icon = GetActivityIcon(activity.Type);
                }
            }
            catch (Exception ex)
            {
                _errorLog.LogError(ex, "LoadRecentActivities");
                RecentActivities = new ObservableCollection<ActivityLog>();
            }
        }

        /// <summary>
        /// Generate call statistics chart data
        /// </summary>
        private void GenerateCallStatisticsChart()
        {
            try
            {
                var seriesData = new SeriesCollection();
                
                // Get data for the last 7 days
                var chartData = new ChartValues<ObservableValue>();
                for (int i = 6; i >= 0; i--)
                {
                    var date = DateTime.Now.Date.AddDays(-i);
                    var dayStart = date;
                    var dayEnd = date.AddDays(1);
                    
                    var calls = _databaseService.GetUserCalls(-1, dayStart, dayEnd);
                    chartData.Add(new ObservableValue(calls.Count));
                }
                
                seriesData.Add(new LineSeries
                {
                    Title = "المكالمات اليومية",
                    Values = chartData,
                    DataLabels = false,
                    PointGeometry = DefaultGeometries.Circle,
                    PointGeometrySize = 8
                });
                
                CallStatisticsSeries = seriesData;
            }
            catch (Exception ex)
            {
                _errorLog.LogError(ex, "GenerateCallStatisticsChart");
                CallStatisticsSeries = new SeriesCollection();
            }
        }

        /// <summary>
        /// Get icon for activity type
        /// </summary>
        private string GetActivityIcon(ActivityType type)
        {
            return type switch
            {
                ActivityType.Login => "Login",
                ActivityType.Logout => "Logout",
                ActivityType.CallStart => "Phone",
                ActivityType.CallEnd => "PhoneEnd",
                ActivityType.SettingsChange => "Settings",
                ActivityType.UserAction => "AccountEdit",
                ActivityType.System => "Information",
                ActivityType.Error => "Alert",
                _ => "Information"
            };
        }

        #endregion

        #region Navigation Methods

        /// <summary>
        /// Navigate to a specific view
        /// </summary>
        private void NavigateTo(string? viewName)
        {
            if (string.IsNullOrWhiteSpace(viewName)) return;
            
            try
            {
                CurrentView = viewName;
                NavigationRequested?.Invoke(this, viewName);
                
                _errorLog.LogInfo($"تم التنقل إلى: {viewName}", "NavigateTo");
            }
            catch (Exception ex)
            {
                _errorLog.LogError(ex, $"NavigateTo_{viewName}");
            }
        }

        #endregion

        #region Logout and Session Management

        /// <summary>
        /// Logout current user
        /// </summary>
        public void Logout()
        {
            try
            {
                if (CurrentUser != null)
                {
                    // Log logout activity
                    _databaseService.LogActivity(CurrentUser.Id, "Logout", ActivityType.Logout, "تسجيل خروج من النافذة الرئيسية");
                    
                    // End login session
                    var activeSession = _databaseService.GetActiveSession(CurrentUser.Id);
                    if (activeSession != null)
                    {
                        _databaseService.EndLoginSession(activeSession.Id);
                    }
                    
                    _errorLog.LogInfo($"تم تسجيل خروج المستخدم: {CurrentUser.FullName}", "Logout");
                }
            }
            catch (Exception ex)
            {
                _errorLog.LogError(ex, "Logout");
            }
        }

        #endregion

        #region Update Methods

        /// <summary>
        /// Update connection status
        /// </summary>
        public void UpdateConnectionStatus(bool isConnected, string message)
        {
            ConnectionStatus = isConnected ? $"متصل - {message}" : $"غير متصل - {message}";
            
            if (isConnected)
            {
                // Refresh data when connection is restored
                Task.Run(() => LoadDashboardData());
            }
        }

        /// <summary>
        /// Update current time
        /// </summary>
        public void UpdateCurrentTime()
        {
            CurrentTime = DateTime.Now;
        }

        /// <summary>
        /// Refresh all data
        /// </summary>
        public async void RefreshData()
        {
            await LoadDashboardData();
        }

        /// <summary>
        /// Save any pending changes
        /// </summary>
        public void SavePendingChanges()
        {
            try
            {
                // Save any unsaved changes
                // This method can be extended to handle specific data saving
                _errorLog.LogDebug("تم حفظ التغييرات المعلقة", "SavePendingChanges");
            }
            catch (Exception ex)
            {
                _errorLog.LogError(ex, "SavePendingChanges");
            }
        }

        /// <summary>
        /// Export report data
        /// </summary>
        private void ExportReport(string? reportType)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(reportType)) return;
                
                var exportPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "CommunicationAssistantPro",
                    "Reports"
                );
                
                if (!Directory.Exists(exportPath))
                {
                    Directory.CreateDirectory(exportPath);
                }
                
                var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                var fileName = $"{reportType}_export_{timestamp}.xlsx";
                var filePath = Path.Combine(exportPath, fileName);
                
                switch (reportType.ToLower())
                {
                    case "calls":
                        ExportCallsReport(filePath);
                        break;
                    case "users":
                        ExportUsersReport(filePath);
                        break;
                    case "activities":
                        ExportActivitiesReport(filePath);
                        break;
                    default:
                        throw new ArgumentException($"نوع التقرير غير مدعوم: {reportType}");
                }
                
                _errorLog.LogInfo($"تم تصدير التقرير: {reportType}", "ExportReport");
            }
            catch (Exception ex)
            {
                _errorLog.LogError(ex, $"ExportReport_{reportType}");
                throw;
            }
        }

        /// <summary>
        /// Export calls report
        /// </summary>
        private void ExportCallsReport(string filePath)
        {
            var calls = _databaseService.GetUserCalls(-1, DateTime.Now.AddDays(-30), DateTime.Now);
            // Implementation would use ClosedXML to create Excel file
            // For now, just create a placeholder
            File.WriteAllText(filePath.Replace(".xlsx", ".txt"), $"تقرير المكالمات - {calls.Count} مكالمة");
        }

        /// <summary>
        /// Export users report
        /// </summary>
        private void ExportUsersReport(string filePath)
        {
            var users = _databaseService.GetAllUsers();
            // Implementation would use ClosedXML to create Excel file
            File.WriteAllText(filePath.Replace(".xlsx", ".txt"), $"تقرير المستخدمين - {users.Count} مستخدم");
        }

        /// <summary>
        /// Export activities report
        /// </summary>
        private void ExportActivitiesReport(string filePath)
        {
            var activities = _databaseService.GetActivityLogs(null, DateTime.Now.AddDays(-30), null, 1000);
            // Implementation would use ClosedXML to create Excel file
            File.WriteAllText(filePath.Replace(".xlsx", ".txt"), $"تقرير النشاطات - {activities.Count} نشاط");
        }

        #endregion

        #region Permission Methods

        /// <summary>
        /// Check if user can access admin features
        /// </summary>
        private bool CanAccessAdminFeatures()
        {
            return CurrentUser?.Role == UserRole.Admin || CurrentUser?.Role == UserRole.SuperAdmin;
        }

        /// <summary>
        /// Check if user can access supervisor features
        /// </summary>
        public bool CanAccessSupervisorFeatures()
        {
            return CurrentUser?.Role >= UserRole.Supervisor;
        }

        /// <summary>
        /// Check if user can manage users
        /// </summary>
        public bool CanManageUsers()
        {
            return CurrentUser?.Role >= UserRole.Supervisor;
        }

        /// <summary>
        /// Check if user can view reports
        /// </summary>
        public bool CanViewReports()
        {
            return CurrentUser?.Role >= UserRole.Supervisor;
        }

        #endregion

        #region Statistics Methods

        /// <summary>
        /// Get detailed statistics for a specific period
        /// </summary>
        public Dictionary<string, object> GetStatistics(DateTime startDate, DateTime endDate)
        {
            try
            {
                var calls = _databaseService.GetUserCalls(-1, startDate, endDate);
                var users = _databaseService.GetAllUsers();
                
                return new Dictionary<string, object>
                {
                    ["Period"] = $"{startDate:yyyy-MM-dd} إلى {endDate:yyyy-MM-dd}",
                    ["TotalCalls"] = calls.Count,
                    ["IncomingCalls"] = calls.Count(c => c.Direction == CallDirection.Incoming),
                    ["OutgoingCalls"] = calls.Count(c => c.Direction == CallDirection.Outgoing),
                    ["MissedCalls"] = calls.Count(c => c.Direction == CallDirection.Missed),
                    ["AnsweredCalls"] = calls.Count(c => c.Status != CallStatus.Offline),
                    ["TotalUsers"] = users.Count,
                    ["ActiveUsers"] = users.Count(u => u.IsActive),
                    ["OnlineUsers"] = users.Count(u => u.IsOnline),
                    ["AverageCallDuration"] = calls.Any(c => c.Duration.HasValue) 
                        ? TimeSpan.FromTicks((long)calls.Where(c => c.Duration.HasValue).Average(c => c.Duration!.Value.Ticks))
                        : TimeSpan.Zero
                };
            }
            catch (Exception ex)
            {
                _errorLog.LogError(ex, "GetStatistics");
                return new Dictionary<string, object>();
            }
        }

        #endregion

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    /// <summary>
    /// Relay command implementation
    /// </summary>
    public class RelayCommand<T> : ICommand
    {
        private readonly Action<T?> _execute;
        private readonly Func<T?, bool>? _canExecute;

        public RelayCommand(Action<T?> execute, Func<T?, bool>? canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public event EventHandler? CanExecuteChanged;

        public bool CanExecute(T? parameter)
        {
            return _canExecute?.Invoke(parameter) ?? true;
        }

        public void Execute(T? parameter)
        {
            _execute(parameter);
        }

        public void RaiseCanExecuteChanged()
        {
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        }

        // ICommand implementation for non-generic usage
        bool ICommand.CanExecute(object? parameter)
        {
            return CanExecute((T?)parameter);
        }

        void ICommand.Execute(object? parameter)
        {
            Execute((T?)parameter);
        }
    }
}
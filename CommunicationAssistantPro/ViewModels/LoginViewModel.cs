using System;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunicationAssistantPro.Models;
using CommunicationAssistantPro.Services;
using CommunicationAssistantPro.Utilities;

namespace CommunicationAssistantPro.ViewModels
{
    /// <summary>
    /// View model for login window
    /// </summary>
    public class LoginViewModel : INotifyPropertyChanged
    {
        private readonly DatabaseService _databaseService;
        private PBXService _pbxService;
        private readonly ErrorLogService _errorLog;
        
        private string _username = string.Empty;
        private string _extension = string.Empty;
        private bool _rememberMe = false;
        private bool _isBusy = false;
        private User? _currentUser;
        
        public event EventHandler? LoginSuccessful;
        public event EventHandler<string>? LoginFailed;
        public event PropertyChangedEventHandler? PropertyChanged;

        public LoginViewModel()
        {
            _databaseService = new DatabaseService();
            _pbxService = new PBXService(null!); // Will be initialized after settings load
            _errorLog = new ErrorLogService();
            
            // Initialize commands
            LoginCommand = new RelayCommand(async _ => await LoginAsync(), _ => !IsBusy);
            OpenSettingsCommand = new RelayCommand(_ => OpenSettings(), _ => true);
        }

        public string Username
        {
            get => _username;
            set
            {
                _username = value;
                OnPropertyChanged(nameof(Username));
            }
        }

        public string Extension
        {
            get => _extension;
            set
            {
                _extension = value;
                OnPropertyChanged(nameof(Extension));
            }
        }

        public bool RememberMe
        {
            get => _rememberMe;
            set
            {
                _rememberMe = value;
                OnPropertyChanged(nameof(RememberMe));
            }
        }

        public bool IsBusy
        {
            get => _isBusy;
            set
            {
                _isBusy = value;
                OnPropertyChanged(nameof(IsBusy));
                OnPropertyChanged(nameof(IsNotBusy));
            }
        }

        public bool IsNotBusy => !IsBusy;

        public User? CurrentUser
        {
            get => _currentUser;
            private set
            {
                _currentUser = value;
                OnPropertyChanged(nameof(CurrentUser));
            }
        }

        public ICommand LoginCommand { get; }
        public ICommand OpenSettingsCommand { get; }

        /// <summary>
        /// Login with provided credentials
        /// </summary>
        public async Task LoginAsync()
        {
            try
            {
                IsBusy = true;
                
                // Validate input
                if (string.IsNullOrWhiteSpace(Username) || string.IsNullOrWhiteSpace(Password))
                {
                    throw new ArgumentException("اسم المستخدم وكلمة المرور مطلوبان");
                }

                await Task.Run(() =>
                {
                    // Authenticate user
                    var user = _databaseService.AuthenticateUser(Username, Password);
                    if (user == null)
                    {
                        throw new UnauthorizedAccessException("اسم المستخدم أو كلمة المرور غير صحيحة");
                    }

                    if (!user.IsActive)
                    {
                        throw new UnauthorizedAccessException("الحساب غير نشط، يرجى الاتصال بمدير النظام");
                    }

                    // Verify extension if provided
                    if (!string.IsNullOrWhiteSpace(Extension))
                    {
                        if (user.Extension != Extension)
                        {
                            throw new UnauthorizedAccessException("رقم الامتداد لا يتطابق مع بيانات المستخدم");
                        }
                    }

                    // Start PBX connection
                    try
                    {
                        var settingsService = new SettingsService();
                        var settings = settingsService.GetSettings();
                        
                        if (!string.IsNullOrWhiteSpace(settings.PBX_IP))
                        {
                            // Initialize PBX service with settings
                            _pbxService = new PBXService(settingsService);
                            
                            var connectionTask = _pbxService.ConnectAsync();
                            if (!connectionTask.Result)
                            {
                                _errorLog.LogWarning("فشل في الاتصال بـ PBX، سيتم المتابعة بدون اتصال", "LoginAsync");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _errorLog.LogWarning($"خطأ في الاتصال بـ PBX: {ex.Message}", "LoginAsync");
                    }

                    // Create login session
                    var sessionId = _databaseService.StartLoginSession(user.Id);
                    
                    // Log successful login
                    _databaseService.LogActivity(user.Id, "Login", ActivityType.Login, $"تسجيل دخول ناجح - الامتداد: {user.Extension}");

                    // Set current user
                    CurrentUser = user;

                    _errorLog.LogInfo($"تم تسجيل دخول المستخدم: {user.FullName} (الامتداد: {user.Extension})", "LoginAsync");
                });

                // Trigger success event on UI thread
                LoginSuccessful?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                _errorLog.LogError(ex, $"فشل تسجيل الدخول للمستخدم: {Username}", null);
                LoginFailed?.Invoke(this, ex.Message);
            }
            finally
            {
                IsBusy = false;
            }
        }

        /// <summary>
        /// Legacy synchronous login method (for backward compatibility)
        /// </summary>
        public void Login(string username, string password, string extension = "")
        {
            Username = username;
            Extension = extension;
            
            // Store password temporarily for validation
            Password = password;
            
            // Start async login
            _ = LoginAsync();
        }

        /// <summary>
        /// Open settings window
        /// </summary>
        private void OpenSettings()
        {
            try
            {
                var settingsWindow = new Views.SettingsWindow();
                settingsWindow.ShowDialog();
            }
            catch (Exception ex)
            {
                _errorLog.LogError(ex, "OpenSettings");
                LoginFailed?.Invoke(this, $"خطأ في فتح الإعدادات: {ex.Message}");
            }
        }

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
                    _databaseService.LogActivity(CurrentUser.Id, "Logout", ActivityType.Logout, "تسجيل خروج");

                    // End PBX connection
                    _pbxService?.Disconnect();

                    // End login session
                    var activeSession = _databaseService.GetActiveSession(CurrentUser.Id);
                    if (activeSession != null)
                    {
                        _databaseService.EndLoginSession(activeSession.Id);
                    }

                    _errorLog.LogInfo($"تم تسجيل خروج المستخدم: {CurrentUser.FullName}", "Logout");

                    // Clear current user
                    CurrentUser = null;
                }
            }
            catch (Exception ex)
            {
                _errorLog.LogError(ex, "Logout");
            }
        }

        /// <summary>
        /// Check if user can access admin features
        /// </summary>
        public bool CanAccessAdminFeatures()
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

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        // Private property for password (not bound to UI)
        private string Password { get; set; } = string.Empty;
    }

    /// <summary>
    /// Relay command implementation
    /// </summary>
    public class RelayCommand : ICommand
    {
        private readonly Action<object?> _execute;
        private readonly Func<object?, bool>? _canExecute;

        public RelayCommand(Action<object?> execute, Func<object?, bool>? canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public event EventHandler? CanExecuteChanged;

        public bool CanExecute(object? parameter)
        {
            return _canExecute?.Invoke(parameter) ?? true;
        }

        public void Execute(object? parameter)
        {
            _execute(parameter);
        }

        public void RaiseCanExecuteChanged()
        {
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
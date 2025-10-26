using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using CommunicationAssistantPro.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.Sqlite;

namespace CommunicationAssistantPro.Services
{
    /// <summary>
    /// Database service for managing all data operations
    /// </summary>
    public class DatabaseService : IDisposable
    {
        private readonly CommunicationDbContext _context;
        private readonly string _databasePath;

        public DatabaseService()
        {
            // Set database path in user's local application data
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var databaseDirectory = Path.Combine(appDataPath, "CommunicationAssistantPro", "Database");
            
            if (!Directory.Exists(databaseDirectory))
            {
                Directory.CreateDirectory(databaseDirectory);
            }

            _databasePath = Path.Combine(databaseDirectory, "CommunicationAssistant.db");
            
            // Initialize Entity Framework context
            var options = new DbContextOptionsBuilder<CommunicationDbContext>()
                .UseSqlite($"Data Source={_databasePath}")
                .Options;

            _context = new CommunicationDbContext(options);
        }

        /// <summary>
        /// Initialize the database with all required tables
        /// </summary>
        public void InitializeDatabase()
        {
            try
            {
                // Ensure database is created
                _context.Database.EnsureCreated();

                // Create initial data if needed
                CreateInitialData();
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"خطأ في إنشاء قاعدة البيانات: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Create initial data for the system
        /// </summary>
        private void CreateInitialData()
        {
            try
            {
                // Check if admin user exists
                if (!_context.Users.Any())
                {
                    // Create default admin user
                    var adminUser = new User
                    {
                        Username = "admin",
                        FullName = "مدير النظام",
                        Email = "admin@basradelight.com",
                        Department = "تقنية المعلومات",
                        Role = UserRole.SuperAdmin,
                        PasswordHash = BCrypt.Net.BCrypt.HashPassword("admin123"),
                        Extension = "1000",
                        IsActive = true,
                        CreatedAt = DateTime.Now
                    };

                    _context.Users.Add(adminUser);
                }

                // Check if settings exist
                if (!_context.AppSettings.Any())
                {
                    var defaultSettings = new AppSettings
                    {
                        PBX_IP = "192.168.1.100",
                        PBX_Port = 5038,
                        PBX_Username = "admin",
                        DatabasePath = _databasePath,
                        LastSettingsUpdate = DateTime.Now
                    };

                    _context.AppSettings.Add(defaultSettings);
                }

                _context.SaveChanges();
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"خطأ في إنشاء البيانات الأولية: {ex.Message}", ex);
            }
        }

        #region User Management

        /// <summary>
        /// Get user by username
        /// </summary>
        public User? GetUserByUsername(string username)
        {
            return _context.Users.FirstOrDefault(u => u.Username == username && u.IsActive);
        }

        /// <summary>
        /// Get user by extension
        /// </summary>
        public User? GetUserByExtension(string extension)
        {
            return _context.Users.FirstOrDefault(u => u.Extension == extension && u.IsActive);
        }

        /// <summary>
        /// Authenticate user
        /// </summary>
        public User? AuthenticateUser(string username, string password)
        {
            var user = GetUserByUsername(username);
            if (user != null && BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
            {
                return user;
            }
            return null;
        }

        /// <summary>
        /// Get all active users
        /// </summary>
        public List<User> GetAllUsers()
        {
            return _context.Users.Where(u => u.IsActive).ToList();
        }

        /// <summary>
        /// Add new user
        /// </summary>
        public void AddUser(User user)
        {
            user.CreatedAt = DateTime.Now;
            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(user.PasswordHash ?? "default123");
            _context.Users.Add(user);
            _context.SaveChanges();
        }

        /// <summary>
        /// Update user
        /// </summary>
        public void UpdateUser(User user)
        {
            _context.Entry(user).State = EntityState.Modified;
            _context.SaveChanges();
        }

        /// <summary>
        /// Delete user (soft delete)
        /// </summary>
        public void DeleteUser(int userId)
        {
            var user = _context.Users.Find(userId);
            if (user != null)
            {
                user.IsActive = false;
                _context.SaveChanges();
            }
        }

        #endregion

        #region Call Management

        /// <summary>
        /// Add new call record
        /// </summary>
        public void AddCall(Call call)
        {
            call.CreatedAt = DateTime.Now;
            _context.Calls.Add(call);
            _context.SaveChanges();
        }

        /// <summary>
        /// Update call record
        /// </summary>
        public void UpdateCall(Call call)
        {
            call.UpdatedAt = DateTime.Now;
            _context.Entry(call).State = EntityState.Modified;
            _context.SaveChanges();
        }

        /// <summary>
        /// Get calls by user
        /// </summary>
        public List<Call> GetUserCalls(int userId, DateTime? startDate = null, DateTime? endDate = null)
        {
            var query = _context.Calls.Where(c => c.UserId == userId);

            if (startDate.HasValue)
                query = query.Where(c => c.StartTime >= startDate.Value);

            if (endDate.HasValue)
                query = query.Where(c => c.StartTime <= endDate.Value);

            return query.OrderByDescending(c => c.StartTime).ToList();
        }

        /// <summary>
        /// Get call statistics for a user
        /// </summary>
        public CallStatistics GetUserCallStatistics(int userId, DateTime startDate, DateTime endDate)
        {
            var calls = _context.Calls
                .Where(c => c.UserId == userId && c.StartTime >= startDate && c.StartTime <= endDate)
                .ToList();

            var stats = new CallStatistics
            {
                TotalCalls = calls.Count,
                IncomingCalls = calls.Count(c => c.Direction == CallDirection.Incoming),
                OutgoingCalls = calls.Count(c => c.Direction == CallDirection.Outgoing),
                MissedCalls = calls.Count(c => c.Direction == CallDirection.Missed),
                Date = startDate.Date
            };

            var answeredCalls = calls.Where(c => c.Status != CallStatus.Offline);
            stats.AnsweredCalls = answeredCalls.Count();
            stats.TotalDuration = new TimeSpan(answeredCalls.Sum(c => c.Duration?.Ticks ?? 0));
            stats.AverageDuration = stats.TotalCalls > 0 
                ? new TimeSpan(stats.TotalDuration.Ticks / stats.TotalCalls) 
                : TimeSpan.Zero;
            stats.AnswerRate = stats.TotalCalls > 0 ? (double)stats.AnsweredCalls / stats.TotalCalls * 100 : 0;

            return stats;
        }

        /// <summary>
        /// Get call statistics for all users
        /// </summary>
        public Dictionary<int, CallStatistics> GetAllUsersStatistics(DateTime startDate, DateTime endDate)
        {
            var result = new Dictionary<int, CallStatistics>();
            var users = GetAllUsers();

            foreach (var user in users)
            {
                result[user.Id] = GetUserCallStatistics(user.Id, startDate, endDate);
            }

            return result;
        }

        #endregion

        #region Session Management

        /// <summary>
        /// Start new login session
        /// </summary>
        public int StartLoginSession(int userId, string? ipAddress = null, string? macAddress = null)
        {
            var session = new LoginSession
            {
                UserId = userId,
                LoginTime = DateTime.Now,
                IpAddress = ipAddress,
                MacAddress = macAddress,
                IsActive = true
            };

            _context.LoginSessions.Add(session);
            _context.SaveChanges();

            // Update user status
            var user = _context.Users.Find(userId);
            if (user != null)
            {
                user.IsOnline = true;
                user.LastLogin = DateTime.Now;
                user.LastSeen = DateTime.Now;
                user.Status = CallStatus.Idle;
                _context.SaveChanges();
            }

            return session.Id;
        }

        /// <summary>
        /// End login session
        /// </summary>
        public void EndLoginSession(int sessionId)
        {
            var session = _context.LoginSessions.Find(sessionId);
            if (session != null && session.IsActive)
            {
                session.LogoutTime = DateTime.Now;
                session.IsActive = false;
                _context.SaveChanges();

                // Update user status
                var user = _context.Users.Find(session.UserId);
                if (user != null)
                {
                    user.IsOnline = false;
                    user.Status = CallStatus.Offline;
                    _context.SaveChanges();
                }
            }
        }

        /// <summary>
        /// Get active login session for user
        /// </summary>
        public LoginSession? GetActiveSession(int userId)
        {
            return _context.LoginSessions.FirstOrDefault(s => s.UserId == userId && s.IsActive);
        }

        #endregion

        #region Settings Management

        /// <summary>
        /// Get application settings
        /// </summary>
        public AppSettings GetSettings()
        {
            var settings = _context.AppSettings.FirstOrDefault();
            if (settings == null)
            {
                settings = new AppSettings { DatabasePath = _databasePath };
                _context.AppSettings.Add(settings);
                _context.SaveChanges();
            }
            return settings;
        }

        /// <summary>
        /// Update application settings
        /// </summary>
        public void UpdateSettings(AppSettings settings)
        {
            settings.LastSettingsUpdate = DateTime.Now;
            settings.SettingsVersion++;
            
            _context.Entry(settings).State = EntityState.Modified;
            _context.SaveChanges();
        }

        /// <summary>
        /// Get user preferences
        /// </summary>
        public UserPreferences? GetUserPreferences(int userId)
        {
            return _context.UserPreferences.FirstOrDefault(p => p.UserId == userId);
        }

        /// <summary>
        /// Update user preferences
        /// </summary>
        public void UpdateUserPreferences(UserPreferences preferences)
        {
            preferences.UpdatedAt = DateTime.Now;
            _context.Entry(preferences).State = EntityState.Modified;
            _context.SaveChanges();
        }

        #endregion

        #region Activity Logging

        /// <summary>
        /// Log user activity
        /// </summary>
        public void LogActivity(int? userId, string action, ActivityType type, string? description = null, string? ipAddress = null)
        {
            var log = new ActivityLog
            {
                UserId = userId,
                Action = action,
                Type = type,
                Description = description,
                IpAddress = ipAddress,
                Timestamp = DateTime.Now
            };

            _context.ActivityLogs.Add(log);
            _context.SaveChanges();
        }

        /// <summary>
        /// Get activity logs
        /// </summary>
        public List<ActivityLog> GetActivityLogs(int? userId = null, DateTime? startDate = null, DateTime? endDate = null, int count = 100)
        {
            var query = _context.ActivityLogs.AsQueryable();

            if (userId.HasValue)
                query = query.Where(l => l.UserId == userId.Value);

            if (startDate.HasValue)
                query = query.Where(l => l.Timestamp >= startDate.Value);

            if (endDate.HasValue)
                query = query.Where(l => l.Timestamp <= endDate.Value);

            return query.OrderByDescending(l => l.Timestamp).Take(count).ToList();
        }

        #endregion

        #region Database Maintenance

        /// <summary>
        /// Backup database
        /// </summary>
        public string BackupDatabase(string? backupPath = null)
        {
            if (string.IsNullOrEmpty(backupPath))
            {
                var backupDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), 
                    "CommunicationAssistantPro", "Backups");
                if (!Directory.Exists(backupDir))
                    Directory.CreateDirectory(backupDir);
                
                backupPath = Path.Combine(backupDir, $"backup_{DateTime.Now:yyyyMMdd_HHmmss}.db");
            }

            File.Copy(_databasePath, backupPath, true);
            return backupPath;
        }

        /// <summary>
        /// Clean old data
        /// </summary>
        public void CleanOldData(int retentionDays = 90)
        {
            var cutoffDate = DateTime.Now.AddDays(-retentionDays);

            // Clean old calls
            _context.Calls.RemoveRange(_context.Calls.Where(c => c.CreatedAt < cutoffDate));

            // Clean old activity logs
            _context.ActivityLogs.RemoveRange(_context.ActivityLogs.Where(l => l.Timestamp < cutoffDate));

            // Clean old inactive sessions
            _context.LoginSessions.RemoveRange(_context.LoginSessions
                .Where(s => !s.IsActive && s.LogoutTime < cutoffDate));

            _context.SaveChanges();
        }

        /// <summary>
        /// Get database statistics
        /// </summary>
        public Dictionary<string, object> GetDatabaseStats()
        {
            return new Dictionary<string, object>
            {
                ["TotalUsers"] = _context.Users.Count(u => u.IsActive),
                ["TotalCalls"] = _context.Calls.Count(),
                ["ActiveSessions"] = _context.LoginSessions.Count(s => s.IsActive),
                ["TotalActivityLogs"] = _context.ActivityLogs.Count(),
                ["DatabaseSizeMB"] = new FileInfo(_databasePath).Length / (1024 * 1024),
                ["DatabasePath"] = _databasePath
            };
        }

        #endregion

        public void Dispose()
        {
            _context?.Dispose();
        }
    }

    /// <summary>
    /// Entity Framework DbContext
    /// </summary>
    public class CommunicationDbContext : DbContext
    {
        public CommunicationDbContext(DbContextOptions<CommunicationDbContext> options) : base(options)
        {
        }

        public DbSet<User> Users { get; set; }
        public DbSet<Call> Calls { get; set; }
        public DbSet<LoginSession> LoginSessions { get; set; }
        public DbSet<AppSettings> AppSettings { get; set; }
        public DbSet<UserPreferences> UserPreferences { get; set; }
        public DbSet<ActivityLog> ActivityLogs { get; set; }
        public DbSet<SettingsBackup> SettingsBackups { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configure relationships
            modelBuilder.Entity<LoginSession>()
                .HasOne<User>(s => s.User)
                .WithMany(u => u.LoginSessions)
                .HasForeignKey(s => s.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Call>()
                .HasOne<User>(c => c.User)
                .WithMany(u => u.Calls)
                .HasForeignKey(c => c.UserId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<UserPreferences>()
                .HasOne<User>(p => p.User)
                .WithMany(u => u.Preferences)
                .HasForeignKey(p => p.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<ActivityLog>()
                .HasOne<User>(l => l.User)
                .WithMany(u => u.ActivityLogs)
                .HasForeignKey(l => l.UserId)
                .OnDelete(DeleteBehavior.SetNull);

            // Configure indexes
            modelBuilder.Entity<User>()
                .HasIndex(u => u.Username)
                .IsUnique();

            modelBuilder.Entity<User>()
                .HasIndex(u => u.Extension)
                .IsUnique();

            modelBuilder.Entity<Call>()
                .HasIndex(c => c.StartTime);

            modelBuilder.Entity<ActivityLog>()
                .HasIndex(l => l.Timestamp);
        }
    }
}
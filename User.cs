using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace CommunicationAssistantPro.Models
{
    /// <summary>
    /// User model representing an employee/user in the system
    /// </summary>
    public class User
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(50)]
        public string Username { get; set; } = string.Empty;

        [StringLength(20)]
        public string EmployeeNumber { get; set; } = string.Empty;

        [StringLength(20)]
        public string Extension { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        public string FullName { get; set; } = string.Empty;

        [StringLength(100)]
        public string Email { get; set; } = string.Empty;

        [StringLength(100)]
        public string Department { get; set; } = string.Empty;

        public bool IsActive { get; set; } = true;

        public UserRole Role { get; set; } = UserRole.Employee;

        [StringLength(255)]
        public string PasswordHash { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public DateTime LastLogin { get; set; }

        public bool IsOnline { get; set; } = false;

        public DateTime? LastSeen { get; set; }

        public CallStatus Status { get; set; } = CallStatus.Idle;

        // Navigation properties for Entity Framework
        public virtual ICollection<LoginSession> LoginSessions { get; set; } = new List<LoginSession>();
        public virtual ICollection<Call> Calls { get; set; } = new List<Call>();
        public virtual ICollection<UserPreferences> Preferences { get; set; } = new List<UserPreferences>();
        public virtual ICollection<ActivityLog> ActivityLogs { get; set; } = new List<ActivityLog>();
    }

    /// <summary>
    /// User roles in the system
    /// </summary>
    public enum UserRole
    {
        Employee = 1,
        Supervisor = 2,
        Admin = 3,
        SuperAdmin = 4
    }

    /// <summary>
    /// Call status types
    /// </summary>
    public enum CallStatus
    {
        Idle = 1,
        Busy = 2,
        Ringing = 3,
        OnCall = 4,
        Offline = 5
    }

    /// <summary>
    /// Login session tracking
    /// </summary>
    public class LoginSession
    {
        [Key]
        public int Id { get; set; }

        public int UserId { get; set; }

        public DateTime LoginTime { get; set; }

        public DateTime? LogoutTime { get; set; }

        public string? IpAddress { get; set; }

        public string? MacAddress { get; set; }

        public bool IsActive { get; set; } = true;

        // Navigation property
        public User User { get; set; } = null!;
    }
}
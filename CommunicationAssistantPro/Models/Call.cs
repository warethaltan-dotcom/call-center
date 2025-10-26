using System;
using System.ComponentModel.DataAnnotations;

namespace CommunicationAssistantPro.Models
{
    /// <summary>
    /// Call model for tracking all call activities
    /// </summary>
    public class Call
    {
        [Key]
        public int Id { get; set; }

        public int? UserId { get; set; } // Can be null for external calls

        [Required]
        [StringLength(50)]
        public string CallerId { get; set; } = string.Empty;

        [StringLength(50)]
        public string? CalledId { get; set; }

        [StringLength(20)]
        public string? Extension { get; set; }

        public CallType Type { get; set; }

        public CallDirection Direction { get; set; }

        public CallStatus Status { get; set; }

        public DateTime StartTime { get; set; }

        public DateTime? EndTime { get; set; }

        public TimeSpan? Duration { get; set; }

        public string? Notes { get; set; }

        public string? CallFilePath { get; set; } // Path to temporary call status file

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public DateTime? UpdatedAt { get; set; }

        // Navigation property
        public User? User { get; set; }
    }

    /// <summary>
    /// Types of calls in the system
    /// </summary>
    public enum CallType
    {
        Voice = 1,
        Video = 2,
        Conference = 3,
        Transfer = 4
    }

    /// <summary>
    /// Call direction
    /// </summary>
    public enum CallDirection
    {
        Incoming = 1,
        Outgoing = 2,
        Internal = 3,
        Missed = 4
    }

    /// <summary>
    /// Call statistics for reporting
    /// </summary>
    public class CallStatistics
    {
        public int TotalCalls { get; set; }
        public int IncomingCalls { get; set; }
        public int OutgoingCalls { get; set; }
        public int MissedCalls { get; set; }
        public int AnsweredCalls { get; set; }
        public TimeSpan TotalDuration { get; set; }
        public TimeSpan AverageDuration { get; set; }
        public double AnswerRate { get; set; }
        public TimeSpan PeakHourStart { get; set; }
        public TimeSpan PeakHourEnd { get; set; }
        public DateTime Date { get; set; }
    }

    /// <summary>
    /// Real-time call status for CaCallstatus.dat file
    /// </summary>
    public class CallStatusFile
    {
        public string CallerId { get; set; } = string.Empty;
        public string Extension { get; set; } = string.Empty;
        public CallStatus Status { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.Now;
        public string? CallId { get; set; }
        public TimeSpan Duration { get; set; }
    }

    /// <summary>
    /// System activity log
    /// </summary>
    public class ActivityLog
    {
        [Key]
        public int Id { get; set; }

        public int? UserId { get; set; }

        [Required]
        [StringLength(100)]
        public string Action { get; set; } = string.Empty;

        [StringLength(255)]
        public string? Description { get; set; }

        [StringLength(50)]
        public string? IpAddress { get; set; }

        public DateTime Timestamp { get; set; } = DateTime.Now;

        public ActivityType Type { get; set; }

        // UI properties (not stored in database)
        public string Icon { get; set; } = "Information";

        // Navigation property
        public User? User { get; set; }
    }

    /// <summary>
    /// Activity types for logging
    /// </summary>
    public enum ActivityType
    {
        Login = 1,
        Logout = 2,
        CallStart = 3,
        CallEnd = 4,
        SettingsChange = 5,
        UserAction = 6,
        System = 7,
        Error = 8
    }
}
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CommunicationAssistantPro.Models;
using CommunicationAssistantPro.Utilities;

namespace CommunicationAssistantPro.Services
{
    /// <summary>
    /// PBX service for Grandstream integration
    /// Supports both AMI (Asterisk Manager Interface) and HTTP API
    /// </summary>
    public class PBXService : IDisposable
    {
        private readonly SettingsService _settingsService;
        private readonly DatabaseService _databaseService;
        private readonly ErrorLogService _errorLog;
        
        private TcpClient? _amiClient;
        private NetworkStream? _amiStream;
        private CancellationTokenSource? _cancellationTokenSource;
        private bool _isConnected;
        private string _lastEventData = string.Empty;

        // Event handlers
        public event EventHandler<CallEventArgs>? CallReceived;
        public event EventHandler<CallEventArgs>? CallAnswered;
        public event EventHandler<CallEventArgs>? CallEnded;
        public event EventHandler<UserStatusEventArgs>? UserStatusChanged;
        public event EventHandler<PBXEventArgs>? ConnectionStatusChanged;

        public PBXService(SettingsService settingsService)
        {
            _settingsService = settingsService;
            _databaseService = new DatabaseService();
            _errorLog = new ErrorLogService();
        }

        /// <summary>
        /// Connect to PBX using configured method (AMI or HTTP)
        /// </summary>
        public async Task<bool> ConnectAsync()
        {
            try
            {
                var settings = _settingsService.GetSettings();
                
                if (settings.PBX_UseAMI)
                {
                    return await ConnectAMIAsync(settings);
                }
                else
                {
                    return await ConnectHTTPAsync(settings);
                }
            }
            catch (Exception ex)
            {
                _errorLog.LogError(ex);
                ConnectionStatusChanged?.Invoke(this, new PBXEventArgs(false, ex.Message));
                return false;
            }
        }

        /// <summary>
        /// Connect using AMI (Asterisk Manager Interface)
        /// </summary>
        private async Task<bool> ConnectAMIAsync(AppSettings settings)
        {
            try
            {
                _amiClient = new TcpClient();
                await _amiClient.ConnectAsync(settings.PBX_IP, settings.PBX_Port);
                
                _amiStream = _amiClient.GetStream();
                _cancellationTokenSource = new CancellationTokenSource();

                // Send login command
                var loginCommand = $"Action: Login\r\nUsername: {settings.PBX_Username}\r\nSecret: {settings.PBX_Password}\r\nEvents: on\r\n\r\n";
                await SendAMIMessage(loginCommand);

                // Start listening for events
                _ = Task.Run(() => ListenForAMIEventAsync(_cancellationTokenSource.Token), _cancellationTokenSource.Token);

                _isConnected = true;
                ConnectionStatusChanged?.Invoke(this, new PBXEventArgs(true, "تم الاتصال بنجاح"));
                
                _databaseService.LogActivity(null, "PBX Connection", ActivityType.System, "تم الاتصال بـ PBX بنجاح");

                return true;
            }
            catch (Exception ex)
            {
                _isConnected = false;
                _errorLog.LogError(ex);
                ConnectionStatusChanged?.Invoke(this, new PBXEventArgs(false, ex.Message));
                return false;
            }
        }

        /// <summary>
        /// Connect using HTTP API
        /// </summary>
        private async Task<bool> ConnectHTTPAsync(AppSettings settings)
        {
            try
            {
                // Test HTTP connection
                using var httpClient = new HttpClient();
                httpClient.Timeout = TimeSpan.FromSeconds(30);
                
                var testUrl = $"http://{settings.PBX_IP}:{settings.PBX_Port}/api/call_status";
                var response = await httpClient.GetAsync(testUrl);

                if (response.IsSuccessStatusCode)
                {
                    _isConnected = true;
                    ConnectionStatusChanged?.Invoke(this, new PBXEventArgs(true, "تم الاتصال بـ HTTP API بنجاح"));
                    
                    // Start monitoring calls via HTTP polling
                    _ = Task.Run(() => MonitorCallsViaHTTPAsync(_cancellationTokenSource.Token), _cancellationTokenSource.Token);
                    
                    _databaseService.LogActivity(null, "PBX Connection", ActivityType.System, "تم الاتصال بـ HTTP API بنجاح");
                    return true;
                }
                else
                {
                    throw new Exception($"HTTP API غير متاح: {response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                _isConnected = false;
                _errorLog.LogError(ex);
                ConnectionStatusChanged?.Invoke(this, new PBXEventArgs(false, ex.Message));
                return false;
            }
        }

        /// <summary>
        /// Send AMI message
        /// </summary>
        private async Task SendAMIMessage(string message)
        {
            if (_amiStream == null) throw new InvalidOperationException("AMI غير متصل");

            var data = Encoding.ASCII.GetBytes(message);
            await _amiStream.WriteAsync(data, 0, data.Length);
            await _amiStream.FlushAsync();
        }

        /// <summary>
        /// Listen for AMI events
        /// </summary>
        private async Task ListenForAMIEventAsync(CancellationToken cancellationToken)
        {
            var buffer = new byte[4096];
            var messageBuilder = new StringBuilder();

            try
            {
                while (!cancellationToken.IsCancellationRequested && _amiStream != null)
                {
                    var bytesRead = await _amiStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken);
                    if (bytesRead == 0) break;

                    var chunk = Encoding.ASCII.GetString(buffer, 0, bytesRead);
                    messageBuilder.Append(chunk);

                    // Process complete messages (ending with double CRLF)
                    var messages = messageBuilder.ToString().Split("\r\n\r\n");
                    if (messages.Length > 1)
                    {
                        for (int i = 0; i < messages.Length - 1; i++)
                        {
                            ProcessAMIEvent(messages[i]);
                        }
                        messageBuilder.Clear();
                        messageBuilder.Append(messages[^1]);
                    }
                }
            }
            catch (Exception ex)
            {
                _errorLog.LogError(ex);
                _isConnected = false;
                ConnectionStatusChanged?.Invoke(this, new PBXEventArgs(false, ex.Message));
            }
        }

        /// <summary>
        /// Process AMI event
        /// </summary>
        private void ProcessAMIEvent(string eventData)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(eventData)) return;

                var lines = eventData.Split('\r', '\n');
                var eventType = string.Empty;
                var eventDetails = new Dictionary<string, string>();

                foreach (var line in lines)
                {
                    if (line.StartsWith("Event:"))
                    {
                        eventType = line.Substring("Event:".Length).Trim();
                    }
                    else if (line.Contains(":"))
                    {
                        var parts = line.Split(':', 2);
                        if (parts.Length == 2)
                        {
                            eventDetails[parts[0].Trim()] = parts[1].Trim();
                        }
                    }
                }

                // Process different event types
                switch (eventType.ToLower())
                {
                    case "newexten":
                        ProcessNewExtenEvent(eventDetails);
                        break;
                    case "hangup":
                        ProcessHangupEvent(eventDetails);
                        break;
                    case "bridge":
                        ProcessBridgeEvent(eventDetails);
                        break;
                    case "link":
                        ProcessLinkEvent(eventDetails);
                        break;
                }
            }
            catch (Exception ex)
            {
                _errorLog.LogError(ex);
            }
        }

        /// <summary>
        /// Process new extension event
        /// </summary>
        private void ProcessNewExtenEvent(Dictionary<string, string> details)
        {
            try
            {
                if (details.TryGetValue("Exten", out var extension) && 
                    details.TryGetValue("Context", out var context))
                {
                    var call = new Call
                    {
                        Extension = extension,
                        CallerId = details.GetValueOrDefault("CallerID", "Unknown"),
                        StartTime = DateTime.Now,
                        Status = CallStatus.Ringing,
                        Direction = context.Contains("from-pstn") ? CallDirection.Incoming : CallDirection.Outgoing
                    };

                    // Find user by extension
                    var user = _databaseService.GetUserByExtension(extension);
                    if (user != null)
                    {
                        call.UserId = user.Id;
                        user.Status = CallStatus.Ringing;
                        _databaseService.UpdateUser(user);
                    }

                    _databaseService.AddCall(call);
                    CallReceived?.Invoke(this, new CallEventArgs(call));

                    // Update call status file if enabled
                    UpdateCallStatusFile(call);
                }
            }
            catch (Exception ex)
            {
                _errorLog.LogError(ex);
            }
        }

        /// <summary>
        /// Process hangup event
        /// </summary>
        private void ProcessHangupEvent(Dictionary<string, string> details)
        {
            try
            {
                if (details.TryGetValue("Channel", out var channel) && 
                    channel.Contains("-"))
                {
                    var extension = channel.Split('-')[0];
                    
                    // Find call and update it
                    var calls = _databaseService.GetUserCalls(-1, DateTime.Now.AddHours(-1), DateTime.Now);
                    var call = calls.Find(c => c.Extension == extension && c.Status != CallStatus.Offline);
                    
                    if (call != null)
                    {
                        call.EndTime = DateTime.Now;
                        call.Status = CallStatus.Offline;
                        call.Duration = call.EndTime.Value.Subtract(call.StartTime);
                        _databaseService.UpdateCall(call);

                        // Update user status
                        if (call.UserId.HasValue)
                        {
                            var user = _databaseService.GetUserByUsername(""); // Get user by extension method needed
                            if (user != null)
                            {
                                user.Status = CallStatus.Idle;
                                _databaseService.UpdateUser(user);
                            }
                        }

                        CallEnded?.Invoke(this, new CallEventArgs(call));
                    }
                }
            }
            catch (Exception ex)
            {
                _errorLog.LogError(ex);
            }
        }

        /// <summary>
        /// Process bridge event
        /// </summary>
        private void ProcessBridgeEvent(Dictionary<string, string> details)
        {
            // Handle call bridging (call answered)
            try
            {
                string? channel = null;
                if (details.TryGetValue("Channel1", out var channel1Value)) 
                    channel = channel1Value;
                else if (details.TryGetValue("Channel", out var channel2Value))
                    channel = channel2Value;
                
                if (!string.IsNullOrEmpty(channel))
                {
                    var extension = channel.Split('-')[0];
                    
                    var calls = _databaseService.GetUserCalls(-1, DateTime.Now.AddHours(-1), DateTime.Now);
                    var call = calls.Find(c => c.Extension == extension && c.Status == CallStatus.Ringing);
                    
                    if (call != null)
                    {
                        call.Status = CallStatus.OnCall;
                        _databaseService.UpdateCall(call);

                        // Update user status
                        if (call.UserId.HasValue)
                        {
                            var user = _databaseService.GetUserByExtension(extension);
                            if (user != null)
                            {
                                user.Status = CallStatus.Busy;
                                _databaseService.UpdateUser(user);
                            }
                        }

                        CallAnswered?.Invoke(this, new CallEventArgs(call));
                    }
                }
            }
            catch (Exception ex)
            {
                _errorLog.LogError(ex);
            }
        }

        /// <summary>
        /// Process link event
        /// </summary>
        private void ProcessLinkEvent(Dictionary<string, string> details)
        {
            // Similar to bridge event
            ProcessBridgeEvent(details);
        }

        /// <summary>
        /// Monitor calls via HTTP API polling
        /// </summary>
        private async Task MonitorCallsViaHTTPAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var settings = _settingsService.GetSettings();
                    using var httpClient = new HttpClient();
                    
                    var response = await httpClient.GetAsync($"http://{settings.PBX_IP}:{settings.PBX_Port}/api/calls");
                    if (response.IsSuccessStatusCode)
                    {
                        var json = await response.Content.ReadAsStringAsync();
                        ProcessHTTPCallsData(json);
                    }
                    
                    await Task.Delay(5000, cancellationToken); // Poll every 5 seconds
                }
                catch (Exception ex)
                {
                    _errorLog.LogError(ex);
                    await Task.Delay(30000, cancellationToken); // Wait 30 seconds before retry
                }
            }
        }

        /// <summary>
        /// Process HTTP calls data
        /// </summary>
        private void ProcessHTTPCallsData(string json)
        {
            try
            {
                // Parse JSON response from Grandstream HTTP API
                // This is a simplified example - actual implementation depends on API structure
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };

                // var callsData = JsonSerializer.Deserialize<HttpCallsResponse>(json, options);
                // Process each call in the response
                
                // For now, create a placeholder implementation
                _lastEventData = json;
            }
            catch (Exception ex)
            {
                _errorLog.LogError(ex);
            }
        }

        /// <summary>
        /// Update call status file (CaCallstatus.dat)
        /// </summary>
        private void UpdateCallStatusFile(Call call)
        {
            try
            {
                var settings = _settingsService.GetSettings();
                if (!settings.EnableCallStatusFile) return;

                var callStatus = new CallStatusFile
                {
                    CallerId = call.CallerId,
                    Extension = call.Extension ?? string.Empty,
                    Status = call.Status,
                    Timestamp = DateTime.Now,
                    CallId = call.Id.ToString(),
                    Duration = TimeSpan.Zero
                };

                var json = JsonSerializer.Serialize(callStatus);
                File.WriteAllText(settings.CallStatusFilePath, json);

                // Schedule file cleanup
                _ = Task.Run(async () =>
                {
                    await Task.Delay(settings.CallStatusFileTimeout * 1000);
                    try
                    {
                        if (File.Exists(settings.CallStatusFilePath))
                        {
                            File.Delete(settings.CallStatusFilePath);
                        }
                    }
                    catch (Exception ex)
                    {
                        _errorLog.LogError(ex);
                    }
                });
            }
            catch (Exception ex)
            {
                _errorLog.LogError(ex);
            }
        }

        /// <summary>
        /// Get PBX status information
        /// </summary>
        public PBXStatusInfo GetPBXStatus()
        {
            return new PBXStatusInfo
            {
                IsConnected = _isConnected,
                ConnectionMethod = _settingsService.GetSettings().PBX_UseAMI ? "AMI" : "HTTP",
                LastUpdate = DateTime.Now,
                EventCount = _lastEventData.Length
            };
        }

        /// <summary>
        /// Make a call through PBX
        /// </summary>
        public async Task<bool> MakeCallAsync(string extension, string destination)
        {
            try
            {
                var settings = _settingsService.GetSettings();
                
                if (settings.PBX_UseAMI)
                {
                    var command = $"Action: Originate\r\nChannel: SIP/{extension}\r\nExten: {destination}\r\nContext: from-internal\r\nPriority: 1\r\n\r\n";
                    await SendAMIMessage(command);
                    return true;
                }
                else
                {
                    // HTTP API call
                    using var httpClient = new HttpClient();
                    var response = await httpClient.PostAsync($"http://{settings.PBX_IP}:{settings.PBX_Port}/api/calls/originate", 
                        new StringContent($"{{\"extension\":\"{extension}\",\"destination\":\"{destination}\"}}", 
                        Encoding.UTF8, "application/json"));
                    return response.IsSuccessStatusCode;
                }
            }
            catch (Exception ex)
            {
                _errorLog.LogError(ex);
                return false;
            }
        }

        public void Disconnect()
        {
            try
            {
                _cancellationTokenSource?.Cancel();
                _amiStream?.Close();
                _amiClient?.Close();
                _isConnected = false;
                
                ConnectionStatusChanged?.Invoke(this, new PBXEventArgs(false, "تم قطع الاتصال"));
                _databaseService.LogActivity(null, "PBX Disconnection", ActivityType.System, "تم قطع الاتصال بـ PBX");
            }
            catch (Exception ex)
            {
                _errorLog.LogError(ex);
            }
        }

        public void Dispose()
        {
            Disconnect();
            _cancellationTokenSource?.Dispose();
            _databaseService?.Dispose();
        }
    }

    #region Event Args and Helper Classes

    /// <summary>
    /// Call event arguments
    /// </summary>
    public class CallEventArgs : EventArgs
    {
        public Call Call { get; }

        public CallEventArgs(Call call)
        {
            Call = call;
        }
    }

    /// <summary>
    /// User status event arguments
    /// </summary>
    public class UserStatusEventArgs : EventArgs
    {
        public int UserId { get; }
        public CallStatus Status { get; }

        public UserStatusEventArgs(int userId, CallStatus status)
        {
            UserId = userId;
            Status = status;
        }
    }

    /// <summary>
    /// PBX event arguments
    /// </summary>
    public class PBXEventArgs : EventArgs
    {
        public bool IsConnected { get; }
        public string Message { get; }

        public PBXEventArgs(bool isConnected, string message)
        {
            IsConnected = isConnected;
            Message = message;
        }
    }

    /// <summary>
    /// PBX status information
    /// </summary>
    public class PBXStatusInfo
    {
        public bool IsConnected { get; set; }
        public string ConnectionMethod { get; set; } = string.Empty;
        public DateTime LastUpdate { get; set; }
        public int EventCount { get; set; }
        public List<string> RecentEvents { get; set; } = new();
    }

    #endregion
}
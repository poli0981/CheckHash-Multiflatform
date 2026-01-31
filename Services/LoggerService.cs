using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Channels;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;

namespace CheckHash.Services;

public enum LogLevel
{
    Info,
    Warning,
    Error,
    Success
}

public partial class LoggerService : ObservableObject
{
    private readonly string _debugLogDir;
    private readonly string _errorLogDir;

    private readonly string _logBaseDir;
    private readonly Channel<LogWriteRequest> _logChannel;

    // Settings
    [ObservableProperty] private bool _isRecording = true; // Write logs to UI by default
    [ObservableProperty] private bool _isSavingDebugLog; // Save debug logs to files by default

    private record struct LogWriteRequest(string Directory, string Filename, string Content);

    public LoggerService()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        _logBaseDir = Path.Combine(appData, "CheckHash", "log");
        _errorLogDir = Path.Combine(_logBaseDir, "errors");
        _debugLogDir = Path.Combine(_logBaseDir, "devdebug");

        EnsureDirectories();

        _logChannel = Channel.CreateUnbounded<LogWriteRequest>();
        _ = ProcessLogQueueAsync();
    }

    public static LoggerService Instance { get; } = new();

    // UI Data
    public ObservableCollection<string> Logs { get; } = new();

    private void EnsureDirectories()
    {
        if (!Directory.Exists(_errorLogDir)) Directory.CreateDirectory(_errorLogDir);
        if (!Directory.Exists(_debugLogDir)) Directory.CreateDirectory(_debugLogDir);
    }

    public void Log(string message, LogLevel level = LogLevel.Info)
    {
        var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        var logEntry = $"[{timestamp}] [{level.ToString().ToUpper()}] {message}";

        // 1. Write in UI (Real-time)
        if (IsRecording)
            Dispatcher.UIThread.Post(() =>
            {
                Logs.Add(logEntry);
                // Limit the number of logs to prevent memory issues (keep last 1000 logs)
                if (Logs.Count > 1000) Logs.RemoveAt(0);
            });

        // 2. Save to files
        if (level == LogLevel.Error)
            // Error log always saved
            WriteToFile(_errorLogDir, "error_log.txt", logEntry);
        else if (IsSavingDebugLog)
            // Debug log saved only if enabled
            WriteToFile(_debugLogDir, $"debug_log_{DateTime.Now:yyyyMMdd}.txt", logEntry);
    }

    public void ClearLogs()
    {
        Dispatcher.UIThread.Post(() => Logs.Clear());
    }

    private void WriteToFile(string dir, string filename, string content)
    {
        // Offload to background channel
        _logChannel.Writer.TryWrite(new LogWriteRequest(dir, filename, content));
    }

    private async Task ProcessLogQueueAsync()
    {
        while (await _logChannel.Reader.WaitToReadAsync())
        {
            var batch = new List<LogWriteRequest>();
            while (_logChannel.Reader.TryRead(out var msg))
            {
                batch.Add(msg);
                // Limit batch size to avoid holding too much memory or delaying writes too long
                if (batch.Count >= 1000) break;
            }

            if (batch.Count == 0) continue;

            // Group by file path to minimize file open/close operations
            var fileGroups = batch.GroupBy(x => Path.Combine(x.Directory, x.Filename));

            foreach (var group in fileGroups)
            {
                try
                {
                    var sb = new StringBuilder();
                    foreach (var item in group)
                    {
                        sb.AppendLine(item.Content);
                    }

                    // Use AppendAllTextAsync which opens and closes the file.
                    // Since we batched messages, this is much more efficient than one open/close per message.
                    await File.AppendAllTextAsync(group.Key, sb.ToString());
                }
                catch
                {
                    // Ignore file write errors
                }
            }
        }
    }
}

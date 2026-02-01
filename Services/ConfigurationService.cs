using System;
using System.IO;
using System.Text.Json;
using CheckHash.Models;

namespace CheckHash.Services;

using System.Threading;

public class ConfigurationService
{
    private readonly string _configDir;
    private readonly SemaphoreSlim _saveLock = new(1, 1);

    public ConfigurationService()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        _configDir = Path.Combine(appData, "HashTool", "log", "settings");
        ConfigPath = Path.Combine(_configDir, "config.json");
    }

    public static ConfigurationService Instance { get; } = new();

    public string ConfigPath { get; }

    public async System.Threading.Tasks.Task Save(AppConfig config)
    {
        await _saveLock.WaitAsync();
        try
        {
            if (!Directory.Exists(_configDir)) Directory.CreateDirectory(_configDir);

            var json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(ConfigPath, json);
        }
        catch (Exception ex)
        {
            LoggerService.Instance.Log($"Failed to save config: {ex.Message}", LogLevel.Error);
        }
        finally
        {
            _saveLock.Release();
        }
    }

    public async System.Threading.Tasks.Task SaveAsync(AppConfig config)
    {
        await _saveLock.WaitAsync();
        try
        {
            if (!Directory.Exists(_configDir)) Directory.CreateDirectory(_configDir);

            var json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(ConfigPath, json);
        }
        catch (Exception ex)
        {
            LoggerService.Instance.Log($"Failed to save config: {ex.Message}", LogLevel.Error);
        }
        finally
        {
            _saveLock.Release();
        }
    }

    public AppConfig Load()
    {
        try
        {
            if (File.Exists(ConfigPath))
            {
                var json = File.ReadAllText(ConfigPath);
                var config = JsonSerializer.Deserialize<AppConfig>(json);
                return config ?? new AppConfig();
            }
        }
        catch (Exception ex)
        {
            LoggerService.Instance.Log($"Failed to load config: {ex.Message}", LogLevel.Error);
        }

        return new AppConfig();
    }

    public async System.Threading.Tasks.Task<AppConfig> LoadAsync()
    {
        try
        {
            if (File.Exists(ConfigPath))
            {
                var json = await File.ReadAllTextAsync(ConfigPath);
                var config = JsonSerializer.Deserialize<AppConfig>(json);
                return config ?? new AppConfig();
            }
        }
        catch (Exception ex)
        {
            LoggerService.Instance.Log($"Failed to load config: {ex.Message}", LogLevel.Error);
        }

        return new AppConfig();
    }

    public async System.Threading.Tasks.Task EnsureConfigFileExistsAsync()
    {
        if (!File.Exists(ConfigPath)) await Save(new AppConfig());
    }
}
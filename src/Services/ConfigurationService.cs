using System;
using System.IO;
using System.Text.Json;

namespace X4PlayerShipTradeAnalyzer.Services;

public sealed class ConfigurationService
{
  private static readonly Lazy<ConfigurationService> _lazy = new(() => new ConfigurationService());
  public static ConfigurationService Instance => _lazy.Value;

  private readonly string _configPath;
  private JsonSerializerOptions _jsonSerializerOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true };

  private ConfigurationService()
  {
    // Store <exeName>.json next to the executable for simplicity
    var baseDir = Path.GetDirectoryName(Environment.ProcessPath) ?? AppContext.BaseDirectory;
    var exeName = Path.GetFileNameWithoutExtension(Environment.ProcessPath) ?? "config";
    _configPath = Path.Combine(baseDir, exeName + ".json");
    Load();
  }

  public string? GameFolderExePath { get; set; }
  public string? GameSavePath { get; set; }
  public bool LoadOnlyGameLanguage { get; set; } = true;
  public string AppTheme { get; set; } = "System"; // System | Light | Dark

  public void Save()
  {
    var dto = new PersistedConfig
    {
      GameFolderExePath = GameFolderExePath,
      GameSavePath = GameSavePath,
      LoadOnlyGameLanguage = LoadOnlyGameLanguage,
      AppTheme = AppTheme,
    };
    var json = JsonSerializer.Serialize(dto, _jsonSerializerOptions);
    File.WriteAllText(_configPath, json);
  }

  private void Load()
  {
    try
    {
      if (!File.Exists(_configPath))
        return;
      var json = File.ReadAllText(_configPath);
      var dto = JsonSerializer.Deserialize<PersistedConfig>(json, _jsonSerializerOptions);
      GameFolderExePath = dto?.GameFolderExePath;
      GameSavePath = dto?.GameSavePath;
      LoadOnlyGameLanguage = dto?.LoadOnlyGameLanguage ?? true; // default to true for backward compatibility
      AppTheme = string.IsNullOrWhiteSpace(dto?.AppTheme) ? "System" : dto!.AppTheme!;
    }
    catch
    {
      // ignore malformed config
    }
  }

  private sealed class PersistedConfig
  {
    public string? GameFolderExePath { get; set; }
    public string? GameSavePath { get; set; }
    public bool? LoadOnlyGameLanguage { get; set; }
    public string? AppTheme { get; set; }
  }
}

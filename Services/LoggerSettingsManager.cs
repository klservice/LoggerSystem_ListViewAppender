using LoggerSystem.Interfaces;
using LoggerSystem.Models;

using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
namespace LoggerSystem.Services;

public sealed class LoggerSettingsManager : ILogSettingsService
{
    readonly SemaphoreSlim _gate = new(1, 1);
    readonly string _path;
    public string SettingsFilePath => _path;
    static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public LoggerSettingsManager(string? path = null)
    {
        _path = Path.GetFullPath(
            path ??
            Path.Combine(
                Environment.GetFolderPath(
                    Environment.SpecialFolder.LocalApplicationData),
                "BewerbungsAssiLogger",
                "LoggerSettings.json"));
    }

    public LoggerConfig GetDefaults() => new();
    public async Task<LoggerConfig> LoadAsync(CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        try
        {
            if (!File.Exists(_path))
            {
                var defaults = GetDefaults();
                await WriteAsync(defaults, ct);
                return defaults;
            }
            await using var stream = File.OpenRead(_path);
            var config =
                await JsonSerializer.DeserializeAsync<LoggerConfig>(
                    stream,
                    Options,
                    ct)
                ?? throw new InvalidDataException("Empty settings file.");
            config.Validate();
            return config;
        }
        finally
        {
            _gate.Release();
        }
    }
    public async Task SaveAsync(LoggerConfig settings, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var snapshot = settings.Clone();
        snapshot.Validate();
        await _gate.WaitAsync(ct);
        try
        {
            await WriteAsync(snapshot, ct);
        }
        finally
        {
            _gate.Release();
        }
    }
    async Task WriteAsync(LoggerConfig settings, CancellationToken ct)
    {
        settings.Validate(); Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        var temp = _path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await using (
                var fs = new FileStream(
                    temp,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None,
                    81920,
                    true))
            {
                await JsonSerializer.SerializeAsync(
                    fs,
                    settings,
                    Options,
                    ct);
            }
            File.Move(temp, _path, true);
        }
        finally
        {
            if (File.Exists(temp))
                File.Delete(temp);
        }
    }
}

using LoggerSystem.Models;
namespace LoggerSystem.Interfaces;

public interface ILogSettingsService
{
    Task<LoggerConfig> LoadAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(LoggerConfig settings, CancellationToken cancellationToken = default);
    LoggerConfig GetDefaults();
}

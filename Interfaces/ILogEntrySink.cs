using LoggerSystem.Models;
namespace LoggerSystem.Interfaces;

public interface ILogEntrySink
{
    void AddEntry(LogEntryModel entry);
}

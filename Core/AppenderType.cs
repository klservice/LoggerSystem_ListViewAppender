namespace LoggerSystem.Core;

/// <summary>Combine output targets with the bitwise OR operator.</summary>
[Flags]
public enum AppenderType
{
    None = 0,
    RollingFile = 1,
    Console = 2,
    Memory = 4,
    ListView = Memory
}

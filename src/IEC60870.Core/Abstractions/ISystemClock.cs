namespace IEC60870.Core.Abstractions;

public interface ISystemClock
{
    DateTimeOffset UtcNow { get; }
}

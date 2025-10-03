using IEC60870.Core.Abstractions;

namespace IEC60870.Core.Util;

public sealed class SystemClock : ISystemClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}

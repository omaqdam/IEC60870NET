namespace IEC60870.Runtime.Configuration;

public sealed class RuntimeOptions
{
    public const string SectionName = "IEC60870";

    public EndpointOptions Endpoint { get; init; } = new();
    public Transport104Options Transport104 { get; init; } = new();
    public SecurityOptions? Security { get; init; }

    public sealed class EndpointOptions
    {
        public string Host { get; init; } = "localhost";
        public int Port { get; init; } = 2404;
    }

    public sealed class Transport104Options
    {
        public int T1Milliseconds { get; init; } = 15000;
        public int T2Milliseconds { get; init; } = 10000;
        public int T3Milliseconds { get; init; } = 20000;
        public ushort KWindow { get; init; } = 12;
        public ushort WWindow { get; init; } = 8;
    }

    public sealed class SecurityOptions
    {
        public bool EnableTls { get; init; }
        public string TargetHost { get; init; } = string.Empty;
    }
}

namespace IEC60870.Transport104.States;

public sealed class ApciOptions
{
    public TimeSpan T1 { get; init; } = TimeSpan.FromSeconds(15);
    public TimeSpan T2 { get; init; } = TimeSpan.FromSeconds(10);
    public TimeSpan T3 { get; init; } = TimeSpan.FromSeconds(20);
    public ushort K { get; init; } = 12;
    public ushort W { get; init; } = 8;
}

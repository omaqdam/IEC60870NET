namespace IEC60870.Link101.Serial;

public sealed class SerialPortOptions
{
    public string PortName { get; init; } = "COM1";
    public int BaudRate { get; init; } = 9600;
    public int DataBits { get; init; } = 8;
    public Parity Parity { get; init; } = Parity.Even;
    public StopBits StopBits { get; init; } = StopBits.One;
}

public enum Parity
{
    None,
    Odd,
    Even,
    Mark,
    Space
}

public enum StopBits
{
    None,
    One,
    Two,
    OnePointFive
}

namespace IEC60870.Core.Time;

public readonly struct Cp56Time2a
{
    private const int BytesLength = 7;
    public Cp56Time2a(DateTimeOffset timestamp)
    {
        Timestamp = timestamp.ToUniversalTime();
    }

    public DateTimeOffset Timestamp { get; }

    public static Cp56Time2a FromBytes(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < BytesLength)
        {
            throw new ArgumentException("CP56Time2a requires 7 bytes.", nameof(bytes));
        }

        var milliseconds = bytes[0] | (bytes[1] << 8);
        var minute = (byte)(bytes[2] & 0x3F);
        var hour = (byte)(bytes[3] & 0x1F);
        var day = (byte)(bytes[4] & 0x1F);
        var month = (byte)(bytes[5] & 0x0F);
        var year = (byte)(bytes[6] & 0x7F);

        var baseYear = 2000 + year;
        var dateTime = new DateTime(baseYear, month, day, hour, minute, milliseconds / 1000, DateTimeKind.Utc)
            .AddMilliseconds(milliseconds % 1000);

        return new Cp56Time2a(new DateTimeOffset(dateTime));
    }

    public void WriteBytes(Span<byte> destination)
    {
        if (destination.Length < BytesLength)
        {
            throw new ArgumentException("CP56Time2a requires 7 bytes.", nameof(destination));
        }

        var time = Timestamp;
        var dateTime = time.UtcDateTime;

        var milliseconds = (int)Math.Round(dateTime.TimeOfDay.TotalMilliseconds % 60000);

        destination[0] = (byte)(milliseconds & 0xFF);
        destination[1] = (byte)((milliseconds >> 8) & 0xFF);
        destination[2] = (byte)(dateTime.Minute & 0x3F);
        destination[3] = (byte)(dateTime.Hour & 0x1F);
        destination[4] = (byte)(dateTime.Day & 0x1F);
        destination[5] = (byte)(dateTime.Month & 0x0F);
        destination[6] = (byte)((dateTime.Year - 2000) & 0x7F);
    }

    public override string ToString() => $"{Timestamp:O}";
}

using System;
using System.Buffers;

namespace IEC60870.Link101.Frames;

public sealed class Ft12Frame
{
    public const byte StartByte = 0x68;
    public const byte EndByte = 0x16;

    private Ft12Frame(byte control, ushort address, ReadOnlyMemory<byte> userData)
    {
        Control = control;
        Address = address;
        UserData = userData;
    }

    public byte Control { get; }

    public ushort Address { get; }

    public ReadOnlyMemory<byte> UserData { get; }

    public static Ft12Frame Create(byte control, ushort address, ReadOnlyMemory<byte> userData)
        => new(control, address, userData);

    public void WriteTo(IBufferWriter<byte> writer)
    {
        var userLength = UserData.Length;
        var lengthField = 3 + userLength; // control + address (2 bytes) + user data
        var totalLength = lengthField + 6; // start, len, len, start, payload, checksum, end
        var span = writer.GetSpan(totalLength);

        span[0] = StartByte;
        span[1] = (byte)lengthField;
        span[2] = (byte)lengthField;
        span[3] = StartByte;
        span[4] = Control;
        span[5] = (byte)(Address & 0xFF);
        span[6] = (byte)(Address >> 8);

        if (userLength > 0)
        {
            UserData.Span.CopyTo(span.Slice(7, userLength));
        }

        var checksumIndex = 4 + lengthField;
        var checksum = CalculateChecksum(span.Slice(4, lengthField));
        span[checksumIndex] = checksum;
        span[checksumIndex + 1] = EndByte;

        writer.Advance(totalLength);
    }

    public static bool TryParse(ReadOnlySpan<byte> buffer, out Ft12Frame? frame, out int consumed)
    {
        frame = null;
        consumed = 0;

        if (buffer.Length < 6 || buffer[0] != StartByte)
        {
            return false;
        }

        var lengthField = buffer[1];
        if (buffer[2] != lengthField)
        {
            return false;
        }

        var requiredLength = lengthField + 6;
        if (buffer.Length < requiredLength)
        {
            return false;
        }

        if (buffer[3] != StartByte)
        {
            return false;
        }

        var payloadSpan = buffer.Slice(4, lengthField);
        var checksum = buffer[4 + lengthField];
        var end = buffer[5 + lengthField];

        if (end != EndByte)
        {
            return false;
        }

        if (CalculateChecksum(payloadSpan) != checksum)
        {
            return false;
        }

        var control = payloadSpan[0];
        var address = (ushort)(payloadSpan[1] | (payloadSpan[2] << 8));
        var userDataLength = lengthField - 3;
        ReadOnlyMemory<byte> userData = ReadOnlyMemory<byte>.Empty;
        if (userDataLength > 0)
        {
            var userSlice = payloadSpan.Slice(3, userDataLength).ToArray();
            userData = userSlice;
        }

        frame = new Ft12Frame(control, address, userData);
        consumed = requiredLength;
        return true;
    }

    private static byte CalculateChecksum(ReadOnlySpan<byte> payload)
    {
        int sum = 0;
        for (var i = 0; i < payload.Length; i++)
        {
            sum += payload[i];
        }

        return (byte)(sum & 0xFF);
    }
}

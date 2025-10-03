using System.Buffers;
using System.Buffers.Binary;

namespace IEC60870.Transport104.States;

public enum ApciFrameType
{
    I,
    S,
    U,
}

public enum UFrameCommand : ushort
{
    StartDtAct = 0x07,
    StartDtCon = 0x0B,
    StopDtAct = 0x13,
    StopDtCon = 0x23,
    TestFrAct = 0x43,
    TestFrCon = 0x83,
}

public sealed class ApciFrame
{
    private ApciFrame(ApciFrameType type, ushort sendSequence, ushort receiveSequence, UFrameCommand? command, ReadOnlyMemory<byte> payload)
    {
        Type = type;
        SendSequence = sendSequence;
        ReceiveSequence = receiveSequence;
        Command = command;
        Payload = payload;
    }

    public ApciFrameType Type { get; }

    public ushort SendSequence { get; }

    public ushort ReceiveSequence { get; }

    public UFrameCommand? Command { get; }

    public ReadOnlyMemory<byte> Payload { get; }

    public static ApciFrame CreateIFrame(ushort sendSequence, ushort receiveSequence, ReadOnlyMemory<byte> payload)
        => new(ApciFrameType.I, sendSequence, receiveSequence, null, payload);

    public static ApciFrame CreateSFrame(ushort receiveSequence)
        => new(ApciFrameType.S, 0, receiveSequence, null, ReadOnlyMemory<byte>.Empty);

    public static ApciFrame CreateUFrame(UFrameCommand command)
        => new(ApciFrameType.U, 0, 0, command, ReadOnlyMemory<byte>.Empty);

    public void WriteTo(IBufferWriter<byte> writer)
    {
        var totalLength = 4 + Payload.Length;
        var span = writer.GetSpan(totalLength + 2);
        var slice = span[..(totalLength + 2)];
        slice[0] = 0x68;
        slice[1] = (byte)totalLength;

        switch (Type)
        {
            case ApciFrameType.I:
                BinaryPrimitives.WriteUInt16LittleEndian(slice[2..], (ushort)(SendSequence << 1));
                BinaryPrimitives.WriteUInt16LittleEndian(slice[4..], (ushort)(ReceiveSequence << 1));
                break;
            case ApciFrameType.S:
                BinaryPrimitives.WriteUInt16LittleEndian(slice[2..], 0x01);
                BinaryPrimitives.WriteUInt16LittleEndian(slice[4..], (ushort)(ReceiveSequence << 1));
                break;
            case ApciFrameType.U:
                var command = Command ?? throw new InvalidOperationException("U frame requires a command.");
                BinaryPrimitives.WriteUInt16LittleEndian(slice[2..], (ushort)command);
                BinaryPrimitives.WriteUInt16LittleEndian(slice[4..], 0x0000);
                break;
        }

        if (Payload.Length > 0)
        {
            Payload.Span.CopyTo(slice[6..]);
        }

        writer.Advance(totalLength + 2);
    }

    public static bool TryParse(ref ReadOnlySequence<byte> input, out ApciFrame? frame)
    {
        frame = null;
        if (input.Length < 6)
        {
            return false;
        }

        var reader = new SequenceReader<byte>(input);
        if (!reader.TryRead(out var start) || start != 0x68)
        {
            throw new InvalidOperationException("Invalid APCI start byte.");
        }

        if (!reader.TryRead(out var length))
        {
            return false;
        }

        if (length < 4)
        {
            throw new InvalidOperationException("APCI length field invalid.");
        }

        if (reader.Remaining < length)
        {
            return false;
        }

        Span<byte> control = stackalloc byte[4];
        reader.TryCopyTo(control);
        reader.Advance(4);

        var remainingPayload = length - 4;
        ReadOnlyMemory<byte> payload = ReadOnlyMemory<byte>.Empty;
        if (remainingPayload > 0)
        {
            var payloadBuffer = new byte[remainingPayload];
            reader.TryCopyTo(payloadBuffer);
            reader.Advance(remainingPayload);
            payload = payloadBuffer;
        }

        var sendField = BinaryPrimitives.ReadUInt16LittleEndian(control);
        var receiveField = BinaryPrimitives.ReadUInt16LittleEndian(control[2..]);

        if ((sendField & 0x01) == 0)
        {
            frame = new ApciFrame(ApciFrameType.I, (ushort)(sendField >> 1), (ushort)(receiveField >> 1), null, payload);
        }
        else if ((sendField & 0x03) == 1)
        {
            frame = new ApciFrame(ApciFrameType.S, 0, (ushort)(receiveField >> 1), null, payload);
        }
        else
        {
            frame = new ApciFrame(ApciFrameType.U, 0, 0, (UFrameCommand)sendField, payload);
        }

        input = input.Slice(reader.Position);
        return true;
    }
}

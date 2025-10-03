using System;
namespace IEC60870.Link101.Frames;

public readonly struct Ft12Frame
{
    public Ft12Frame(byte control, ReadOnlyMemory<byte> payload)
    {
        Control = control;
        Payload = payload;
    }

    public byte Control { get; }

    public ReadOnlyMemory<byte> Payload { get; }
}

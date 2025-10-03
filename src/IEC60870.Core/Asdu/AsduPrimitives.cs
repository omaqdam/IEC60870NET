namespace IEC60870.Core.Asdu;

public enum AsduTypeId : byte
{
    // Monitoring
    M_SP_NA_1  = 1,
    M_ME_NC_1  = 13,

    // Control
    C_IC_NA_1 = 100,
}

[Flags]
public enum CauseOfTransmission : ushort
{
    Periodic = 1,
    BackgroundScan = 2,
    Spontaneous = 3,
    Initialized = 4,
    Requested = 5,
    Activation = 6,
    ActivationConfirmation = 7,
    ActivationTermination = 10,
    ReturnInformation = 11,
    ReturnInformationPositive = 20,
    ReturnInformationNegative = 21,
}

public readonly record struct CommonAddress(ushort Value)
{
    public static CommonAddress FromBytes(ReadOnlySpan<byte> span)
        => new((ushort)(span[0] | (span.Length > 1 ? span[1] << 8 : 0)));

    public void WriteBytes(Span<byte> span)
    {
        span[0] = (byte)(Value & 0xFF);
        span[1] = (byte)(Value >> 8);
    }
}

public readonly record struct InformationObjectAddress(uint Value)
{
    public static InformationObjectAddress FromBytes(ReadOnlySpan<byte> span)
        => new((uint)(span[0] | (span[1] << 8) | (span[2] << 16)));

    public void WriteBytes(Span<byte> span)
    {
        span[0] = (byte)(Value & 0xFF);
        span[1] = (byte)((Value >> 8) & 0xFF);
        span[2] = (byte)((Value >> 16) & 0xFF);
    }
}

public readonly record struct QualityDescriptor(byte Raw)
{
    public bool Invalid => (Raw & 0x80) != 0;
    public bool NotTopical => (Raw & 0x40) != 0;
    public bool Substituted => (Raw & 0x20) != 0;
    public bool Blocked => (Raw & 0x10) != 0;
    public bool Overflow => (Raw & 0x01) != 0;
}

public readonly record struct AsduHeader(AsduTypeId TypeId, byte Vsq, CauseOfTransmission Cause, CommonAddress CommonAddress)
{
    public bool IsSequence => (Vsq & 0x80) != 0;
    public byte ObjectCount => (byte)(Vsq & 0x7F);
}

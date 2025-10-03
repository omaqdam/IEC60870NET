using System;
using System.Collections.ObjectModel;
using System.Linq;
using IEC60870.Core.Time;

namespace IEC60870.Core.Asdu;

public abstract class InformationObject
{
    protected InformationObject(InformationObjectAddress address, QualityDescriptor quality)
    {
        Address = address;
        Quality = quality;
    }

    public InformationObjectAddress Address { get; }

    public QualityDescriptor Quality { get; }

    public abstract AsduTypeId TypeId { get; }
}

public sealed class SinglePointInformation : InformationObject
{
    public SinglePointInformation(InformationObjectAddress address, bool value, QualityDescriptor quality)
        : base(address, quality)
    {
        Value = value;
    }

    public bool Value { get; }

    public override AsduTypeId TypeId => AsduTypeId.M_SP_NA_1;
}

public sealed class MeasuredValueShortFloat : InformationObject
{
    public MeasuredValueShortFloat(InformationObjectAddress address, float value, QualityDescriptor quality)
        : base(address, quality)
    {
        if (float.IsNaN(value) || float.IsInfinity(value))
        {
            throw new ArgumentOutOfRangeException(nameof(value), "Measured value must be a finite IEEE 754 number.");
        }

        Value = value;
    }

    public float Value { get; }

    public override AsduTypeId TypeId => AsduTypeId.M_ME_NC_1;
}

public sealed class TimeTaggedSinglePointInformation : InformationObject
{
    public TimeTaggedSinglePointInformation(InformationObjectAddress address, bool value, QualityDescriptor quality, DateTimeOffset timestamp)
        : base(address, quality)
    {
        Value = value;
        Timestamp = timestamp.ToUniversalTime();
    }

    public bool Value { get; }

    public DateTimeOffset Timestamp { get; }

    public override AsduTypeId TypeId => AsduTypeId.M_SP_TB_1;
}

public sealed class InterrogationCommand : InformationObject
{
    public InterrogationCommand(InformationObjectAddress address, byte qualifier)
        : base(address, new QualityDescriptor(0))
    {
        Qualifier = qualifier;
    }

    public byte Qualifier { get; }

    public override AsduTypeId TypeId => AsduTypeId.C_IC_NA_1;
}

public sealed class DoubleCommandInformation : InformationObject
{
    public DoubleCommandInformation(InformationObjectAddress address, DoubleCommandState state, bool select, QualityDescriptor quality)
        : base(address, quality)
    {
        State = state;
        Select = select;
    }

    public DoubleCommandState State { get; }

    public bool Select { get; }

    public override AsduTypeId TypeId => AsduTypeId.C_DC_NA_1;
}

public sealed class Asdu
{
    private readonly ReadOnlyCollection<InformationObject> _objects;

    public Asdu(AsduHeader header, IReadOnlyList<InformationObject> objects)
    {
        if (objects.Count == 0)
        {
            throw new ArgumentException("An ASDU must contain at least one information object.", nameof(objects));
        }

        if (objects.Any(obj => obj.TypeId != header.TypeId))
        {
            throw new InvalidOperationException("All information objects must match the ASDU type.");
        }

        Header = header with
        {
            Vsq = (byte)((objects.Count & 0x7F) | (header.IsSequence ? 0x80 : 0x00))
        };

        _objects = new ReadOnlyCollection<InformationObject>(objects.ToArray());
    }

    public AsduHeader Header { get; }

    public IReadOnlyList<InformationObject> Objects => _objects;
}

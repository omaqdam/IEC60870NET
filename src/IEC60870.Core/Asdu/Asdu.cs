using System.Linq;
using System.Collections.ObjectModel;

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
public sealed class Asdu
{
    private readonly ReadOnlyCollection<InformationObject> _objects;

    public Asdu(AsduHeader header, IReadOnlyList<InformationObject> objects)
    {
        if (objects.Count == 0)
        {
            throw new ArgumentException("An ASDU must contain at least one information object.", nameof(objects));
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

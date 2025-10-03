using IEC60870.Core.Asdu;
using IEC60870.Core.Util;

namespace IEC60870.App.Codecs;

public sealed class DoublePointCodec : IInformationObjectCodec
{
    public AsduTypeId TypeId => AsduTypeId.M_DP_NA_1;

    public void Encode(InformationObject informationObject, ref SpanWriter writer)
    {
        if (informationObject is not DoublePointInformation dpi)
        {
            throw new ArgumentException($"Information object must be {nameof(DoublePointInformation)}.", nameof(informationObject));
        }

        writer.WriteUInt24(dpi.Address.Value);
        var encoded = (byte)((byte)dpi.State & 0x03);
        encoded |= (byte)(dpi.Quality.Raw & 0xF0);
        writer.WriteByte(encoded);
    }

    public InformationObject Decode(ref SpanReader reader, AsduHeader header)
    {
        var address = new InformationObjectAddress(reader.ReadUInt24());
        var raw = reader.ReadByte();
        var state = (DoublePointState)(raw & 0x03);
        var quality = new QualityDescriptor((byte)(raw & 0xF0));
        return new DoublePointInformation(address, state, quality);
    }
}

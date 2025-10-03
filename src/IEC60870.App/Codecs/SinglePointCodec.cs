using IEC60870.Core.Asdu;
using IEC60870.Core.Util;

namespace IEC60870.App.Codecs;

public sealed class SinglePointCodec : IInformationObjectCodec
{
    public AsduTypeId TypeId => AsduTypeId.M_SP_NA_1;

    public void Encode(InformationObject informationObject, ref SpanWriter writer)
    {
        if (informationObject is not SinglePointInformation spi)
        {
            throw new ArgumentException($"Information object must be {nameof(SinglePointInformation)}.", nameof(informationObject));
        }

        writer.WriteUInt24(spi.Address.Value);
        var quality = spi.Quality.Raw;
        var valueByte = (byte)(spi.Value ? 0x01 : 0x00);
        writer.WriteByte((byte)(valueByte | (quality & 0xF0)));
    }

    public InformationObject Decode(ref SpanReader reader, AsduHeader header)
    {
        var address = new InformationObjectAddress(reader.ReadUInt24());
        var value = reader.ReadByte();
        var state = (value & 0x01) != 0;
        var quality = new QualityDescriptor((byte)(value & 0xF0));
        return new SinglePointInformation(address, state, quality);
    }
}

using IEC60870.Core.Asdu;
using IEC60870.Core.Util;

namespace IEC60870.App.Codecs;

public sealed class NormalizedMeasuredValueCodec : IInformationObjectCodec
{
    public AsduTypeId TypeId => AsduTypeId.M_ME_NA_1;

    public void Encode(InformationObject informationObject, ref SpanWriter writer)
    {
        if (informationObject is not NormalizedMeasuredValue nmv)
        {
            throw new ArgumentException($"Information object must be {nameof(NormalizedMeasuredValue)}.", nameof(informationObject));
        }

        writer.WriteUInt24(nmv.Address.Value);
        writer.WriteUInt16((ushort)nmv.Value);
        writer.WriteByte(nmv.Quality.Raw);
    }

    public InformationObject Decode(ref SpanReader reader, AsduHeader header)
    {
        var address = new InformationObjectAddress(reader.ReadUInt24());
        var value = (short)reader.ReadUInt16();
        var quality = new QualityDescriptor(reader.ReadByte());
        return new NormalizedMeasuredValue(address, value, quality);
    }
}

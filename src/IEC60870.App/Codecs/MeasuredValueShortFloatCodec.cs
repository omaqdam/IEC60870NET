using IEC60870.Core.Asdu;
using IEC60870.Core.Util;
using System.Buffers.Binary;

namespace IEC60870.App.Codecs;

public sealed class MeasuredValueShortFloatCodec : IInformationObjectCodec
{
    public AsduTypeId TypeId => AsduTypeId.M_ME_NC_1;

    public void Encode(InformationObject informationObject, ref SpanWriter writer)
    {
        if (informationObject is not MeasuredValueShortFloat mv)
        {
            throw new ArgumentException($"Information object must be {nameof(MeasuredValueShortFloat)}.", nameof(informationObject));
        }

        writer.WriteUInt24(mv.Address.Value);
        writer.WriteSingle(mv.Value);
        writer.WriteByte(mv.Quality.Raw);
    }

    public InformationObject Decode(ref SpanReader reader, AsduHeader header)
    {
        var address = new InformationObjectAddress(reader.ReadUInt24());
        var floatBytes = reader.ReadSpan(4);
        var value = BinaryPrimitives.ReadSingleLittleEndian(floatBytes);
        var quality = new QualityDescriptor(reader.ReadByte());
        return new MeasuredValueShortFloat(address, value, quality);
    }
}

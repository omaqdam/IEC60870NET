using IEC60870.Core.Asdu;
using IEC60870.Core.Util;

namespace IEC60870.App.Codecs;

public sealed class SetpointNormalizedCommandCodec : IInformationObjectCodec
{
    public AsduTypeId TypeId => AsduTypeId.C_SE_NA_1;

    public void Encode(InformationObject informationObject, ref SpanWriter writer)
    {
        if (informationObject is not SetpointCommandNormalized sp)
        {
            throw new ArgumentException($"Information object must be {nameof(SetpointCommandNormalized)}.", nameof(informationObject));
        }

        writer.WriteUInt24(sp.Address.Value);
        writer.WriteUInt16((ushort)sp.Value);
        byte qos = (byte)(sp.Select ? 0x80 : 0x00);
        qos |= (byte)(sp.Quality.Raw & 0x70);
        writer.WriteByte(qos);
    }

    public InformationObject Decode(ref SpanReader reader, AsduHeader header)
    {
        var address = new InformationObjectAddress(reader.ReadUInt24());
        var value = (short)reader.ReadUInt16();
        var qos = reader.ReadByte();
        var select = (qos & 0x80) != 0;
        var quality = new QualityDescriptor((byte)(qos & 0x70));
        return new SetpointCommandNormalized(address, value, select, quality);
    }
}

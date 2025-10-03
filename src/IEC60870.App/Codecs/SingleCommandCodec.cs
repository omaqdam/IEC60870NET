using IEC60870.Core.Asdu;
using IEC60870.Core.Util;

namespace IEC60870.App.Codecs;

public sealed class SingleCommandCodec : IInformationObjectCodec
{
    public AsduTypeId TypeId => AsduTypeId.C_SC_NA_1;

    public void Encode(InformationObject informationObject, ref SpanWriter writer)
    {
        if (informationObject is not SingleCommandInformation sci)
        {
            throw new ArgumentException($"Information object must be {nameof(SingleCommandInformation)}.", nameof(informationObject));
        }

        writer.WriteUInt24(sci.Address.Value);
        byte command = (byte)(sci.Value ? 0x01 : 0x00);
        if (sci.Select)
        {
            command |= 0x80;
        }

        command |= (byte)(sci.Quality.Raw & 0x70);
        writer.WriteByte(command);
    }

    public InformationObject Decode(ref SpanReader reader, AsduHeader header)
    {
        var address = new InformationObjectAddress(reader.ReadUInt24());
        var command = reader.ReadByte();
        var value = (command & 0x01) != 0;
        var select = (command & 0x80) != 0;
        var quality = new QualityDescriptor((byte)(command & 0x70));
        return new SingleCommandInformation(address, value, select, quality);
    }
}

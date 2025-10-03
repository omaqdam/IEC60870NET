using IEC60870.Core.Asdu;
using IEC60870.Core.Util;

namespace IEC60870.App.Codecs;

public sealed class DoubleCommandCodec : IInformationObjectCodec
{
    public AsduTypeId TypeId => AsduTypeId.C_DC_NA_1;

    public void Encode(InformationObject informationObject, ref SpanWriter writer)
    {
        if (informationObject is not DoubleCommandInformation dc)
        {
            throw new ArgumentException($"Information object must be {nameof(DoubleCommandInformation)}.", nameof(informationObject));
        }

        writer.WriteUInt24(dc.Address.Value);
        var command = (byte)((byte)dc.State & 0x03);
        if (dc.Select)
        {
            command |= 0x04;
        }

        command |= (byte)(dc.Quality.Raw & 0xF0);
        writer.WriteByte(command);
    }

    public InformationObject Decode(ref SpanReader reader, AsduHeader header)
    {
        var address = new InformationObjectAddress(reader.ReadUInt24());
        var command = reader.ReadByte();
        var state = (DoubleCommandState)(command & 0x03);
        var select = (command & 0x04) != 0;
        var quality = new QualityDescriptor((byte)(command & 0xF0));
        return new DoubleCommandInformation(address, state, select, quality);
    }
}

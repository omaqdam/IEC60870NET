using IEC60870.Core.Asdu;
using IEC60870.Core.Time;
using IEC60870.Core.Util;

namespace IEC60870.App.Codecs;

public sealed class TimeTaggedSinglePointCodec : IInformationObjectCodec
{
    public AsduTypeId TypeId => AsduTypeId.M_SP_TB_1;

    public void Encode(InformationObject informationObject, ref SpanWriter writer)
    {
        if (informationObject is not TimeTaggedSinglePointInformation tt)
        {
            throw new ArgumentException($"Information object must be {nameof(TimeTaggedSinglePointInformation)}.", nameof(informationObject));
        }

        writer.WriteUInt24(tt.Address.Value);
        var stateByte = (byte)(tt.Value ? 0x01 : 0x00);
        stateByte |= (byte)(tt.Quality.Raw & 0xF0);
        writer.WriteByte(stateByte);

        var timestampBytes = new byte[7];
        new Cp56Time2a(tt.Timestamp).WriteBytes(timestampBytes);
        writer.WriteBytes(timestampBytes);
    }

    public InformationObject Decode(ref SpanReader reader, AsduHeader header)
    {
        var address = new InformationObjectAddress(reader.ReadUInt24());
        var state = reader.ReadByte();
        var value = (state & 0x01) != 0;
        var quality = new QualityDescriptor((byte)(state & 0xF0));
        var timeBytes = reader.ReadSpan(7);
        var timestamp = Cp56Time2a.FromBytes(timeBytes).Timestamp;
        return new TimeTaggedSinglePointInformation(address, value, quality, timestamp);
    }
}


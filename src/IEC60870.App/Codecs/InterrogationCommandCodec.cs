using IEC60870.Core.Asdu;
using IEC60870.Core.Util;

namespace IEC60870.App.Codecs;

public sealed class InterrogationCommandCodec : IInformationObjectCodec
{
    public AsduTypeId TypeId => AsduTypeId.C_IC_NA_1;

    public void Encode(InformationObject informationObject, ref SpanWriter writer)
    {
        if (informationObject is not InterrogationCommand command)
        {
            throw new ArgumentException($"Information object must be {nameof(InterrogationCommand)}.", nameof(informationObject));
        }

        writer.WriteUInt24(command.Address.Value);
        writer.WriteByte(command.Qualifier);
    }

    public InformationObject Decode(ref SpanReader reader, AsduHeader header)
    {
        var address = new InformationObjectAddress(reader.ReadUInt24());
        var qualifier = reader.ReadByte();
        return new InterrogationCommand(address, qualifier);
    }
}

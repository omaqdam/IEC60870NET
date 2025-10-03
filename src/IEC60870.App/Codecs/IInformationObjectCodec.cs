using IEC60870.Core.Asdu;

namespace IEC60870.App.Codecs;

public interface IInformationObjectCodec
{
    AsduTypeId TypeId { get; }

    void Encode(InformationObject informationObject, ref IEC60870.Core.Util.SpanWriter writer);

    InformationObject Decode(ref IEC60870.Core.Util.SpanReader reader, AsduHeader header);
}

using IEC60870.Core.Abstractions;
using IEC60870.Core.Asdu;
using IEC60870.Core.Util;
using AsduMessage = IEC60870.Core.Asdu.Asdu;

namespace IEC60870.App.Codecs;

public sealed class AsduCodecRegistry : IAsduSerializer
{
    private readonly Dictionary<AsduTypeId, IInformationObjectCodec> _codecs;

    public AsduCodecRegistry(IEnumerable<IInformationObjectCodec>? codecs = null)
    {
        _codecs = (codecs ?? BuildDefaultCodecs()).ToDictionary(codec => codec.TypeId);
    }

    private static IEnumerable<IInformationObjectCodec> BuildDefaultCodecs()
    {
        yield return new SinglePointCodec();
        yield return new MeasuredValueShortFloatCodec();
        yield return new TimeTaggedSinglePointCodec();
        yield return new InterrogationCommandCodec();
        yield return new DoubleCommandCodec();
    }

    public int Write(AsduMessage asdu, Span<byte> destination)
    {
        if (!_codecs.TryGetValue(asdu.Header.TypeId, out var codec))
        {
            throw new InvalidOperationException($"No codec registered for type {asdu.Header.TypeId}.");
        }

        var writer = new SpanWriter(destination);
        writer.WriteByte((byte)asdu.Header.TypeId);
        writer.WriteByte(asdu.Header.Vsq);
        writer.WriteUInt16((ushort)asdu.Header.Cause);
        writer.WriteUInt16(asdu.Header.CommonAddress.Value);

        foreach (var informationObject in asdu.Objects)
        {
            codec.Encode(informationObject, ref writer);
        }

        return writer.Position;
    }

    public bool TryRead(ref ReadOnlySpan<byte> source, out AsduMessage? asdu)
    {
        if (source.Length < 6)
        {
            asdu = null;
            return false;
        }

        var reader = new SpanReader(source);
        var typeId = (AsduTypeId)reader.ReadByte();
        var vsq = reader.ReadByte();
        var cause = (CauseOfTransmission)reader.ReadUInt16();
        var ca = new CommonAddress(reader.ReadUInt16());

        if (!_codecs.TryGetValue(typeId, out var codec))
        {
            asdu = null;
            return false;
        }

        var header = new AsduHeader(typeId, vsq, cause, ca);
        var expectedCount = Math.Max((int)header.ObjectCount, 1);
        var objects = new List<InformationObject>(expectedCount);

        for (var i = 0; i < header.ObjectCount; i++)
        {
            objects.Add(codec.Decode(ref reader, header));
        }

        var consumed = source.Length - reader.Remaining;
        source = source.Slice(consumed);
        asdu = new AsduMessage(header, objects);
        return true;
    }
}

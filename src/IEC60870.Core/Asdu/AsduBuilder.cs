using System.Linq;
namespace IEC60870.Core.Asdu;

public sealed class AsduBuilder
{
    private AsduHeader? _header;
    private readonly List<InformationObject> _objects = new();

    public AsduBuilder WithHeader(AsduHeader header)
    {
        _header = header;
        return this;
    }

    public AsduBuilder AddObject(InformationObject informationObject)
    {
        _objects.Add(informationObject ?? throw new ArgumentNullException(nameof(informationObject)));
        return this;
    }

    public Asdu Build()
    {
        if (_header is null)
        {
            throw new InvalidOperationException("Header must be set before building an ASDU.");
        }

        if (_objects.Count == 0)
        {
            throw new InvalidOperationException("At least one information object must be supplied.");
        }

        var header = _header.Value;
        if (_objects.Any(o => o.TypeId != header.TypeId))
        {
            throw new InvalidOperationException("All information objects must match the ASDU type.");
        }

        var normalizedHeader = header with
        {
            Vsq = (byte)((_objects.Count & 0x7F) | (header.IsSequence ? 0x80 : 0x00))
        };

        return new Asdu(normalizedHeader, _objects);
    }
}

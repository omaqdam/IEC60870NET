using IEC60870.Core.Asdu;
using AsduMessage = IEC60870.Core.Asdu.Asdu;

namespace IEC60870.Core.Abstractions;

public interface IAsduSerializer
{
    int Write(AsduMessage asdu, Span<byte> destination);

    bool TryRead(ref ReadOnlySpan<byte> source, out AsduMessage? asdu);
}

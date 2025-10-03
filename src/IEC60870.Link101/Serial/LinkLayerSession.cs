using System.Buffers;
using System.Threading;
using System.Threading.Tasks;
using IEC60870.Link101.Frames;

namespace IEC60870.Link101.Serial;

public sealed class LinkLayerSession : IAsyncDisposable
{
    private readonly Stream _transport;
    private readonly bool _balanced;
    private readonly Ft12FrameParser _parser = new();
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private readonly byte _stationAddress;

    public LinkLayerSession(Stream transport, byte stationAddress, bool balanced)
    {
        _transport = transport ?? throw new ArgumentNullException(nameof(transport));
        _stationAddress = stationAddress;
        _balanced = balanced;
    }

    public bool IsBalanced => _balanced;

    public Task SendStartDataTransferAsync(CancellationToken cancellationToken)
        => SendAsync(_balanced ? (byte)0x43 : (byte)0x07, ReadOnlyMemory<byte>.Empty, cancellationToken);

    public Task SendStopDataTransferAsync(CancellationToken cancellationToken)
        => SendAsync(_balanced ? (byte)0x23 : (byte)0x13, ReadOnlyMemory<byte>.Empty, cancellationToken);

    public Task SendTestFrameAsync(CancellationToken cancellationToken)
        => SendAsync(_balanced ? (byte)0x83 : (byte)0x43, ReadOnlyMemory<byte>.Empty, cancellationToken);

    public async Task SendAsync(byte control, ReadOnlyMemory<byte> userData, CancellationToken cancellationToken)
    {
        var frame = Ft12Frame.Create(control, _stationAddress, userData);
        var writer = new ArrayBufferWriter<byte>();
        frame.WriteTo(writer);

        await _writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await _transport.WriteAsync(writer.WrittenMemory, cancellationToken).ConfigureAwait(false);
            await _transport.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public async Task<Ft12Frame?> ReceiveAsync(CancellationToken cancellationToken)
    {
        byte[] buffer = new byte[256];

        while (!cancellationToken.IsCancellationRequested)
        {
            if (_parser.TryReadFrame(out var frame))
            {
                return frame;
            }

            var read = await _transport.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                continue;
            }

            _parser.Append(buffer.AsSpan(0, read));
        }

        return null;
    }

    public ValueTask DisposeAsync()
    {
        _writeLock.Dispose();
        return ValueTask.CompletedTask;
    }
}

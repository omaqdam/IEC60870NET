using System;
using System.Buffers;
using System.IO;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using IEC60870.Link101.Frames;
using IEC60870.Link101.Serial;
using Xunit;

namespace IEC60870.UnitTests;

public sealed class Link101SessionTests
{
    [Fact]
    public async Task SessionSendsAndReceivesFrames()
    {
        await using var pair = LoopbackDuplex.Create();
        await using var session = new LinkLayerSession(pair.Local, stationAddress: 0x01, balanced: true);

        await session.SendStartDataTransferAsync(CancellationToken.None);

        var outboundBuffer = new byte[64];
        var read = await pair.Remote.ReadAsync(outboundBuffer, 0, outboundBuffer.Length);
        read.Should().BeGreaterThan(0);
        var parser = new Ft12FrameParser();
        parser.Append(outboundBuffer.AsSpan(0, read));
        parser.TryReadFrame(out var startFrame).Should().BeTrue();
        startFrame!.Control.Should().Be(0x43);

        var responseWriter = new ArrayBufferWriter<byte>();
        Ft12Frame.Create(0x83, 0x01, new byte[] { 0x55 }).WriteTo(responseWriter);
        var responseBytes = responseWriter.WrittenSpan.ToArray();
        await pair.Remote.WriteAsync(responseBytes, 0, responseBytes.Length, CancellationToken.None);

        var received = await session.ReceiveAsync(CancellationToken.None);
        received.Should().NotBeNull();
        received!.Control.Should().Be(0x83);
        received.UserData.ToArray().Should().Equal(new byte[] { 0x55 });
    }

    private sealed class LoopbackDuplex : IAsyncDisposable
    {
        private readonly LoopbackStream _local;
        private readonly LoopbackStream _remote;

        private LoopbackDuplex(LoopbackStream local, LoopbackStream remote)
        {
            _local = local;
            _remote = remote;
        }

        public Stream Local => _local;
        public Stream Remote => _remote;

        public static LoopbackDuplex Create()
        {
            var forward = new Pipe();
            var backward = new Pipe();
            var local = new LoopbackStream(forward.Reader, backward.Writer);
            var remote = new LoopbackStream(backward.Reader, forward.Writer);
            return new LoopbackDuplex(local, remote);
        }

        public ValueTask DisposeAsync()
        {
            _local.Dispose();
            _remote.Dispose();
            return ValueTask.CompletedTask;
        }

        private sealed class LoopbackStream : Stream
        {
            private readonly PipeReader _reader;
            private readonly PipeWriter _writer;

            public LoopbackStream(PipeReader reader, PipeWriter writer)
            {
                _reader = reader;
                _writer = writer;
            }

            public override bool CanRead => true;
            public override bool CanSeek => false;
            public override bool CanWrite => true;
            public override long Length => throw new NotSupportedException();
            public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

            public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
            {
                while (true)
                {
                    var result = await _reader.ReadAsync(cancellationToken).ConfigureAwait(false);
                    if (!result.Buffer.IsEmpty)
                    {
                        var toCopy = (int)Math.Min(buffer.Length, result.Buffer.Length);
                        result.Buffer.Slice(0, toCopy).CopyTo(buffer.Span);
                        _reader.AdvanceTo(result.Buffer.GetPosition(toCopy));
                        return toCopy;
                    }

                    if (result.IsCompleted)
                    {
                        return 0;
                    }

                    _reader.AdvanceTo(result.Buffer.Start, result.Buffer.End);
                }
            }

            public override int Read(byte[] buffer, int offset, int count)
                => ReadAsync(buffer.AsMemory(offset, count)).GetAwaiter().GetResult();

            public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            {
                await _writer.WriteAsync(buffer.AsMemory(offset, count), cancellationToken).ConfigureAwait(false);
                await _writer.FlushAsync(cancellationToken).ConfigureAwait(false);
            }

            public override void Write(byte[] buffer, int offset, int count)
                => WriteAsync(buffer, offset, count, CancellationToken.None).GetAwaiter().GetResult();

            public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
            {
                return InternalWriteAsync(buffer, cancellationToken);

                async ValueTask InternalWriteAsync(ReadOnlyMemory<byte> data, CancellationToken ct)
                {
                    await _writer.WriteAsync(data, ct).ConfigureAwait(false);
                    await _writer.FlushAsync(ct).ConfigureAwait(false);
                }
            }

            public override void Flush() => _writer.FlushAsync(CancellationToken.None).GetAwaiter().GetResult();
            public override Task FlushAsync(CancellationToken cancellationToken)
                => _writer.FlushAsync(cancellationToken).AsTask();

            public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
            public override void SetLength(long value) => throw new NotSupportedException();

            protected override void Dispose(bool disposing)
            {
                if (disposing)
                {
                    _reader.Complete();
                    _writer.Complete();
                }
                base.Dispose(disposing);
            }
        }
    }
}

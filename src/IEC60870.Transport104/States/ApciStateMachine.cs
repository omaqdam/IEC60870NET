using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using IEC60870.Core.Abstractions;
using IEC60870.Core.Asdu;
using IEC60870.Core.Util;
using Microsoft.Extensions.Logging;
using AsduMessage = IEC60870.Core.Asdu.Asdu;

namespace IEC60870.Transport104.States;

public sealed class ApciStateMachine : IAsyncDisposable
{
    private const int SequenceModulus = 1 << 15;

    private readonly Stream _stream;
    private readonly IAsduSerializer _serializer;
    private readonly ApciOptions _options;
    private readonly ISystemClock _clock;
    private readonly ILogger<ApciStateMachine>? _logger;
    private readonly Channel<AsduMessage> _outbound;
    private readonly CancellationTokenSource _cts = new();
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private readonly AsyncAutoResetEvent _windowSignal = new(true);

    private Task? _receiveLoop;
    private Task? _sendLoop;
    private Task? _timerLoop;

    private TaskCompletionSource<bool> _startCompletion = CreateCompletionSource();
    private bool _started;

    private ushort _sendSequence;
    private ushort _expectedReceiveSequence;
    private ushort _peerAckSequence;
    private ushort _lastAckSentSequence;
    private ushort _pendingAckSequence;

    private bool _ackPending;

    private DateTimeOffset _lastIFrameSentUtc;
    private DateTimeOffset _lastAckFromPeerUtc;
    private DateTimeOffset _lastRxUtc;
    private DateTimeOffset _ackPendingSinceUtc;
    private DateTimeOffset _lastTestFrameUtc;

    public event EventHandler<AsduMessage>? AsduReceived;
    public event EventHandler<Exception>? ConnectionFaulted;

    public ApciStateMachine(
        Stream stream,
        IAsduSerializer serializer,
        ISystemClock clock,
        ApciOptions? options = null,
        ILogger<ApciStateMachine>? logger = null)
    {
        _stream = stream ?? throw new ArgumentNullException(nameof(stream));
        if (!stream.CanRead || !stream.CanWrite)
        {
            throw new ArgumentException("Stream must support read and write.", nameof(stream));
        }

        _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _options = options ?? new ApciOptions();
        _logger = logger;

        _outbound = Channel.CreateUnbounded<AsduMessage>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        });

        var now = _clock.UtcNow;
        _lastRxUtc = now;
        _lastAckFromPeerUtc = now;
        _lastTestFrameUtc = now;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (_started)
        {
            throw new InvalidOperationException("State machine already started.");
        }

        _started = true;
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token, cancellationToken);
        var ct = linkedCts.Token;

        _receiveLoop = Task.Run(() => ReceiveLoopAsync(ct), CancellationToken.None);
        _sendLoop = Task.Run(() => SendLoopAsync(ct), CancellationToken.None);
        _timerLoop = Task.Run(() => TimerLoopAsync(ct), CancellationToken.None);

        await SendUFrameAsync(UFrameCommand.StartDtAct, ct).ConfigureAwait(false);

        using var timeout = new CancellationTokenSource(_options.T1);
        using var wait = CancellationTokenSource.CreateLinkedTokenSource(timeout.Token, ct);
        try
        {
            await _startCompletion.Task.WaitAsync(wait.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException ex)
        {
            throw new TimeoutException("STARTDT confirmation was not received within T1.", ex);
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (!_started)
        {
            return;
        }

        await SendUFrameAsync(UFrameCommand.StopDtAct, cancellationToken).ConfigureAwait(false);
        await DisposeAsync().ConfigureAwait(false);
    }

    public async Task SendAsduAsync(AsduMessage asdu, CancellationToken cancellationToken)
    {
        if (!_started)
        {
            throw new InvalidOperationException("State machine not started.");
        }

        await _outbound.Writer.WriteAsync(asdu, cancellationToken).ConfigureAwait(false);
    }

    private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
    {
        var reader = PipeReader.Create(_stream);

        try
        {
            while (true)
            {
                var result = await reader.ReadAsync(cancellationToken).ConfigureAwait(false);
                var buffer = result.Buffer;

                while (ApciFrame.TryParse(ref buffer, out var frame))
                {
                    if (frame is not null)
                    {
                        await HandleFrameAsync(frame, cancellationToken).ConfigureAwait(false);
                    }
                }

                reader.AdvanceTo(buffer.Start, buffer.End);

                if (result.IsCompleted)
                {
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Receive loop faulted.");
            ConnectionFaulted?.Invoke(this, ex);
            _cts.Cancel();
        }
        finally
        {
            await reader.CompleteAsync().ConfigureAwait(false);
        }
    }

    private async Task HandleFrameAsync(ApciFrame frame, CancellationToken cancellationToken)
    {
        _lastRxUtc = _clock.UtcNow;

        switch (frame.Type)
        {
            case ApciFrameType.I:
                await HandleIFrameAsync(frame, cancellationToken).ConfigureAwait(false);
                break;
            case ApciFrameType.S:
                HandleSFrame(frame);
                break;
            case ApciFrameType.U:
                await HandleUFrameAsync(frame, cancellationToken).ConfigureAwait(false);
                break;
        }
    }

    private async Task HandleIFrameAsync(ApciFrame frame, CancellationToken cancellationToken)
    {
        HandlePeerAck(frame.ReceiveSequence);

        if (frame.SendSequence != _expectedReceiveSequence)
        {
            _logger?.LogWarning("Unexpected I frame sequence. Expected {Expected}, got {Actual}.", _expectedReceiveSequence, frame.SendSequence);
            _expectedReceiveSequence = frame.SendSequence;
        }

        _expectedReceiveSequence = IncrementSequence(_expectedReceiveSequence);
        _pendingAckSequence = _expectedReceiveSequence;
        _ackPending = true;
        _ackPendingSinceUtc = _clock.UtcNow;

        if (frame.Payload.Length > 0)
        {
            var asdus = DecodePayload(frame.Payload);
            foreach (var asdu in asdus)
            {
                AsduReceived?.Invoke(this, asdu);
            }
        }

        if (SequenceDistance(_pendingAckSequence, _lastAckSentSequence) >= _options.W)
        {
            await SendSFrameAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private void HandleSFrame(ApciFrame frame)
    {
        HandlePeerAck(frame.ReceiveSequence);
    }

    private async Task HandleUFrameAsync(ApciFrame frame, CancellationToken cancellationToken)
    {
        if (frame.Command is null)
        {
            return;
        }

        switch (frame.Command.Value)
        {
            case UFrameCommand.StartDtCon:
                _startCompletion.TrySetResult(true);
                break;
            case UFrameCommand.StopDtAct:
                await SendUFrameAsync(UFrameCommand.StopDtCon, cancellationToken).ConfigureAwait(false);
                break;
            case UFrameCommand.TestFrAct:
                await SendUFrameAsync(UFrameCommand.TestFrCon, cancellationToken).ConfigureAwait(false);
                break;
            case UFrameCommand.TestFrCon:
                _lastAckFromPeerUtc = _clock.UtcNow;
                break;
        }
    }

    private async Task SendLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (await _outbound.Reader.WaitToReadAsync(cancellationToken).ConfigureAwait(false))
            {
                while (_outbound.Reader.TryRead(out var asdu))
                {
                    await SendAsduInternalAsync(asdu, cancellationToken).ConfigureAwait(false);
                }
            }
        }
        catch (Exception ex) when (ex is OperationCanceledException or ChannelClosedException)
        {
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Send loop faulted.");
            ConnectionFaulted?.Invoke(this, ex);
            _cts.Cancel();
        }
    }

    private async Task SendAsduInternalAsync(AsduMessage asdu, CancellationToken cancellationToken)
    {
        while (SequenceDistance(_sendSequence, _peerAckSequence) >= _options.K)
        {
            await _windowSignal.WaitAsync(cancellationToken).ConfigureAwait(false);
        }

        var payload = SerializeAsdu(asdu);
        var frame = ApciFrame.CreateIFrame(_sendSequence, _expectedReceiveSequence, payload);
        await SendFrameAsync(frame, cancellationToken).ConfigureAwait(false);

        _sendSequence = IncrementSequence(_sendSequence);
        _lastIFrameSentUtc = _clock.UtcNow;
    }

    private async Task SendFrameAsync(ApciFrame frame, CancellationToken cancellationToken)
    {
        var buffer = new ArrayBufferWriter<byte>(frame.Payload.Length + 6);
        frame.WriteTo(buffer);

        await _writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await _stream.WriteAsync(buffer.WrittenMemory, cancellationToken).ConfigureAwait(false);
            await _stream.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    private async Task SendUFrameAsync(UFrameCommand command, CancellationToken cancellationToken)
    {
        var frame = ApciFrame.CreateUFrame(command);
        await SendFrameAsync(frame, cancellationToken).ConfigureAwait(false);

        if (command == UFrameCommand.TestFrAct)
        {
            _lastTestFrameUtc = _clock.UtcNow;
        }
    }

    private async Task SendSFrameAsync(CancellationToken cancellationToken)
    {
        var frame = ApciFrame.CreateSFrame(_pendingAckSequence);
        await SendFrameAsync(frame, cancellationToken).ConfigureAwait(false);
        _lastAckSentSequence = _pendingAckSequence;
        _ackPending = false;
    }

    private void HandlePeerAck(ushort peerAck)
    {
        if (SequenceDistance(peerAck, _peerAckSequence) == 0)
        {
            return;
        }

        _peerAckSequence = peerAck;
        _lastAckFromPeerUtc = _clock.UtcNow;
        _windowSignal.Set();
    }

    private async Task TimerLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var now = _clock.UtcNow;

                if (SequenceDistance(_sendSequence, _peerAckSequence) > 0)
                {
                    if (now - _lastIFrameSentUtc > _options.T1)
                    {
                        throw new TimeoutException("t1 expired waiting for acknowledgement.");
                    }
                }

                if (_ackPending && now - _ackPendingSinceUtc > _options.T2)
                {
                    await SendSFrameAsync(cancellationToken).ConfigureAwait(false);
                }

                if (now - _lastRxUtc > _options.T3 && now - _lastTestFrameUtc > _options.T3)
                {
                    await SendUFrameAsync(UFrameCommand.TestFrAct, cancellationToken).ConfigureAwait(false);
                }

                await Task.Delay(TimeSpan.FromMilliseconds(200), cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Timer loop faulted.");
            ConnectionFaulted?.Invoke(this, ex);
            _cts.Cancel();
        }
    }

    private List<AsduMessage> DecodePayload(ReadOnlyMemory<byte> payload)
    {
        var result = new List<AsduMessage>();
        var remaining = payload.Span;

        while (remaining.Length > 0)
        {
            var slice = remaining;
            if (!_serializer.TryRead(ref slice, out var asdu))
            {
                _logger?.LogWarning("Failed to decode ASDU payload of length {Length}.", remaining.Length);
                break;
            }

            var consumed = remaining.Length - slice.Length;
            remaining = remaining.Slice(consumed);

            if (asdu is not null)
            {
                result.Add(asdu);
            }
        }

        return result;
    }

    private ReadOnlyMemory<byte> SerializeAsdu(AsduMessage asdu)
    {
        var buffer = ArrayPool<byte>.Shared.Rent(512);
        try
        {
            var span = buffer.AsSpan();
            var written = _serializer.Write(asdu, span);
            var payload = new byte[written];
            span[..written].CopyTo(payload);
            return payload;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private static TaskCompletionSource<bool> CreateCompletionSource()
        => new(TaskCreationOptions.RunContinuationsAsynchronously);

    private static ushort IncrementSequence(ushort value)
        => (ushort)((value + 1) % SequenceModulus);

    private static int SequenceDistance(ushort newer, ushort older)
        => (newer + SequenceModulus - older) % SequenceModulus;

    public async ValueTask DisposeAsync()
    {
        _cts.Cancel();
        _outbound.Writer.TryComplete();

        if (_sendLoop is not null)
        {
            await _sendLoop.ConfigureAwait(false);
        }

        if (_receiveLoop is not null)
        {
            await _receiveLoop.ConfigureAwait(false);
        }

        if (_timerLoop is not null)
        {
            await _timerLoop.ConfigureAwait(false);
        }

        _cts.Dispose();
        _writeLock.Dispose();
    }
}

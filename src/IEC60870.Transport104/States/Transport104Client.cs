using AsduMessage = IEC60870.Core.Asdu.Asdu;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using IEC60870.Core.Abstractions;
using IEC60870.Core.Asdu;
using IEC60870.Security;
using IEC60870.Transport104.Tcp;
using Microsoft.Extensions.Logging;

namespace IEC60870.Transport104.States;

public sealed class Transport104Client : IAsyncDisposable
{
    private readonly Tcp104Connector _connector;
    private readonly IAsduSerializer _serializer;
    private readonly ISystemClock _clock;
    private readonly ApciOptions _options;
    private readonly ILoggerFactory? _loggerFactory;

    private ApciStateMachine? _stateMachine;
    private Stream? _stream;

    public event EventHandler<AsduMessage>? AsduReceived;
    public event EventHandler<Exception>? ConnectionFaulted;

    public Transport104Client(
        IAsduSerializer serializer,
        ISystemClock clock,
        ApciOptions? options = null,
        TlsClientOptions? tlsOptions = null,
        ILoggerFactory? loggerFactory = null)
    {
        _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _options = options ?? new ApciOptions();
        _connector = new Tcp104Connector(tlsOptions);
        _loggerFactory = loggerFactory;
    }

    public async Task ConnectAsync(string host, int port, CancellationToken cancellationToken)
    {
        if (_stateMachine is not null)
        {
            throw new InvalidOperationException("Client already connected.");
        }

        _stream = await _connector.ConnectAsync(host, port, cancellationToken).ConfigureAwait(false);
        var logger = _loggerFactory?.CreateLogger<ApciStateMachine>();
        _stateMachine = new ApciStateMachine(_stream, _serializer, _clock, _options, logger);
        _stateMachine.AsduReceived += HandleAsduReceived;
        _stateMachine.ConnectionFaulted += HandleConnectionFaulted;
        await _stateMachine.StartAsync(cancellationToken).ConfigureAwait(false);
    }

    public Task SendAsync(AsduMessage asdu, CancellationToken cancellationToken)
    {
        if (_stateMachine is null)
        {
            throw new InvalidOperationException("Client is not connected.");
        }

        return _stateMachine.SendAsduAsync(asdu, cancellationToken);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_stateMachine is null)
        {
            return;
        }

        await _stateMachine.StopAsync(cancellationToken).ConfigureAwait(false);
        await _stateMachine.DisposeAsync().ConfigureAwait(false);
        _stateMachine.AsduReceived -= HandleAsduReceived;
        _stateMachine.ConnectionFaulted -= HandleConnectionFaulted;
        _stateMachine = null;

        if (_stream is not null)
        {
            await _stream.DisposeAsync().ConfigureAwait(false);
            _stream = null;
        }

        await _connector.ResetAsync().ConfigureAwait(false);
    }

    private void HandleAsduReceived(object? sender, AsduMessage asdu)
    {
        AsduReceived?.Invoke(this, asdu);
    }

    private void HandleConnectionFaulted(object? sender, Exception exception)
    {
        ConnectionFaulted?.Invoke(this, exception);
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            await StopAsync(CancellationToken.None).ConfigureAwait(false);
        }
        catch
        {
        }
    }
}

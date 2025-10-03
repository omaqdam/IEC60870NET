using AsduMessage = IEC60870.Core.Asdu.Asdu;
using IEC60870.Core.Asdu;
using IEC60870.Runtime.Configuration;
using IEC60870.Transport104.States;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace IEC60870.Runtime.Services;

public sealed class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly Transport104Client _client;
    private readonly IOptionsMonitor<RuntimeOptions> _options;

    private TaskCompletionSource<Exception?> _disconnectSignal = CreateDisconnectSignal();

    public Worker(
        ILogger<Worker> logger,
        Transport104Client client,
        IOptionsMonitor<RuntimeOptions> options)
    {
        _logger = logger;
        _client = client;
        _options = options;
        _client.AsduReceived += HandleAsduReceived;
        _client.ConnectionFaulted += HandleConnectionFaulted;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var config = _options.CurrentValue;
            var host = config.Endpoint.Host;
            var port = config.Endpoint.Port;

            try
            {
                _logger.LogInformation("Connecting to IEC 60870-5-104 endpoint {Host}:{Port}...", host, port);
                _disconnectSignal = CreateDisconnectSignal();
                await _client.ConnectAsync(host, port, stoppingToken).ConfigureAwait(false);
                _logger.LogInformation("Connected to {Host}:{Port}.", host, port);

                var completed = await Task.WhenAny(_disconnectSignal.Task, Task.Delay(Timeout.Infinite, stoppingToken)).ConfigureAwait(false);
                if (completed == _disconnectSignal.Task)
                {
                    if (_disconnectSignal.Task.Result is Exception ex)
                    {
                        _logger.LogWarning(ex, "Connection fault detected, scheduling reconnect.");
                    }
                    else
                    {
                        _logger.LogInformation("Connection ended gracefully.");
                    }
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to connect to {Host}:{Port}. Retrying shortly.", host, port);
            }
            finally
            {
                try
                {
                    await _client.StopAsync(CancellationToken.None).ConfigureAwait(false);
                }
                catch (Exception stopEx)
                {
                    _logger.LogWarning(stopEx, "Error while stopping transport client.");
                }
            }

            if (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }
    }

    private void HandleAsduReceived(object? sender, AsduMessage asdu)
    {
        _logger.LogInformation("Received ASDU TypeId={TypeId} Objects={Count} Cause={Cause}", asdu.Header.TypeId, asdu.Objects.Count, asdu.Header.Cause);
    }

    private void HandleConnectionFaulted(object? sender, Exception exception)
    {
        _disconnectSignal.TrySetResult(exception);
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _disconnectSignal.TrySetResult(null);
        await base.StopAsync(cancellationToken).ConfigureAwait(false);
    }

    public override void Dispose()
    {
        _client.AsduReceived -= HandleAsduReceived;
        _client.ConnectionFaulted -= HandleConnectionFaulted;
        base.Dispose();
    }

    private static TaskCompletionSource<Exception?> CreateDisconnectSignal()
        => new(TaskCreationOptions.RunContinuationsAsynchronously);
}

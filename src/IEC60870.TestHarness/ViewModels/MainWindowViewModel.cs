using AsduMessage = IEC60870.Core.Asdu.Asdu;
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.ObjectModel;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IEC60870.App.Codecs;
using IEC60870.Core.Asdu;
using IEC60870.Core.Util;
using IEC60870.Transport104.States;

namespace IEC60870.TestHarness.ViewModels;

public sealed partial class MainWindowViewModel : ObservableObject
{
    private readonly Transport104Client _client;
    private readonly Dispatcher _dispatcher;

    [ObservableProperty]
    private string _host = "127.0.0.1";

    [ObservableProperty]
    private int _port = 2404;

    [ObservableProperty]
    private ushort _commonAddress = 1;

    [ObservableProperty]
    private string _status = "Disconnected";

    private bool _isConnected;

    public ObservableCollection<EventEntry> Events { get; } = new();

    public IAsyncRelayCommand ConnectCommand { get; }
    public IAsyncRelayCommand DisconnectCommand { get; }
    public IAsyncRelayCommand GeneralInterrogationCommand { get; }

    public MainWindowViewModel(Dispatcher dispatcher)
    {
        _dispatcher = dispatcher;
        var serializer = new AsduCodecRegistry();
        var clock = new SystemClock();
        _client = new Transport104Client(serializer, clock);
        _client.AsduReceived += ClientOnAsduReceived;
        _client.ConnectionFaulted += ClientOnConnectionFaulted;

        ConnectCommand = new AsyncRelayCommand(ConnectAsync, () => !_isConnected);
        DisconnectCommand = new AsyncRelayCommand(DisconnectAsync, () => _isConnected);
        GeneralInterrogationCommand = new AsyncRelayCommand(SendGeneralInterrogationAsync, () => _isConnected);
    }

    private async Task ConnectAsync()
    {
        await _client.ConnectAsync(Host, Port, CancellationToken.None).ConfigureAwait(false);
        _isConnected = true;
        Status = $"Connected to {Host}:{Port}";
        RaiseCanExecuteChanged();
        LogEvent("Connection", Status);
    }

    private async Task DisconnectAsync()
    {
        await _client.StopAsync(CancellationToken.None).ConfigureAwait(false);
        _isConnected = false;
        Status = "Disconnected";
        RaiseCanExecuteChanged();
        LogEvent("Connection", Status);
    }

    private async Task SendGeneralInterrogationAsync()
    {
        var header = new AsduHeader(AsduTypeId.C_IC_NA_1, 1, CauseOfTransmission.Activation, new CommonAddress(CommonAddress));
        var asdu = new AsduBuilder()
            .WithHeader(header)
            .AddObject(new InterrogationCommand(new InformationObjectAddress(0), 20))
            .Build();

        await _client.SendAsync(asdu, CancellationToken.None).ConfigureAwait(false);
        LogEvent("Command", "General interrogation sent (QOI=20).");
    }

    private void ClientOnAsduReceived(object? sender, AsduMessage asdu)
    {
        var message = $"Received {asdu.Header.TypeId} with {asdu.Objects.Count} objects (COT={asdu.Header.Cause}).";
        LogEvent("ASDU", message);
    }

    private void ClientOnConnectionFaulted(object? sender, Exception exception)
    {
        LogEvent("Fault", exception.Message);
        _dispatcher.Invoke(() =>
        {
            _isConnected = false;
            Status = "Connection faulted";
            RaiseCanExecuteChanged();
        });
    }

    private void RaiseCanExecuteChanged()
    {
        _dispatcher.Invoke(() =>
        {
            (ConnectCommand as AsyncRelayCommand)?.NotifyCanExecuteChanged();
            (DisconnectCommand as AsyncRelayCommand)?.NotifyCanExecuteChanged();
            (GeneralInterrogationCommand as AsyncRelayCommand)?.NotifyCanExecuteChanged();
        });
    }

    private void LogEvent(string category, string message)
    {
        _dispatcher.Invoke(() =>
        {
            Events.Insert(0, new EventEntry(DateTimeOffset.Now, category, message));
            Status = message;
        });
    }
}

public sealed record EventEntry(DateTimeOffset Timestamp, string Category, string Message);

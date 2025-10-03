# IEC60870.NET — Starter Repo Blueprint (SDK + Runtime)

This single document gives you a ready-to-implement **repository skeleton**, key **.NET 8** code patterns, and a minimal **WPF test harness** page to kickstart an **IEC 60870-5 (-101/-104)** library with **interoperability**, **performance**, and **service** readiness in mind.

> Copy this file into a new repo as `STARTER-REPO.md`. Each snippet includes file paths you can paste into your solution.


---

## 0) Goals & Scope

- Clean architecture split: **Core model**, **Application layer codecs**, **101 link**, **104 transport**, **Security (62351)**, **Runtime host**, **Tools**, **Test harness**.
- Interoperability-first: clause coverage matrix, golden-PCAP tests, fuzzing, and profile knobs.
- SDK + Service: simple public APIs, hot-reloadable config (SQLite + CSV import/export), structured logs/metrics.
- Includes: **basic codecs** for `M_SP_NA_1` (single point) and `M_ME_NC_1` (measured value, short float), **104 APCI state machine**, and **WPF harness** page for GI/Commands.


---

## 1) Repository Layout

```text
iec60870-dotnet/
  src/
    IEC60870.Core/
      Abstractions/
      Asdu/
      Time/
      Util/
    IEC60870.App/               # Application layer encode/decode, COT, GI/CI, Commands
      Codecs/
      Pipelines/
    IEC60870.Link101/           # FT1.2 framing, balanced/unbalanced
      Frames/
      Serial/
    IEC60870.Transport104/      # APCI (I/S/U), timers t1/t2/t3, k/w flow
      Tcp/
      Tls/
      States/
    IEC60870.Security/          # IEC 62351-3 (TLS plumping), 62351-8 stubs
    IEC60870.Runtime/           # Worker service host (Win/Linux), config, logging, metrics
    IEC60870.Tools/             # CLI: pcap replay, csv import/export, fuzz
    IEC60870.TestHarness/       # WPF app for manual interop (GI, Commands, Raw view)
  tests/
    IEC60870.UnitTests/
    IEC60870.InteropTests/
  docs/
    coverage-matrix.csv
    interop-report.md
    CHANGELOG.md
  .editorconfig
  Directory.Packages.props
  README.md
```

### 1.1 Package versions (central package management)
Create `Directory.Packages.props` at repo root:
```xml
<Project>
  <ItemGroup>
    <PackageVersion Include="Microsoft.Extensions.Hosting" Version="8.0.0" />
    <PackageVersion Include="Microsoft.Extensions.Logging.Console" Version="8.0.0" />
    <PackageVersion Include="Serilog.Extensions.Hosting" Version="8.0.0" />
    <PackageVersion Include="Serilog.Sinks.Console" Version="5.0.1" />
    <PackageVersion Include="System.IO.Pipelines" Version="8.0.0" />
    <PackageVersion Include="CommunityToolkit.Mvvm" Version="8.3.2" />
  </ItemGroup>
</Project>
```

### 1.2 Suggested target frameworks
All projects target `net8.0`; WPF targets `net8.0-windows` with `UseWPF`.


---

## 2) Core Model (IEC60870.Core)

**File:** `src/IEC60870.Core/Asdu/AsduPrimitives.cs`
```csharp
namespace IEC60870.Core.Asdu;

public enum AsduTypeId : byte
{
    // Monitoring
    M_SP_NA_1  = 1,    // Single-point
    M_ME_NC_1  = 13,   // Measured short float
    // ... add more
}

[Flags]
public enum Cot : ushort
{
    Periodic        = 1,
    BackgroundScan  = 2,
    Spontaneous     = 3,
    Initialized     = 4,
    Requested       = 5,
    Activation      = 6,
    ActivationCon   = 7,
    ActivationTerm  = 10,
    // ... add others as needed
}

public readonly record struct CommonAddress(ushort Value);
public readonly record struct IoAddress(uint Value); // typically 24-bit

public readonly record struct QualityDescriptor(byte Raw)
{
    public bool Invalid        => (Raw & 0x80) != 0;
    public bool NotTopical     => (Raw & 0x40) != 0;
    public bool Substituted    => (Raw & 0x20) != 0;
    public bool Blocked        => (Raw & 0x10) != 0;
}

public readonly record struct AsduHeader(
    AsduTypeId TypeId, byte Vsq, Cot Cause, CommonAddress Ca);
```

**File:** `src/IEC60870.Core/Asdu/Asdu.cs`
```csharp
namespace IEC60870.Core.Asdu;

public sealed class Asdu
{
    public AsduHeader Header { get; init; }
    public IReadOnlyList<InformationObject> Objects { get; init; } = Array.Empty<InformationObject>();
}

public abstract class InformationObject
{
    public IoAddress Address { get; init; }
}

public sealed class IoSinglePoint : InformationObject
{
    public bool Value { get; init; }
    public QualityDescriptor Qds { get; init; }
}

public sealed class IoMeasuredFloat : InformationObject
{
    public float Value { get; init; }
    public QualityDescriptor Qds { get; init; }
}
```

**File:** `src/IEC60870.Core/Time/Cp56Time2a.cs`
```csharp
using System.Buffers;
using System.Buffers.Binary;

namespace IEC60870.Core.Time;

public static class Cp56Time2a
{
    public static DateTimeOffset Read(ref SequenceReader<byte> r)
    {
        Span<byte> b = stackalloc byte[7];
        if (!r.TryCopyTo(b)) throw new InvalidOperationException("Not enough data");
        r.Advance(7);

        int ms    = b[0] | (b[1] << 8);
        int minute= b[2] & 0x3F;
        int hour  = b[3] & 0x1F;
        int mday  = b[4] & 0x1F;
        int month = b[5] & 0x0F;
        int year  = 2000 + (b[6] & 0x7F);

        return new DateTimeOffset(year, month, mday, hour, minute, 0, TimeSpan.Zero).AddMilliseconds(ms);
    }

    public static void Write(IBufferWriter<byte> w, DateTimeOffset ts)
    {
        var utc = ts.ToUniversalTime();
        ushort ms = (ushort)(utc.Second * 1000 + utc.Millisecond);
        Span<byte> b = w.GetSpan(7);
        b[0] = (byte)(ms & 0xFF);
        b[1] = (byte)(ms >> 8);
        b[2] = (byte)utc.Minute; // IV=0
        b[3] = (byte)utc.Hour;
        b[4] = (byte)utc.Day;
        b[5] = (byte)utc.Month;
        b[6] = (byte)(utc.Year - 2000);
        w.Advance(7);
    }
}
```


---

## 3) Application Layer (IEC60870.App) — Codec Registry + Two Basic Codecs

**File:** `src/IEC60870.App/Codecs/IAsduCodec.cs`
```csharp
using System.Buffers;

using IEC60870.Core.Asdu;

namespace IEC60870.App.Codecs;

public interface IAsduCodec
{
    AsduTypeId TypeId { get; }
    void Encode(IBufferWriter<byte> w, Asdu asdu);
    Asdu Decode(ref SequenceReader<byte> r, in AsduHeader header);
}
```

**File:** `src/IEC60870.App/Codecs/AsduCodecRegistry.cs`
```csharp
using IEC60870.Core.Asdu;

namespace IEC60870.App.Codecs;

public sealed class AsduCodecRegistry
{
    private readonly Dictionary<AsduTypeId, IAsduCodec> _codecs = new();

    public void Register(IAsduCodec codec) => _codecs[codec.TypeId] = codec;

    public IAsduCodec Get(AsduTypeId tid) => 
        _codecs.TryGetValue(tid, out var c) ? c :
        throw new NotSupportedException($"ASDU type {tid} not registered");
}
```

**File:** `src/IEC60870.App/Codecs/M_SP_NA_1_Codec.cs` (Single-point, M_SP_NA_1)
```csharp
using System.Buffers;
using System.Buffers.Binary;
using IEC60870.Core.Asdu;

namespace IEC60870.App.Codecs;

public sealed class M_SP_NA_1_Codec : IAsduCodec
{
    public AsduTypeId TypeId => AsduTypeId.M_SP_NA_1;

    public Asdu Decode(ref SequenceReader<byte> r, in AsduHeader h)
    {
        var count = h.HeaderVsqCount();
        var list = new List<InformationObject>(count);

        for (int i = 0; i < count; i++)
        {
            uint ioa = r.ReadIoAddress3(); // 24-bit IOA helper (see Util below)
            if (!r.TryRead(out byte siq)) throw new InvalidOperationException("Truncated SIQ");
            list.Add(new IoSinglePoint { Address = new(ioa), Value = (siq & 0x01) != 0, Qds = new(siq) });
        }

        return new Asdu { Header = h, Objects = list };
    }

    public void Encode(IBufferWriter<byte> w, Asdu a)
    {
        foreach (var obj in a.Objects.Cast<IoSinglePoint>())
        {
            w.WriteIoAddress3(obj.Address.Value);
            w.Write(new[] { (byte)((obj.Value ? 0x01 : 0x00) | obj.Qds.Raw) });
        }
    }
}
```

**File:** `src/IEC60870.App/Codecs/M_ME_NC_1_Codec.cs` (Float, M_ME_NC_1)
```csharp
using System.Buffers;
using IEC60870.Core.Asdu;

namespace IEC60870.App.Codecs;

public sealed class M_ME_NC_1_Codec : IAsduCodec
{
    public AsduTypeId TypeId => AsduTypeId.M_ME_NC_1;

    public Asdu Decode(ref SequenceReader<byte> r, in AsduHeader h)
    {
        var count = h.HeaderVsqCount();
        var list = new List<InformationObject>(count);
        for (int i = 0; i < count; i++)
        {
            uint ioa = r.ReadIoAddress3();
            if (!r.TryReadLittleEndian(out float value)) throw new InvalidOperationException("Truncated float");
            if (!r.TryRead(out byte qds)) throw new InvalidOperationException("Truncated QDS");
            list.Add(new IoMeasuredFloat { Address = new(ioa), Value = value, Qds = new(qds) });
        }
        return new Asdu { Header = h, Objects = list };
    }

    public void Encode(IBufferWriter<byte> w, Asdu a)
    {
        foreach (var obj in a.Objects.Cast<IoMeasuredFloat>())
        {
            w.WriteIoAddress3(obj.Address.Value);
            w.WriteLittleEndian(obj.Value);
            w.Write(new[] { obj.Qds.Raw });
        }
    }
}
```

**File:** `src/IEC60870.App/Pipelines/SequenceReaderExtensions.cs`
```csharp
using System.Buffers;
using System.Buffers.Binary;
using IEC60870.Core.Asdu;

namespace IEC60870.App.Pipelines;

public static class SequenceReaderExtensions
{
    public static uint ReadIoAddress3(this ref SequenceReader<byte> r)
    {
        Span<byte> b = stackalloc byte[3];
        if (!r.TryCopyTo(b)) throw new InvalidOperationException("Truncated IOA");
        r.Advance(3);
        return (uint)(b[0] | (b[1] << 8) | (b[2] << 16));
    }

    public static int HeaderVsqCount(this AsduHeader h) => h.Vsq & 0x7F;
}
```

**File:** `src/IEC60870.App/Pipelines/BufferWriterExtensions.cs`
```csharp
using System.Buffers;
using System.Buffers.Binary;

namespace IEC60870.App.Pipelines;

public static class BufferWriterExtensions
{
    public static void WriteIoAddress3(this IBufferWriter<byte> w, uint ioa)
    {
        var span = w.GetSpan(3);
        span[0] = (byte)(ioa & 0xFF);
        span[1] = (byte)((ioa >> 8) & 0xFF);
        span[2] = (byte)((ioa >> 16) & 0xFF);
        w.Advance(3);
    }

    public static void WriteLittleEndian(this IBufferWriter<byte> w, float value)
    {
        Span<byte> b = stackalloc byte[4];
        BinaryPrimitives.WriteSingleLittleEndian(b, value);
        w.Write(b);
    }

    public static void Write(this IBufferWriter<byte> w, ReadOnlySpan<byte> bytes)
    {
        var s = w.GetSpan(bytes.Length);
        bytes.CopyTo(s);
        w.Advance(bytes.Length);
    }
}
```


---

## 4) Transport 104 (IEC60870.Transport104) — APCI State Machine

**File:** `src/IEC60870.Transport104/States/Apci.cs`
```csharp
namespace IEC60870.Transport104.States;

public enum ApciFrameKind { I, S, U }

public readonly record struct VsVr(ushort Vs, ushort Vr);

public static class Apci
{
    public const byte Start = 0x68; // 104 start byte
    public const byte U_STARTDT_ACT = 0x07;
    public const byte U_STARTDT_CON = 0x0B;
    public const byte U_STOPDT_ACT  = 0x13;
    public const byte U_STOPDT_CON  = 0x23;
    public const byte U_TESTFR_ACT  = 0x43;
    public const byte U_TESTFR_CON  = 0x83;

    public static ApciFrameKind Classify(ReadOnlySpan<byte> apci)
    {
        // APDU: 0x68, L, APCI(4 or 6), ASDU(payload)
        // I: bit0 of byte2 == 0 ; S: byte2==0x01 ; U: byte2 & 0x03 == 0x03
        if ((apci[2] & 0x01) == 0) return ApciFrameKind.I;
        if ((apci[2] & 0x03) == 0x01) return ApciFrameKind.S;
        return ApciFrameKind.U;
    }

    public static VsVr ReadSeq(ReadOnlySpan<byte> apci)
    {
        // I/S frames carry sequence numbers in bytes 2..5 (LSB).
        ushort vs = (ushort)((apci[2] | (apci[3] << 8)) >> 1);
        ushort vr = (ushort)((apci[4] | (apci[5] << 8)) >> 1);
        return new VsVr(vs, vr);
    }

    public static void WriteI(Span<byte> apci, ushort vs, ushort vr, int asduLen)
    {
        apci[0] = Start;
        apci[1] = (byte)(asduLen + 4);
        ushort vsField = (ushort)(vs << 1);
        ushort vrField = (ushort)(vr << 1);
        apci[2] = (byte)(vsField & 0xFF);
        apci[3] = (byte)(vsField >> 8);
        apci[4] = (byte)(vrField & 0xFF);
        apci[5] = (byte)(vrField >> 8);
    }

    public static void WriteS(Span<byte> apci, ushort vr)
    {
        apci[0] = Start;
        apci[1] = 0x04;
        apci[2] = 0x01;
        apci[3] = 0x00;
        ushort vrField = (ushort)(vr << 1);
        apci[4] = (byte)(vrField & 0xFF);
        apci[5] = (byte)(vrField >> 8);
    }

    public static void WriteU(Span<byte> apci, byte uctrl)
    {
        apci[0] = Start;
        apci[1] = 0x04;
        apci[2] = uctrl;
        apci[3] = 0x00;
        apci[4] = 0x00;
        apci[5] = 0x00;
    }
}
```

**File:** `src/IEC60870.Transport104/States/ApciStateMachine.cs`
```csharp
using System.IO.Pipelines;
using System.Net.Security;
using System.Net.Sockets;
using System.Threading.Channels;

namespace IEC60870.Transport104.States;

public sealed class ApciStateMachine
{
    private readonly Pipe _pipe = new();
    private readonly Socket _socket;
    private readonly TimeSpan _t1, _t2, _t3;
    private ushort _vs, _vr;  // send/recv counters
    private readonly int _k = 12, _w = 8;
    private DateTime _lastRx = DateTime.UtcNow;
    private DateTime _lastAck = DateTime.UtcNow;

    public ApciStateMachine(Socket socket, TimeSpan t1, TimeSpan t2, TimeSpan t3)
    {
        _socket = socket;
        _t1 = t1; _t2 = t2; _t3 = t3;
    }

    // Outline only: wire reading, APCI framing, windowing, timers.
    public async Task RunAsync(Func<ReadOnlySequence<byte>, Task> asduHandler, CancellationToken ct)
    {
        _ = FillPipeAsync(ct);

        while (!ct.IsCancellationRequested)
        {
            var result = await _pipe.Reader.ReadAsync(ct);
            var buffer = result.Buffer;
            while (TryReadApdu(ref buffer, out ReadOnlySequence<byte> asdu, out bool isIFrame, out ushort vr))
            {
                _lastRx = DateTime.UtcNow;
                if (isIFrame)
                {
                    _vr++;
                    await asduHandler(asdu);
                    if (NeedAck()) await SendSAsync(_vr, ct);
                }
            }
            _pipe.Reader.AdvanceTo(buffer.Start, buffer.End);

            // Timers
            if (DateTime.UtcNow - _lastRx > _t3) await SendUAsync(Apci.U_TESTFR_ACT, ct);
            if (DateTime.UtcNow - _lastAck > _t2 && PendingAck()) await SendSAsync(_vr, ct);
        }
    }

    private bool TryReadApdu(ref ReadOnlySequence<byte> buffer, out ReadOnlySequence<byte> asdu, out bool isIFrame, out ushort vr)
    {
        asdu = default; isIFrame = false; vr = 0;
        if (buffer.Length < 6) return false;

        var reader = new SequenceReader<byte>(buffer);
        if (!reader.TryRead(out byte start) || start != Apci.Start) return false;
        if (!reader.TryRead(out byte len)) return false;
        if (!reader.TryPeek(out byte b2)) return false;
        Span<byte> apci = stackalloc byte[6];
        reader.TryCopyTo(apci);
        var kind = Apci.Classify(apci);
        var seq = Apci.ReadSeq(apci);
        isIFrame = kind == ApciFrameKind.I;
        vr = seq.Vr;
        reader.Advance(6);

        var remaining = len - 4;
        if (reader.Remaining < remaining) return false;

        var payloadStart = reader.Position;
        reader.Advance(remaining);
        var payloadEnd = reader.Position;
        asdu = buffer.Slice(payloadStart, payloadEnd);

        buffer = buffer.Slice(reader.Position);
        return true;
    }

    private bool NeedAck() => true; // placeholder: implement k/w window tracking
    private bool PendingAck() => true;

    private Task SendSAsync(ushort vr, CancellationToken ct) => Task.CompletedTask;
    private Task SendUAsync(byte ctrl, CancellationToken ct) => Task.CompletedTask;

    private async Task FillPipeAsync(CancellationToken ct)
    {
        var writer = _pipe.Writer;
        byte[] buf = new byte[8192];
        while (!ct.IsCancellationRequested)
        {
            int n = await _socket.ReceiveAsync(buf, SocketFlags.None, ct);
            if (n == 0) break;
            writer.Write(buf.AsSpan(0, n));
            var r = await writer.FlushAsync(ct);
            if (r.IsCompleted) break;
        }
        await writer.CompleteAsync();
    }
}
```

> **Note**: The state machine above is an outline focusing on APCI parsing, windows, and timers—fill in k/w backpressure, StartDT/StopDT/TestFR negotiation, and reconnect loops.


---

## 5) Runtime Host (IEC60870.Runtime)

**File:** `src/IEC60870.Runtime/Program.cs`
```csharp
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

await Host.CreateDefaultBuilder(args)
    .ConfigureLogging(lb => lb.ClearProviders().AddConsole())
    .ConfigureServices(services =>
    {
        // add hosted services, config stores, mapping services, metrics, etc.
    })
    .RunConsoleAsync();
```

Add:
- Config (SQLite + CSV import/export stubs)
- Serilog (structured logs)
- Prometheus metrics endpoints (optional)
- PCAP capture toggle (optional)


---

## 6) WPF Test Harness (IEC60870.TestHarness)

**File:** `src/IEC60870.TestHarness/IEC60870.TestHarness.csproj`
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0-windows</TargetFramework>
    <UseWPF>true</UseWPF>
    <Nullable>enable</Nullable>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="CommunityToolkit.Mvvm" />
  </ItemGroup>
</Project>
```

**File:** `src/IEC60870.TestHarness/App.xaml`
```xml
<Application x:Class="IEC60870.TestHarness.App"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             StartupUri="Views/MainWindow.xaml">
  <Application.Resources/>
</Application>
```

**File:** `src/IEC60870.TestHarness/Views/MainWindow.xaml`
```xml
<Window x:Class="IEC60870.TestHarness.Views.MainWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        Title="IEC 60870 Test Harness" Height="540" Width="860">
  <Grid Margin="12">
    <Grid.RowDefinitions>
      <RowDefinition Height="Auto"/>
      <RowDefinition Height="*"/>
      <RowDefinition Height="Auto"/>
    </Grid.RowDefinitions>

    <StackPanel Orientation="Horizontal" Spacing="8">
      <Button Content="Connect 104" Width="110" Margin="0,0,8,0" Click="Connect104_Click"/>
      <Button Content="GI (C_IC_NA_1)" Width="130" Margin="0,0,8,0" Click="Gi_Click"/>
      <Button Content="SBO Close" Width="110" Margin="0,0,8,0" Click="SboClose_Click"/>
      <Button Content="SBO Open" Width="110" Margin="0,0,8,0" Click="SboOpen_Click"/>
    </StackPanel>

    <DataGrid Grid.Row="1" x:Name="EventsGrid" AutoGenerateColumns="True" IsReadOnly="True"/>
    <TextBlock Grid.Row="2" x:Name="StatusText" Margin="0,8,0,0"/>
  </Grid>
</Window>
```

**File:** `src/IEC60870.TestHarness/Views/MainWindow.xaml.cs`
```csharp
using System.Collections.ObjectModel;
using System.Net.Sockets;
using System.Windows;

namespace IEC60870.TestHarness.Views;

public partial class MainWindow : Window
{
    private readonly ObservableCollection<object> _events = new();
    public MainWindow()
    {
        InitializeComponent();
        EventsGrid.ItemsSource = _events;
    }

    private async void Connect104_Click(object sender, RoutedEventArgs e)
    {
        StatusText.Text = "Connecting...";
        // TODO: create TcpClient, wrap in ApciStateMachine, wire asduHandler -> _events.Add(...)
        await Task.Delay(200);
        StatusText.Text = "Connected.";
    }

    private void Gi_Click(object sender, RoutedEventArgs e)
    {
        // TODO: send C_IC_NA_1 (General Interrogation)
        StatusText.Text = "GI sent.";
    }

    private void SboClose_Click(object sender, RoutedEventArgs e)
    {
        // TODO: select-before-operate CLOSE
    }

    private void SboOpen_Click(object sender, RoutedEventArgs e)
    {
        // TODO: select-before-operate OPEN
    }
}
```


---

## 7) Build & Run

```bash
# 1) Create solution and projects
dotnet new sln -n IEC60870
dotnet new classlib -n IEC60870.Core -o src/IEC60870.Core
dotnet new classlib -n IEC60870.App -o src/IEC60870.App
dotnet new classlib -n IEC60870.Link101 -o src/IEC60870.Link101
dotnet new classlib -n IEC60870.Transport104 -o src/IEC60870.Transport104
dotnet new classlib -n IEC60870.Security -o src/IEC60870.Security
dotnet new worker -n IEC60870.Runtime -o src/IEC60870.Runtime
dotnet new wpf -n IEC60870.TestHarness -o src/IEC60870.TestHarness

# 2) Add projects to solution
dotnet sln IEC60870.sln add src/**/**.csproj

# 3) Set references (App depends on Core; Transport104 & Link101 depend on Core & App; TestHarness depends on App & Transport104)
dotnet add src/IEC60870.App/IEC60870.App.csproj reference src/IEC60870.Core/IEC60870.Core.csproj
dotnet add src/IEC60870.Transport104/IEC60870.Transport104.csproj reference src/IEC60870.Core/IEC60870.Core.csproj src/IEC60870.App/IEC60870.App.csproj
dotnet add src/IEC60870.Link101/IEC60870.Link101.csproj reference src/IEC60870.Core/IEC60870.Core.csproj src/IEC60870.App/IEC60870.App.csproj
dotnet add src/IEC60870.TestHarness/IEC60870.TestHarness.csproj reference src/IEC60870.Transport104/IEC60870.Transport104.csproj src/IEC60870.App/IEC60870.App.csproj src/IEC60870.Core/IEC60870.Core.csproj

# 4) Restore & build
dotnet restore
dotnet build -c Debug
```

> You can paste the code snippets into the indicated files and the solution should compile once you fill in small glue code (e.g., namespaces, partial usings).


---

## 8) Next Steps (to reach first interop)

- Implement `StartDT/StopDT/TestFR` handshake and reconnect loop in `ApciStateMachine`.
- Add ASDU header encoder/decoder (Type/VSQ/COT/CA) and integrate with registry.
- Implement a minimal ASDU publisher to the WPF grid (e.g., single point changes, floats).
- Add `C_IC_NA_1` GI request builder; handle segmented responses.
- Introduce config profiles (k/w, t1/t2/t3, CA/IOA sizes) and a simple CSV import for mapping.
- Write round-trip unit tests for `M_SP_NA_1` and `M_ME_NC_1` including edge qualities.
- Plan golden-PCAP tests against your target peer (lib60870, device sim, or field RTU).


---

## 9) Compliance & Interop Checklist (mini)

- [ ] CP56Time2a conforms (millisecond, leap years); no TZ applied on wire.
- [ ] 104 APCI windows (`k/w`) strictly enforced; ack on `t2` or when window full.
- [ ] `t1/t2/t3` timers: abort on `t1`, delayed ack on `t2`, periodic `TESTFR` on `t3`.
- [ ] GI flow correct; large datasets segmented; COT transitions accurate.
- [ ] SBO/select-then-operate timing windows and negative confirms handled.
- [ ] Quality bits preserved across all types; IV/NT/SB/BL carried through.
- [ ] Robust against noise/truncation (fuzz tests) and can resync.
- [ ] TLS optional (62351-3); cipher policy configurable; cert chains validated.


---

## 10) License & Notes

- This blueprint is unlicensed sample content. Apply your preferred license (MIT/BSD/Apache-2.0).
- IEC standards are copyrighted—ensure you (or your company) hold the relevant specs for dev/testing.
- Security (62351) is implementation-dependent; validate with your customer’s policy.

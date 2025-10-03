# IEC60870.NET

IEC60870.NET is a production-ready .NET 8 implementation of core IEC 60870-5 functionality, including reusable codecs, an IEC 104 transport stack, a background service runtime, and a lightweight WPF test harness.

## Features

- **Core library**: strongly typed ASDU models, CP56Time2a handling, and serialization utilities.
- **Application codecs**: support for monitoring data (`M_SP_NA_1`, `M_ME_NC_1`), time-tagged events (`M_SP_TB_1`), and control commands (`C_IC_NA_1`, `C_DC_NA_1`).
- **Transport 104**: APCI state machine with timer management (t1/t2/t3), window enforcement (k/w), and automatic TestFR handling.
- **Link-layer 101**: FT1.2 variable-length frame encode/decode helpers for serial integrations.
- **TLS-ready**: pluggable IEC 62351-3 client options using `SslStream`.
- **Runtime service**: Worker-based host that manages lifecycle, reconnect, logging, and configuration binding.
- **Test harness**: WPF desktop client for manual interoperability tests (connect, disconnect, GI commands, live ASDU log).
- **Unit tests**: round-trip coverage for key codecs, FT1.2 framing, and CP56Time2a conversion.

## Repository Layout

```
src/
  IEC60870.Core/
  IEC60870.App/
  IEC60870.Transport104/
  IEC60870.Security/
  IEC60870.Runtime/
  IEC60870.TestHarness/
  IEC60870.Link101/
tests/
  IEC60870.UnitTests/
  IEC60870.InteropTests/
```

## Getting Started

```powershell
# Restore and build everything
dotnet restore
dotnet build -c Release

# Run unit tests
dotnet test tests/IEC60870.UnitTests

# Launch runtime service (configure appsettings.json as needed)
dotnet run --project src/IEC60870.Runtime

# Start the WPF test harness
dotnet build src/IEC60870.TestHarness
start src/IEC60870.TestHarness/bin/Debug/net8.0-windows/IEC60870.TestHarness.exe
```

## Configuration

`IEC60870.Runtime` binds configuration from the `IEC60870` section. Example `appsettings.json`:

```json
{
  "IEC60870": {
    "Endpoint": {
      "Host": "127.0.0.1",
      "Port": 2404
    },
    "Transport104": {
      "T1Milliseconds": 15000,
      "T2Milliseconds": 10000,
      "T3Milliseconds": 20000,
      "KWindow": 12,
      "WWindow": 8
    },
    "Security": {
      "EnableTls": false,
      "TargetHost": ""
    }
  }
}
```

## Roadmap

- Expand codec coverage for additional information object types.
- Implement IEC 101 FT1.2 framing and serial transport.
- Add golden PCAP playback and fuzzing tools under `IEC60870.Tools`.
- Provide golden-path interoperability scenarios in `IEC60870.InteropTests`.

## Continuous Integration

An automated GitHub Actions workflow (`.github/workflows/dotnet-ci.yml`) restores, builds, and tests the full solution on every push and pull request targeting `main`.

## License

MIT (to be confirmed with stakeholders).

# Blueprint Execution Status

## Repository Skeleton
- [x] Solution IEC60870.sln with core, transport, runtime, security, tools placeholders, test harness, and tests per blueprint Layout.
- [x] Central package management via Directory.Packages.props.

## Core Model & Utilities
- [x] ASDU primitives, strongly typed records, and builder (IEC60870.Core).
- [x] CP56Time2a encoder/decoder and span utilities.

## Application Layer Codecs
- [x] Codec registry with implementations for M_SP_NA_1, M_ME_NC_1, C_IC_NA_1.

## Transport 101/104 & Security
- [ ] IEC 101 FT1.2 framing implementation (skeleton present in IEC60870.Link101).
- [x] IEC 104 APCI state machine with timers, k/w enforcement, Start/Stop/Test, TLS-capable connector.
- [ ] IEC 62351 security enhancements beyond TLS options.

## Runtime & Tooling
- [x] Worker-service runtime with reconnect loop, configuration binding, logging.
- [ ] CLI tooling for PCAP replay / CSV import / fuzzing (placeholder project only).

## Test Harness & Testing
- [x] WPF test harness page for manage GI and live event log.
- [x] Unit test suite for codec round-trips and APCI framing.
- [ ] Automated interop tests (placeholder with skipped test).
- [ ] Golden PCAP or fuzzing workflows.

## Documentation & CI
- [x] README with getting-started/build/test guidance.
- [x] Coverage matrix and interop report stubs.
- [x] CI pipeline (GitHub Actions) for restore/build/test.

## Next Steps (tracked in docs/project-next-steps.md)
- Broaden codec coverage (double commands, time-tagged monitors, etc.).
- Flesh out IEC 101 framing/serial transport beyond skeleton.
- Add automated interop harnesses or golden PCAP replay under 	ests/IEC60870.InteropTests.
- Implement CLI tooling and security hardening.

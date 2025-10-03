using System;
using FluentAssertions;
using IEC60870.App.Codecs;
using IEC60870.Core.Asdu;
using IEC60870.Core.Time;
using Xunit;

namespace IEC60870.UnitTests;

public sealed class AsduCodecTests
{
    [Fact]
    public void EncodeDecodeSinglePointInformation()
    {
        var serializer = new AsduCodecRegistry();
        var header = new AsduHeader(AsduTypeId.M_SP_NA_1, 1, CauseOfTransmission.Spontaneous, new CommonAddress(1));
        var asdu = new AsduBuilder()
            .WithHeader(header)
            .AddObject(new SinglePointInformation(new InformationObjectAddress(42), true, new QualityDescriptor(0x10)))
            .Build();

        Span<byte> buffer = stackalloc byte[32];
        var written = serializer.Write(asdu, buffer);

        var span = (ReadOnlySpan<byte>)buffer[..written];
        serializer.TryRead(ref span, out var decoded).Should().BeTrue();
        decoded.Should().NotBeNull();
        decoded!.Header.TypeId.Should().Be(AsduTypeId.M_SP_NA_1);
        var spi = decoded.Objects[0].Should().BeOfType<SinglePointInformation>().Subject;
        spi.Value.Should().BeTrue();
        spi.Quality.Blocked.Should().BeTrue();
    }

    [Fact]
    public void EncodeDecodeDoublePointInformation()
    {
        var serializer = new AsduCodecRegistry();
        var header = new AsduHeader(AsduTypeId.M_DP_NA_1, 1, CauseOfTransmission.Spontaneous, new CommonAddress(1));
        var asdu = new AsduBuilder()
            .WithHeader(header)
            .AddObject(new DoublePointInformation(new InformationObjectAddress(17), DoublePointState.On, new QualityDescriptor(0x80)))
            .Build();

        Span<byte> buffer = stackalloc byte[16];
        var written = serializer.Write(asdu, buffer);
        var span = (ReadOnlySpan<byte>)buffer[..written];
        serializer.TryRead(ref span, out var decoded).Should().BeTrue();
        var dpi = decoded!.Objects[0].Should().BeOfType<DoublePointInformation>().Subject;
        dpi.State.Should().Be(DoublePointState.On);
        dpi.Quality.Invalid.Should().BeTrue();
    }

    [Fact]
    public void EncodeDecodeMeasuredValueShortFloat()
    {
        var serializer = new AsduCodecRegistry();
        var header = new AsduHeader(AsduTypeId.M_ME_NC_1, 1, CauseOfTransmission.BackgroundScan, new CommonAddress(2));
        var asdu = new AsduBuilder()
            .WithHeader(header)
            .AddObject(new MeasuredValueShortFloat(new InformationObjectAddress(7), 12.34f, new QualityDescriptor(0x00)))
            .Build();

        Span<byte> buffer = stackalloc byte[32];
        var written = serializer.Write(asdu, buffer);

        var span = (ReadOnlySpan<byte>)buffer[..written];
        serializer.TryRead(ref span, out var decoded).Should().BeTrue();
        var mv = decoded!.Objects[0].Should().BeOfType<MeasuredValueShortFloat>().Subject;
        mv.Value.Should().BeApproximately(12.34f, 0.0001f);
    }

    [Fact]
    public void EncodeDecodeGeneralInterrogation()
    {
        var serializer = new AsduCodecRegistry();
        var header = new AsduHeader(AsduTypeId.C_IC_NA_1, 1, CauseOfTransmission.Activation, new CommonAddress(3));
        var asdu = new AsduBuilder()
            .WithHeader(header)
            .AddObject(new InterrogationCommand(new InformationObjectAddress(0), 20))
            .Build();

        Span<byte> buffer = stackalloc byte[16];
        var written = serializer.Write(asdu, buffer);
        var span = (ReadOnlySpan<byte>)buffer[..written];
        serializer.TryRead(ref span, out var decoded).Should().BeTrue();
        var command = decoded!.Objects[0].Should().BeOfType<InterrogationCommand>().Subject;
        command.Qualifier.Should().Be(20);
    }

    [Fact]
    public void EncodeDecodeSingleCommand()
    {
        var serializer = new AsduCodecRegistry();
        var header = new AsduHeader(AsduTypeId.C_SC_NA_1, 1, CauseOfTransmission.Activation, new CommonAddress(5));
        var asdu = new AsduBuilder()
            .WithHeader(header)
            .AddObject(new SingleCommandInformation(new InformationObjectAddress(12), value: true, select: true, new QualityDescriptor(0x00)))
            .Build();

        Span<byte> buffer = stackalloc byte[16];
        var written = serializer.Write(asdu, buffer);
        var span = (ReadOnlySpan<byte>)buffer[..written];
        serializer.TryRead(ref span, out var decoded).Should().BeTrue();
        var sc = decoded!.Objects[0].Should().BeOfType<SingleCommandInformation>().Subject;
        sc.Value.Should().BeTrue();
        sc.Select.Should().BeTrue();
        sc.Quality.NotTopical.Should().BeFalse();
    }

    [Fact]
    public void EncodeDecodeDoubleCommand()
    {
        var serializer = new AsduCodecRegistry();
        var header = new AsduHeader(AsduTypeId.C_DC_NA_1, 1, CauseOfTransmission.Activation, new CommonAddress(10));
        var asdu = new AsduBuilder()
            .WithHeader(header)
            .AddObject(new DoubleCommandInformation(new InformationObjectAddress(5), DoubleCommandState.On, select: true, new QualityDescriptor(0x10)))
            .Build();

        Span<byte> buffer = stackalloc byte[16];
        var written = serializer.Write(asdu, buffer);
        var span = (ReadOnlySpan<byte>)buffer[..written];
        serializer.TryRead(ref span, out var decoded).Should().BeTrue();
        var command = decoded!.Objects[0].Should().BeOfType<DoubleCommandInformation>().Subject;
        command.State.Should().Be(DoubleCommandState.On);
        command.Select.Should().BeTrue();
        command.Quality.Blocked.Should().BeTrue();
    }

    [Fact]
    public void EncodeDecodeTimeTaggedSinglePoint()
    {
        var serializer = new AsduCodecRegistry();
        var header = new AsduHeader(AsduTypeId.M_SP_TB_1, 1, CauseOfTransmission.Spontaneous, new CommonAddress(11));
        var timestamp = new DateTimeOffset(2025, 3, 15, 6, 30, 10, TimeSpan.Zero).AddMilliseconds(250);
        var asdu = new AsduBuilder()
            .WithHeader(header)
            .AddObject(new TimeTaggedSinglePointInformation(new InformationObjectAddress(77), true, new QualityDescriptor(0x80), timestamp))
            .Build();

        Span<byte> buffer = stackalloc byte[32];
        var written = serializer.Write(asdu, buffer);
        var span = (ReadOnlySpan<byte>)buffer[..written];
        serializer.TryRead(ref span, out var decoded).Should().BeTrue();
        var info = decoded!.Objects[0].Should().BeOfType<TimeTaggedSinglePointInformation>().Subject;
        info.Value.Should().BeTrue();
        info.Timestamp.Should().Be(timestamp);
        info.Quality.Invalid.Should().BeTrue();
    }

    [Fact]
    public void Cp56Time2aRoundTrip()
    {
        var original = new DateTimeOffset(2024, 12, 31, 23, 59, 58, TimeSpan.Zero).AddMilliseconds(750);
        var cp = new Cp56Time2a(original);
        Span<byte> buffer = stackalloc byte[7];
        cp.WriteBytes(buffer);
        var decoded = Cp56Time2a.FromBytes(buffer);
        decoded.Timestamp.Should().Be(original);
    }
}


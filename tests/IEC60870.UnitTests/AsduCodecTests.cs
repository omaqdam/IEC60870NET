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
        written.Should().BeGreaterThan(0);

        var slice = buffer[..written];
        var readSpan = (ReadOnlySpan<byte>)slice;
        serializer.TryRead(ref readSpan, out var decoded).Should().BeTrue();
        decoded.Should().NotBeNull();
        decoded!.Header.TypeId.Should().Be(AsduTypeId.M_SP_NA_1);
        decoded.Objects.Should().ContainSingle();
        var spi = decoded.Objects[0].Should().BeOfType<SinglePointInformation>().Subject;
        spi.Value.Should().BeTrue();
        spi.Quality.Invalid.Should().BeFalse();
        spi.Quality.Blocked.Should().BeTrue();
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

        var slice = buffer[..written];
        var readSpan = (ReadOnlySpan<byte>)slice;
        serializer.TryRead(ref readSpan, out var decoded).Should().BeTrue();
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
        var slice = buffer[..written];
        var readSpan = (ReadOnlySpan<byte>)slice;
        serializer.TryRead(ref readSpan, out var decoded).Should().BeTrue();
        var command = decoded!.Objects[0].Should().BeOfType<InterrogationCommand>().Subject;
        command.Qualifier.Should().Be(20);
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

using System.Buffers;
using FluentAssertions;
using IEC60870.Link101.Frames;
using Xunit;

namespace IEC60870.UnitTests;

public sealed class Ft12FrameTests
{
    [Fact]
    public void Ft12EncodeDecodeRoundTrip()
    {
        var userData = new byte[] { 0x65, 0x66, 0x67 };
        var frame = Ft12Frame.Create(control: 0x13, address: 0x0201, userData);

        var writer = new ArrayBufferWriter<byte>();
        frame.WriteTo(writer);

        var span = writer.WrittenSpan;
        Ft12Frame.TryParse(span, out var parsed, out var consumed).Should().BeTrue();
        consumed.Should().Be(span.Length);
        parsed.Should().NotBeNull();
        parsed!.Control.Should().Be(0x13);
        parsed.Address.Should().Be(0x0201);
        parsed.UserData.ToArray().Should().Equal(userData);
    }

    [Fact]
    public void Ft12ParseDetectsChecksumError()
    {
        var userData = new byte[] { 0x01 };
        var frame = Ft12Frame.Create(0x33, 0x0001, userData);
        var writer = new ArrayBufferWriter<byte>();
        frame.WriteTo(writer);

        var bytes = writer.WrittenSpan.ToArray();
        bytes[5] ^= 0xFF; // break checksum area

        Ft12Frame.TryParse(bytes, out var parsed, out var consumed).Should().BeFalse();
        parsed.Should().BeNull();
        consumed.Should().Be(0);
    }
}

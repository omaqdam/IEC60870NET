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
        bytes[7] ^= 0xFF; // corrupt payload -> checksum mismatch

        Ft12Frame.TryParse(bytes, out var parsed, out var consumed).Should().BeFalse();
        parsed.Should().BeNull();
        consumed.Should().Be(0);
    }

    [Fact]
    public void Ft12FrameParserHandlesFragmentedInput()
    {
        var frame = Ft12Frame.Create(0x46, 0x1002, new byte[] { 0xAA, 0xBB });
        var writer = new ArrayBufferWriter<byte>();
        frame.WriteTo(writer);
        var bytes = writer.WrittenSpan.ToArray();

        var parser = new Ft12FrameParser();
        parser.Append(bytes.AsSpan(0, 3));
        parser.TryReadFrame(out _).Should().BeFalse();

        parser.Append(bytes.AsSpan(3));
        parser.TryReadFrame(out var parsed).Should().BeTrue();
        parsed.Should().NotBeNull();
        parsed!.Control.Should().Be(0x46);
        parsed.Address.Should().Be(0x1002);
        parsed.UserData.ToArray().Should().Equal(new byte[] { 0xAA, 0xBB });
    }
}

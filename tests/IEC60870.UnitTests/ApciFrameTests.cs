using System.Buffers;
using FluentAssertions;
using IEC60870.Transport104.States;
using Xunit;

namespace IEC60870.UnitTests;

public sealed class ApciFrameTests
{
    [Fact]
    public void IFrameRoundTrip()
    {
        var payload = new byte[] { 0x01, 0x02, 0x03 };
        var frame = ApciFrame.CreateIFrame(5, 10, payload);
        var writer = new ArrayBufferWriter<byte>();
        frame.WriteTo(writer);

        var sequence = new ReadOnlySequence<byte>(writer.WrittenMemory);
        ApciFrame.TryParse(ref sequence, out var parsed).Should().BeTrue();
        parsed.Should().NotBeNull();
        parsed!.Type.Should().Be(ApciFrameType.I);
        parsed.SendSequence.Should().Be(5);
        parsed.ReceiveSequence.Should().Be(10);
        parsed.Payload.ToArray().Should().Equal(payload);
    }

    [Fact]
    public void UFrameSerializesCorrectly()
    {
        var frame = ApciFrame.CreateUFrame(UFrameCommand.StartDtAct);
        var writer = new ArrayBufferWriter<byte>();
        frame.WriteTo(writer);
        writer.WrittenSpan[0].Should().Be(0x68);
        writer.WrittenSpan[2].Should().Be((byte)UFrameCommand.StartDtAct);
    }
}

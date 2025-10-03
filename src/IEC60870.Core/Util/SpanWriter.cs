using System.Buffers.Binary;

namespace IEC60870.Core.Util;

public ref struct SpanWriter
{
    private Span<byte> _buffer;
    private int _position;

    public SpanWriter(Span<byte> destination)
    {
        _buffer = destination;
        _position = 0;
    }

    public int Position => _position;

    public void WriteByte(byte value)
    {
        EnsureAvailable(1);
        _buffer[_position++] = value;
    }

    public void WriteBytes(ReadOnlySpan<byte> source)
    {
        EnsureAvailable(source.Length);
        source.CopyTo(_buffer[_position..]);
        _position += source.Length;
    }

    public void WriteUInt16(ushort value)
    {
        EnsureAvailable(2);
        _buffer[_position++] = (byte)(value & 0xFF);
        _buffer[_position++] = (byte)(value >> 8);
    }

    public void WriteUInt24(uint value)
    {
        EnsureAvailable(3);
        _buffer[_position++] = (byte)(value & 0xFF);
        _buffer[_position++] = (byte)((value >> 8) & 0xFF);
        _buffer[_position++] = (byte)((value >> 16) & 0xFF);
    }

    public void WriteSingle(float value)
    {
        EnsureAvailable(4);
        BinaryPrimitives.WriteSingleLittleEndian(_buffer[_position..], value);
        _position += 4;
    }

    private void EnsureAvailable(int count)
    {
        if (_position + count > _buffer.Length)
        {
            throw new InvalidOperationException("SpanWriter buffer exhausted.");
        }
    }
}

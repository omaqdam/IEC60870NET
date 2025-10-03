namespace IEC60870.Core.Util;

public ref struct SpanReader
{
    private ReadOnlySpan<byte> _buffer;
    private int _position;

    public SpanReader(ReadOnlySpan<byte> buffer)
    {
        _buffer = buffer;
        _position = 0;
    }

    public int Remaining => _buffer.Length - _position;

    public byte ReadByte()
    {
        EnsureAvailable(1);
        return _buffer[_position++];
    }

    public ushort ReadUInt16()
    {
        EnsureAvailable(2);
        var value = (ushort)(_buffer[_position] | (_buffer[_position + 1] << 8));
        _position += 2;
        return value;
    }

    public uint ReadUInt24()
    {
        EnsureAvailable(3);
        var value = (uint)(_buffer[_position] | (_buffer[_position + 1] << 8) | (_buffer[_position + 2] << 16));
        _position += 3;
        return value;
    }

    public ReadOnlySpan<byte> ReadSpan(int count)
    {
        EnsureAvailable(count);
        var slice = _buffer.Slice(_position, count);
        _position += count;
        return slice;
    }

    private void EnsureAvailable(int count)
    {
        if (_position + count > _buffer.Length)
        {
            throw new InvalidOperationException("SpanReader buffer exhausted.");
        }
    }
}

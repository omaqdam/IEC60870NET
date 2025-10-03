using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace IEC60870.Link101.Frames;

public sealed class Ft12FrameParser
{
    private readonly List<byte> _buffer = new();

    public void Append(ReadOnlySpan<byte> data)
    {
        if (data.IsEmpty)
        {
            return;
        }

        _buffer.AddRange(data.ToArray());
    }

    public bool TryReadFrame(out Ft12Frame? frame)
    {
        frame = null;
        if (_buffer.Count < 6)
        {
            return false;
        }

        var span = CollectionsMarshal.AsSpan(_buffer);
        if (!Ft12Frame.TryParse(span, out var parsed, out var consumed) || parsed is null)
        {
            return false;
        }

        _buffer.RemoveRange(0, consumed);
        frame = parsed;
        return true;
    }

    public void Clear() => _buffer.Clear();
}

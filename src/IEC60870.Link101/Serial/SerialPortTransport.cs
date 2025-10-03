using System.IO;
using System.IO.Ports;
using System.Threading;
using System.Threading.Tasks;

namespace IEC60870.Link101.Serial;

public sealed class SerialPortTransport : IAsyncDisposable
{
    private readonly SerialPortOptions _options;
    private SerialPort? _serialPort;

    public SerialPortTransport(SerialPortOptions options)
    {
        _options = options;
    }

    public Stream Open()
    {
        if (_serialPort is not null)
        {
            throw new InvalidOperationException("Serial port already opened.");
        }

        _serialPort = new SerialPort(_options.PortName)
        {
            BaudRate = _options.BaudRate,
            DataBits = _options.DataBits,
            Parity = (System.IO.Ports.Parity)_options.Parity,
            StopBits = (System.IO.Ports.StopBits)_options.StopBits,
            Handshake = _options.Handshake ? Handshake.RequestToSend : Handshake.None,
            ReadTimeout = 1000,
            WriteTimeout = 1000
        };

        _serialPort.Open();
        return new SerialPortStream(_serialPort);
    }

    public async ValueTask DisposeAsync()
    {
        if (_serialPort is not null)
        {
            await Task.Run(() => _serialPort.Dispose()).ConfigureAwait(false);
            _serialPort = null;
        }
    }

    private sealed class SerialPortStream : Stream
    {
        private readonly SerialPort _serialPort;

        public SerialPortStream(SerialPort serialPort)
        {
            _serialPort = serialPort;
        }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

        public override int Read(byte[] buffer, int offset, int count)
            => _serialPort.Read(buffer, offset, count);

        public override void Write(byte[] buffer, int offset, int count)
            => _serialPort.Write(buffer, offset, count);

        public override void Flush() => _serialPort.BaseStream.Flush();

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _serialPort.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}

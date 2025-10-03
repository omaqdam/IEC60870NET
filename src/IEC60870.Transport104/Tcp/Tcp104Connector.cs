using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using IEC60870.Security;

namespace IEC60870.Transport104.Tcp;

public sealed class Tcp104Connector : IAsyncDisposable
{
    private readonly TlsClientOptions _tlsOptions;
    private TcpClient? _tcpClient;
    private Stream? _stream;

    public Tcp104Connector(TlsClientOptions? tlsOptions = null)
    {
        _tlsOptions = tlsOptions ?? new TlsClientOptions();
    }

    public async Task<Stream> ConnectAsync(string host, int port, CancellationToken cancellationToken)
    {
        if (_tcpClient is not null)
        {
            throw new InvalidOperationException("Connector already in use.");
        }

        _tcpClient = new TcpClient();
        await _tcpClient.ConnectAsync(host, port, cancellationToken).ConfigureAwait(false);
        _stream = _tcpClient.GetStream();

        if (_tlsOptions.Enabled)
        {
            if (string.IsNullOrWhiteSpace(_tlsOptions.TargetHost))
            {
                throw new InvalidOperationException("TLS requires a target host for certificate validation.");
            }

            var sslStream = new SslStream(_stream, leaveInnerStreamOpen: false, _tlsOptions.RemoteCertificateValidationCallback);
            await sslStream.AuthenticateAsClientAsync(new SslClientAuthenticationOptions
            {
                TargetHost = _tlsOptions.TargetHost,
                ClientCertificates = _tlsOptions.ClientCertificates,
                EnabledSslProtocols = _tlsOptions.Protocols,
                CertificateRevocationCheckMode = X509RevocationMode.Online
            }, cancellationToken).ConfigureAwait(false);
            _stream = sslStream;
        }

        return _stream;
    }

    public async ValueTask ResetAsync()
    {
        if (_stream is not null)
        {
            await _stream.DisposeAsync().ConfigureAwait(false);
            _stream = null;
        }

        _tcpClient?.Dispose();
        _tcpClient = null;
    }

    public async ValueTask DisposeAsync()
    {
        await ResetAsync().ConfigureAwait(false);
    }
}

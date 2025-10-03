using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;

namespace IEC60870.Security;

public sealed class TlsClientOptions
{
    public bool Enabled { get; init; }
    public string TargetHost { get; init; } = string.Empty;
    public X509CertificateCollection? ClientCertificates { get; init; }
    public RemoteCertificateValidationCallback? RemoteCertificateValidationCallback { get; init; }
    public SslProtocols Protocols { get; init; } = SslProtocols.Tls12 | SslProtocols.Tls13;
}

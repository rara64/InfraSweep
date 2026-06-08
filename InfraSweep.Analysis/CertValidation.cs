using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography;
using InfraSweep.Analysis.Exceptions;

namespace InfraSweep.Analysis;

public class CertValidation
{
    private static readonly Dictionary<string, byte[]> PinnedCertificateAuthorities = new()
    {
        {"vulnerability.circl.lu", Convert.FromHexString("59e738e674221702af1edb87c5200c1a4b75f64fae3d2c3d265124c61bd83c79")},
        {"cpe-guesser.cve-search.org", Convert.FromHexString("025490860b498ab73c6a12f27a49ad5fe230fafe3ac8f6112c9b7d0aad46941d")}
    };

    public static bool ServerCertificateValidation(
        HttpRequestMessage request,
        X509Certificate2? certificate,
        X509Chain? certificateChain,
        SslPolicyErrors sslPolicyErrors)
    {
        if (certificate != null 
                && request.RequestUri != null
                && sslPolicyErrors == SslPolicyErrors.None
                && PinnedCertificateAuthorities.TryGetValue(request.RequestUri.Host, out var pin))
        {

            using X509Chain chain = certificateChain ?? new X509Chain();
            chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck; // Needed for macOS
            chain.ChainPolicy.UrlRetrievalTimeout = TimeSpan.FromSeconds(5);

            string certDnsName = certificate.GetNameInfo(X509NameType.DnsName, false);

            if (!IsDnsMatch(certDnsName, request.RequestUri.Host))
                throw new CertPinException();

            if (!chain.Build(certificate))
                throw new CertPinException();

            foreach (X509ChainElement element in chain.ChainElements)
            {
                byte[] publicKey = element.Certificate.PublicKey.ExportSubjectPublicKeyInfo();
                byte[] hash = SHA256.HashData(publicKey);

                if (hash.SequenceEqual(pin))
                    return true;
            }
        }

        throw new CertPinException();
    }

    private static bool IsDnsMatch(string certDns, string host)
    {
        if (certDns.Equals(host, StringComparison.OrdinalIgnoreCase)) return true;
        if (certDns.StartsWith("*.") && host.EndsWith(certDns[1..], StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }
}
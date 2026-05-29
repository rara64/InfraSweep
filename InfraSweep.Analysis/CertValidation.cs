using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography;

namespace InfraSweep.Analysis;

public class CertValidation
{
    private static readonly Dictionary<string, byte[]> PinnedCertificateAuthorities = new()
    {
        {"vulnerability.circl.lu", Convert.FromHexString("981ceae14bc8103800db606f5bf9950586538674bb55b9bb61d3ee71ada20b1d")},
        {"cpe-guesser.cve-search.org", Convert.FromHexString("2f713f274f44e7f7d9bddd8d0ad397ee4271f6cb5c651a5f1416537c9df5ef57")}
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
            chain.ChainPolicy.RevocationMode = X509RevocationMode.Online;
            chain.ChainPolicy.UrlRetrievalTimeout = TimeSpan.FromSeconds(5);

            if (!chain.Build(certificate))
                return false;

            byte[] subjectPublicKeyInfo = certificate.PublicKey.ExportSubjectPublicKeyInfo();

            return SHA256.HashData(subjectPublicKeyInfo).SequenceEqual(pin);
        }

        return false;
    }
}
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace ME221Dashboard.Services.Mirror;

/// <summary>
/// A's self-signed WSS certificate (R15, R20, KTD-1). Generated and persisted on first
/// run as <c>companion-cert.pfx</c> under the app data directory; pairing-reset
/// regenerates it, which invalidates every issued device token (R20).
/// </summary>
public sealed class MirrorCertificateProvider(ILogger<MirrorCertificateProvider>? logger = null)
{
    internal const string PfxPassword = "me221-mirror";
    internal const string SubjectName = "CN=ME221 Mirror";

    private readonly ILogger<MirrorCertificateProvider> _logger = logger ?? NullLogger<MirrorCertificateProvider>.Instance;
    private X509Certificate2? _cached;
    private string _basePath = DefaultBasePath;

    private static string DefaultBasePath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ME221");

    public string CertificatePath => Path.Combine(_basePath, "companion-cert.pfx");

    /// <summary>Test seam: point the provider at a temp directory.</summary>
    public void SetBasePath(string basePath) => _basePath = basePath;

    /// <summary>True when no certificate file exists yet on disk.</summary>
    public bool CertificateFileExists => File.Exists(CertificatePath);

    /// <summary>Load the persisted certificate or generate and persist one (R20 first run).</summary>
    public X509Certificate2 GetOrCreate()
    {
        if (_cached is not null)
            return _cached;

        if (File.Exists(CertificatePath))
        {
            _cached = LoadFromDisk();
            return _cached;
        }

        _cached = GenerateAndPersist();
        return _cached;
    }

    /// <summary>Pairing-reset (R20): regenerate the certificate; old pins and tokens are invalidated.
    /// The previous in-memory instance is left alive — a running Fleck listener may still
    /// reference it for in-flight handshakes; the caller reloads via GetOrCreate.</summary>
    public X509Certificate2 Regenerate()
    {
        _cached = null; // drop the reference; the old instance stays alive for in-flight TLS

        try
        {
            if (File.Exists(CertificatePath))
                File.Delete(CertificatePath);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to delete the old certificate before regeneration");
        }

        return GenerateAndPersist();
    }

    private X509Certificate2 LoadFromDisk()
    {
        try
        {
            var pfx = File.ReadAllBytes(CertificatePath);
            return X509CertificateLoader.LoadPkcs12(pfx, PfxPassword, X509KeyStorageFlags.Exportable);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load companion-cert.pfx; regenerating");
            File.Delete(CertificatePath);
            return GenerateAndPersist();
        }
    }

    private X509Certificate2 GenerateAndPersist()
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest(SubjectName, rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var cert = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(10));
        var pfx = cert.Export(X509ContentType.Pfx, PfxPassword);
        cert.Dispose();

        Directory.CreateDirectory(_basePath);
        File.WriteAllBytes(CertificatePath, pfx);
        _logger.LogInformation("Mirror certificate generated at {Path}", CertificatePath);

        return X509CertificateLoader.LoadPkcs12(pfx, PfxPassword, X509KeyStorageFlags.Exportable);
    }
}
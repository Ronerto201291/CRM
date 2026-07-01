using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography.Xml;
using System.Text;
using System.Xml;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Erp.Infrastructure.Services.Sii;

/// <summary>
/// Signs SII XML with XAdES-BES (Basic Electronic Signature) using RSA-SHA256.
/// Required by AEAT for Suministro Inmediato de Información telemática.
///
/// Certificate must be a FNMT-RCM qualified certificate stored as PFX.
/// Config keys:
///   Sii:CertPath   — absolute path to .pfx file
///   Sii:CertPass   — PFX password (from env var in production)
/// </summary>
public class SiiSigningService
{
    private readonly ILogger<SiiSigningService> _logger;
    private readonly string? _certPath;
    private readonly string? _certPass;

    private const string XAdESNamespace  = "http://uri.etsi.org/01903/v1.3.2#";
    private const string XmlDsigNamespace = "http://www.w3.org/2000/09/xmldsig#";
    private const string SignatureId     = "SiiSignature";
    private const string XAdESObjectId   = "XAdESObject";
    private const string SignedPropsId   = "SignedProperties";

    public SiiSigningService(IConfiguration config, ILogger<SiiSigningService> logger)
    {
        _logger  = logger;
        _certPath = config["Sii:CertPath"];
        _certPass = config["Sii:CertPass"];
    }

    /// <summary>
    /// Returns true when a signing certificate is configured.
    /// If false, XML can still be downloaded for manual submission.
    /// </summary>
    public bool IsConfigured => !string.IsNullOrEmpty(_certPath) && File.Exists(_certPath);

    /// <summary>
    /// Signs the provided SII XML with XAdES-BES and returns the signed XML string.
    /// The signature is enveloped inside the root element (ds:Signature appended).
    /// </summary>
    public string Sign(string xmlContent)
    {
        if (!IsConfigured)
            throw new InvalidOperationException(
                "SII signing certificate not configured. Set Sii:CertPath and Sii:CertPass.");

        var cert = new X509Certificate2(_certPath!, _certPass,
            X509KeyStorageFlags.MachineKeySet | X509KeyStorageFlags.EphemeralKeySet);

        var doc = new XmlDocument { PreserveWhitespace = true };
        doc.LoadXml(xmlContent);

        var rsa = cert.GetRSAPrivateKey()
            ?? throw new InvalidOperationException("Certificate has no RSA private key.");

        // ── 1. Build XAdES SignedProperties element ──────────────────────────
        var xadesProps = BuildXAdESQualifyingProperties(doc, cert);

        // Append as ds:Object so it participates in signing
        var dsObject = doc.CreateElement("ds", "Object", XmlDsigNamespace);
        dsObject.SetAttribute("Id", XAdESObjectId);
        dsObject.AppendChild(xadesProps);
        doc.DocumentElement!.AppendChild(dsObject);

        // ── 2. Configure SignedXml ────────────────────────────────────────────
        var signedXml = new SignedXml(doc)
        {
            SigningKey = rsa
        };
        signedXml.Signature.Id = SignatureId;
        signedXml.SignedInfo.CanonicalizationMethod = SignedXml.XmlDsigCanonicalizationUrl;
        signedXml.SignedInfo.SignatureMethod = "http://www.w3.org/2001/04/xmldsig-more#rsa-sha256";

        // Reference 1: the document root (enveloped)
        var refDoc = new Reference { Uri = "" };
        refDoc.AddTransform(new XmlDsigEnvelopedSignatureTransform());
        refDoc.AddTransform(new XmlDsigC14NTransform());
        refDoc.DigestMethod = "http://www.w3.org/2001/04/xmlenc#sha256";
        signedXml.AddReference(refDoc);

        // Reference 2: the XAdES SignedProperties
        var refXAdES = new Reference { Uri = $"#{SignedPropsId}" };
        refXAdES.Type = "http://uri.etsi.org/01903#SignedProperties";
        refXAdES.DigestMethod = "http://www.w3.org/2001/04/xmlenc#sha256";
        signedXml.AddReference(refXAdES);

        // KeyInfo: include X.509 certificate
        var keyInfo  = new KeyInfo();
        var keyClause = new KeyInfoX509Data(cert);
        keyClause.AddSubjectName(cert.Subject);
        keyInfo.AddClause(keyClause);
        signedXml.KeyInfo = keyInfo;

        // ── 3. Compute signature ──────────────────────────────────────────────
        signedXml.ComputeSignature();
        var signatureElement = signedXml.GetXml();

        // Move the ds:Object (XAdES) inside the Signature element
        doc.DocumentElement!.RemoveChild(dsObject);
        signatureElement.AppendChild(doc.ImportNode(dsObject, deep: true));

        doc.DocumentElement!.AppendChild(doc.ImportNode(signatureElement, deep: true));

        using var sw = new StringWriter();
        using var xw = XmlWriter.Create(sw, new XmlWriterSettings
        {
            Indent = true,
            Encoding = Encoding.UTF8,
            OmitXmlDeclaration = false
        });
        doc.WriteTo(xw);
        xw.Flush();

        _logger.LogInformation("SII XML signed with XAdES-BES. Cert: {Subject}", cert.Subject);
        return sw.ToString();
    }

    // ── XAdES QualifyingProperties helper ────────────────────────────────────

    private XmlElement BuildXAdESQualifyingProperties(XmlDocument doc, X509Certificate2 cert)
    {
        var qualifyingProps = doc.CreateElement("xades", "QualifyingProperties", XAdESNamespace);
        qualifyingProps.SetAttribute("Target", $"#{SignatureId}");

        var signedProps = doc.CreateElement("xades", "SignedProperties", XAdESNamespace);
        signedProps.SetAttribute("Id", SignedPropsId);

        var signedSigProps = doc.CreateElement("xades", "SignedSignatureProperties", XAdESNamespace);

        // SigningTime
        var signingTime = doc.CreateElement("xades", "SigningTime", XAdESNamespace);
        signingTime.InnerText = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");
        signedSigProps.AppendChild(signingTime);

        // SigningCertificateV2 (recommended over v1 for SHA256)
        var signingCertV2 = doc.CreateElement("xades", "SigningCertificateV2", XAdESNamespace);
        var certEl = doc.CreateElement("xades", "Cert", XAdESNamespace);

        var certDigest = doc.CreateElement("xades", "CertDigest", XAdESNamespace);
        var digestMethod = doc.CreateElement("ds", "DigestMethod", XmlDsigNamespace);
        digestMethod.SetAttribute("Algorithm", "http://www.w3.org/2001/04/xmlenc#sha256");
        var digestValue = doc.CreateElement("ds", "DigestValue", XmlDsigNamespace);
        using var sha256 = SHA256.Create();
        digestValue.InnerText = Convert.ToBase64String(sha256.ComputeHash(cert.RawData));
        certDigest.AppendChild(digestMethod);
        certDigest.AppendChild(digestValue);
        certEl.AppendChild(certDigest);

        // IssuerSerialV2: base64(DER-encoded GeneralName + serialNumber)
        var issuerSerial = doc.CreateElement("xades", "IssuerSerialV2", XAdESNamespace);
        issuerSerial.InnerText = Convert.ToBase64String(Encoding.ASCII.GetBytes(
            $"{cert.Issuer}:{cert.SerialNumber}"));
        certEl.AppendChild(issuerSerial);

        signingCertV2.AppendChild(certEl);
        signedSigProps.AppendChild(signingCertV2);
        signedProps.AppendChild(signedSigProps);
        qualifyingProps.AppendChild(signedProps);
        return qualifyingProps;
    }
}

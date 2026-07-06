using Erp.Application.Common.Fiscal;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography.Xml;
using System.Text;
using System.Xml;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Erp.Infrastructure.Services.FacturaE;

/// <summary>
/// Firma FacturaE con XAdES-EPES (política explícita Facturae v3.1).
/// Reutiliza el certificado FNMT configurado en Sii:CertPath / Sii:CertPass.
/// </summary>
public sealed class FacturaESigningService
{
    private readonly ILogger<FacturaESigningService> _logger;
    private readonly string? _certPath;
    private readonly string? _certPass;

    private const string XAdESNamespace   = "http://uri.etsi.org/01903/v1.3.2#";
    private const string XmlDsigNamespace = "http://www.w3.org/2000/09/xmldsig#";
    private const string SignatureId      = "FacturaESignature";
    private const string XAdESObjectId    = "FacturaEXAdESObject";
    private const string SignedPropsId    = "FacturaESignedProperties";

    public FacturaESigningService(IConfiguration config, ILogger<FacturaESigningService> logger)
    {
        _logger   = logger;
        _certPath = config["Sii:CertPath"];
        _certPass = config["Sii:CertPass"];
    }

    public bool IsConfigured => !string.IsNullOrEmpty(_certPath) && File.Exists(_certPath);

    public string Sign(string xmlContent)
    {
        if (!IsConfigured)
            throw new InvalidOperationException(
                "Certificado de firma no configurado. Configure Sii:CertPath y Sii:CertPass.");

        var cert = new X509Certificate2(_certPath!, _certPass,
            X509KeyStorageFlags.MachineKeySet | X509KeyStorageFlags.EphemeralKeySet);

        var doc = new XmlDocument { PreserveWhitespace = true };
        doc.LoadXml(xmlContent);

        var rsa = cert.GetRSAPrivateKey()
            ?? throw new InvalidOperationException("El certificado no tiene clave privada RSA.");

        var xadesProps = BuildXAdESQualifyingProperties(doc, cert);

        var dsObject = doc.CreateElement("ds", "Object", XmlDsigNamespace);
        dsObject.SetAttribute("Id", XAdESObjectId);
        dsObject.AppendChild(xadesProps);
        doc.DocumentElement!.AppendChild(dsObject);

        var signedXml = new SignedXml(doc)
        {
            SigningKey = rsa
        };
        signedXml.Signature.Id = SignatureId;
        signedXml.SignedInfo.CanonicalizationMethod = SignedXml.XmlDsigCanonicalizationUrl;
        signedXml.SignedInfo.SignatureMethod = "http://www.w3.org/2001/04/xmldsig-more#rsa-sha256";

        var refDoc = new Reference { Uri = "" };
        refDoc.AddTransform(new XmlDsigEnvelopedSignatureTransform());
        refDoc.AddTransform(new XmlDsigC14NTransform());
        refDoc.DigestMethod = "http://www.w3.org/2001/04/xmlenc#sha256";
        signedXml.AddReference(refDoc);

        var refXAdES = new Reference { Uri = $"#{SignedPropsId}" };
        refXAdES.Type = "http://uri.etsi.org/01903#SignedProperties";
        refXAdES.DigestMethod = "http://www.w3.org/2001/04/xmlenc#sha256";
        signedXml.AddReference(refXAdES);

        var keyInfo = new KeyInfo();
        var keyClause = new KeyInfoX509Data(cert);
        keyClause.AddSubjectName(cert.Subject);
        keyInfo.AddClause(keyClause);
        signedXml.KeyInfo = keyInfo;

        signedXml.ComputeSignature();
        var signatureElement = signedXml.GetXml();

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

        _logger.LogInformation("FacturaE firmado con XAdES-EPES. Cert: {Subject}", cert.Subject);
        return sw.ToString();
    }

    private static XmlElement BuildXAdESQualifyingProperties(XmlDocument doc, X509Certificate2 cert)
    {
        var qualifyingProps = doc.CreateElement("xades", "QualifyingProperties", XAdESNamespace);
        qualifyingProps.SetAttribute("Target", $"#{SignatureId}");

        var signedProps = doc.CreateElement("xades", "SignedProperties", XAdESNamespace);
        signedProps.SetAttribute("Id", SignedPropsId);

        var signedSigProps = doc.CreateElement("xades", "SignedSignatureProperties", XAdESNamespace);

        var signingTime = doc.CreateElement("xades", "SigningTime", XAdESNamespace);
        signingTime.InnerText = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");
        signedSigProps.AppendChild(signingTime);

        signedSigProps.AppendChild(BuildSignaturePolicyIdentifier(doc));

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

    private static XmlElement BuildSignaturePolicyIdentifier(XmlDocument doc)
    {
        var policyIdentifier = doc.CreateElement("xades", "SignaturePolicyIdentifier", XAdESNamespace);
        var policyId = doc.CreateElement("xades", "SignaturePolicyId", XAdESNamespace);

        var sigPolicyId = doc.CreateElement("xades", "SigPolicyId", XAdESNamespace);
        var identifier = doc.CreateElement("xades", "Identifier", XAdESNamespace);
        identifier.InnerText = FacturaEConstants.SignaturePolicyUrl;
        var description = doc.CreateElement("xades", "Description", XAdESNamespace);
        description.InnerText = FacturaEConstants.SignaturePolicyDescription;
        sigPolicyId.AppendChild(identifier);
        sigPolicyId.AppendChild(description);
        policyId.AppendChild(sigPolicyId);

        var sigPolicyHash = doc.CreateElement("xades", "SigPolicyHash", XAdESNamespace);
        var hashMethod = doc.CreateElement("ds", "DigestMethod", XmlDsigNamespace);
        hashMethod.SetAttribute("Algorithm", "http://www.w3.org/2000/09/xmldsig#sha1");
        var hashValue = doc.CreateElement("ds", "DigestValue", XmlDsigNamespace);
        hashValue.InnerText = FacturaEConstants.SignaturePolicyHashSha1;
        sigPolicyHash.AppendChild(hashMethod);
        sigPolicyHash.AppendChild(hashValue);
        policyId.AppendChild(sigPolicyHash);

        var qualifiers = doc.CreateElement("xades", "SigPolicyQualifiers", XAdESNamespace);
        var qualifier = doc.CreateElement("xades", "SigPolicyQualifier", XAdESNamespace);
        var spUri = doc.CreateElement("xades", "SPURI", XAdESNamespace);
        spUri.InnerText = FacturaEConstants.SignaturePolicyUrl;
        qualifier.AppendChild(spUri);
        qualifiers.AppendChild(qualifier);
        policyId.AppendChild(qualifiers);

        policyIdentifier.AppendChild(policyId);
        return policyIdentifier;
    }
}

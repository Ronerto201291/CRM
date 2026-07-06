using Erp.Application.Common.Certificates;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography.Xml;
using System.Text;
using System.Xml;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Erp.Infrastructure.Services.Sii;

/// <summary>
/// Firma SII con XAdES-BES (RSA-SHA256). SignedProperties vía DataObject antes de ComputeSignature.
/// </summary>
public class SiiSigningService : ISiiSigningService
{
    private readonly ILogger<SiiSigningService> _logger;
    private readonly string? _certPath;
    private readonly string? _certPass;

    private const string XAdESNamespace   = "http://uri.etsi.org/01903/v1.3.2#";
    private const string XmlDsigNamespace   = "http://www.w3.org/2000/09/xmldsig#";
    private const string SignatureId        = "SiiSignature";
    private const string SignedPropsId      = "SignedProperties";

    public SiiSigningService(IConfiguration config, ILogger<SiiSigningService> logger)
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
                "SII signing certificate not configured. Set Sii:CertPath and Sii:CertPass.");

        var cert = Pkcs12CertificateLoader.Load(_certPath!, _certPass);

        var doc = new XmlDocument { PreserveWhitespace = true };
        doc.LoadXml(xmlContent);

        var rsa = cert.GetRSAPrivateKey()
            ?? throw new InvalidOperationException("Certificate has no RSA private key.");

        var propsDoc = new XmlDocument { PreserveWhitespace = true };
        var qualifyingProps = BuildXAdESQualifyingProperties(propsDoc, cert);
        propsDoc.AppendChild(qualifyingProps);

        var signedXml = new SignedXml(doc) { SigningKey = rsa };
        signedXml.Signature.Id = SignatureId;
        signedXml.SignedInfo.CanonicalizationMethod = SignedXml.XmlDsigCanonicalizationUrl;
        signedXml.SignedInfo.SignatureMethod = SignedXml.XmlDsigRSASHA256Url;

        var dataObject = new DataObject { Id = "XAdESObject" };
        dataObject.LoadXml(qualifyingProps);
        signedXml.AddObject(dataObject);

        var refDoc = new Reference { Uri = "" };
        refDoc.AddTransform(new XmlDsigEnvelopedSignatureTransform());
        refDoc.AddTransform(new XmlDsigC14NTransform());
        refDoc.DigestMethod = SignedXml.XmlDsigSHA256Url;
        signedXml.AddReference(refDoc);

        var refProps = new Reference { Uri = $"#{SignedPropsId}" };
        refProps.Type = "http://uri.etsi.org/01903#SignedProperties";
        refProps.DigestMethod = SignedXml.XmlDsigSHA256Url;
        signedXml.AddReference(refProps);

        var keyInfo = new KeyInfo();
        var keyClause = new KeyInfoX509Data(cert);
        keyClause.AddSubjectName(cert.Subject);
        keyInfo.AddClause(keyClause);
        signedXml.KeyInfo = keyInfo;

        signedXml.ComputeSignature();
        doc.DocumentElement!.AppendChild(doc.ImportNode(signedXml.GetXml(), deep: true));

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

        var signingCert = doc.CreateElement("xades", "SigningCertificate", XAdESNamespace);
        var certEl = doc.CreateElement("xades", "Cert", XAdESNamespace);

        var certDigest = doc.CreateElement("xades", "CertDigest", XAdESNamespace);
        var digestMethod = doc.CreateElement("ds", "DigestMethod", XmlDsigNamespace);
        digestMethod.SetAttribute("Algorithm", SignedXml.XmlDsigSHA256Url);
        var digestValue = doc.CreateElement("ds", "DigestValue", XmlDsigNamespace);
        using (var sha256 = SHA256.Create())
            digestValue.InnerText = Convert.ToBase64String(sha256.ComputeHash(cert.RawData));
        certDigest.AppendChild(digestMethod);
        certDigest.AppendChild(digestValue);
        certEl.AppendChild(certDigest);

        var issuerSerial = doc.CreateElement("xades", "IssuerSerial", XAdESNamespace);
        var issuerName = doc.CreateElement("ds", "X509IssuerName", XmlDsigNamespace);
        issuerName.InnerText = cert.Issuer;
        var serialNumber = doc.CreateElement("ds", "X509SerialNumber", XmlDsigNamespace);
        serialNumber.InnerText = cert.GetSerialNumberString();
        issuerSerial.AppendChild(issuerName);
        issuerSerial.AppendChild(serialNumber);
        certEl.AppendChild(issuerSerial);

        signingCert.AppendChild(certEl);
        signedSigProps.AppendChild(signingCert);
        signedProps.AppendChild(signedSigProps);
        qualifyingProps.AppendChild(signedProps);
        return qualifyingProps;
    }
}

using System.Xml.Serialization;

namespace Erp.Application.Features.Sii.Models;

public static class SiiNamespaces
{
    public const string LR =
        "https://www2.agenciatributaria.gob.es/static_files/common/internet/dep/aplicaciones/es/aeat/ssii/fact/ws/SuministroLR.xsd";
    public const string Info =
        "https://www2.agenciatributaria.gob.es/static_files/common/internet/dep/aplicaciones/es/aeat/ssii/fact/ws/SuministroInformacion.xsd";
}

[XmlRoot("SuministroLRFacturasEmitidas", Namespace = SiiNamespaces.LR)]
public class SuministroLRFacturasEmitidas
{
    [XmlElement("Cabecera", Namespace = SiiNamespaces.Info)]
    public CabeceraSii Cabecera { get; set; } = new();

    [XmlElement("RegistroLRFacturasEmitidas", Namespace = SiiNamespaces.LR)]
    public List<RegistroFacturaEmitida> Registros { get; set; } = new();
}

[XmlRoot("SuministroLRFacturasRecibidas", Namespace = SiiNamespaces.LR)]
public class SuministroLRFacturasRecibidas
{
    [XmlElement("Cabecera", Namespace = SiiNamespaces.Info)]
    public CabeceraSii Cabecera { get; set; } = new();

    [XmlElement("RegistroLRFacturasRecibidas", Namespace = SiiNamespaces.LR)]
    public List<RegistroFacturaRecibida> Registros { get; set; } = new();
}

[XmlType(Namespace = SiiNamespaces.Info)]
public class CabeceraSii
{
    [XmlElement("IDVersionSii", Namespace = SiiNamespaces.Info)] public string IDVersionSii { get; set; } = "1.1";
    [XmlElement("Titular", Namespace = SiiNamespaces.Info)] public PersonaFisicaJuridica Titular { get; set; } = new();
    [XmlElement("TipoComunicacion", Namespace = SiiNamespaces.Info)] public string TipoComunicacion { get; set; } = "A0";
}

[XmlType(Namespace = SiiNamespaces.Info)]
public class PersonaFisicaJuridica
{
    [XmlElement("NombreRazon")] public string NombreRazon { get; set; } = string.Empty;
    [XmlElement("NIF")] public string NIF { get; set; } = string.Empty;
}

[XmlType(Namespace = SiiNamespaces.LR)]
public class RegistroFacturaEmitida
{
    [XmlElement("PeriodoLiquidacion", Namespace = SiiNamespaces.Info)] public PeriodoLiquidacion PeriodoLiquidacion { get; set; } = new();
    [XmlElement("IDFactura", Namespace = SiiNamespaces.Info)] public IDFactura IDFactura { get; set; } = new();
    [XmlElement("FacturaExpedida", Namespace = SiiNamespaces.Info)] public FacturaExpedida FacturaExpedida { get; set; } = new();
}

[XmlType(Namespace = SiiNamespaces.LR)]
public class RegistroFacturaRecibida
{
    [XmlElement("PeriodoLiquidacion", Namespace = SiiNamespaces.Info)] public PeriodoLiquidacion PeriodoLiquidacion { get; set; } = new();
    [XmlElement("IDFactura", Namespace = SiiNamespaces.Info)] public IDFacturaRecibida IDFactura { get; set; } = new();
    [XmlElement("FacturaRecibida", Namespace = SiiNamespaces.Info)] public FacturaRecibida FacturaRecibida { get; set; } = new();
}

[XmlType(Namespace = SiiNamespaces.Info)]
public class PeriodoLiquidacion
{
    [XmlElement("Ejercicio")] public string Ejercicio { get; set; } = string.Empty;
    [XmlElement("Periodo")] public string Periodo { get; set; } = string.Empty;
}

[XmlType(Namespace = SiiNamespaces.Info)]
public class IDFactura
{
    [XmlElement("IDEmisorFactura")] public PersonaFisicaJuridica IDEmisorFactura { get; set; } = new();
    [XmlElement("NumSerieFacturaEmisor")] public string NumSerieFacturaEmisor { get; set; } = string.Empty;
    [XmlElement("FechaExpedicionFacturaEmisor")] public string FechaExpedicion { get; set; } = string.Empty;
}

[XmlType(Namespace = SiiNamespaces.Info)]
public class IDFacturaRecibida
{
    [XmlElement("IDEmisorFactura")] public PersonaFisicaJuridica IDEmisorFactura { get; set; } = new();
    [XmlElement("NumSerieFacturaEmisor")] public string NumSerieFacturaEmisor { get; set; } = string.Empty;
    [XmlElement("FechaExpedicionFacturaEmisor")] public string FechaExpedicion { get; set; } = string.Empty;
}

[XmlType(Namespace = SiiNamespaces.Info)]
public class FacturaExpedida
{
    [XmlElement("TipoFactura")] public string TipoFactura { get; set; } = "F1";
    [XmlElement("ClaveRegimenEspecialOTrascendencia")] public string ClaveRegimen { get; set; } = "01";
    [XmlElement("DescripcionOperacion")] public string DescripcionOperacion { get; set; } = "Venta de bienes/servicios";
    [XmlElement("Contraparte")] public PersonaFisicaJuridica? Contraparte { get; set; }
    [XmlElement("TipoDesglose")] public TipoDesglose TipoDesglose { get; set; } = new();
    [XmlElement("ImporteTotal")] public string ImporteTotal { get; set; } = "0.00";
}

[XmlType(Namespace = SiiNamespaces.Info)]
public class FacturaRecibida
{
    [XmlElement("TipoFactura")] public string TipoFactura { get; set; } = "F1";
    [XmlElement("ClaveRegimenEspecialOTrascendencia")] public string ClaveRegimen { get; set; } = "01";
    [XmlElement("DescripcionOperacion")] public string DescripcionOperacion { get; set; } = "Compra de bienes/servicios";
    [XmlElement("FacturaSimplificadaArticulos7374")] public string FacturaSimplificada { get; set; } = "N";
    [XmlElement("Contraparte")] public PersonaFisicaJuridica Contraparte { get; set; } = new();
    [XmlElement("FechaRegContable")] public string FechaRegContable { get; set; } = string.Empty;
    [XmlElement("CuotaDeducible")] public string CuotaDeducible { get; set; } = "0.00";
    [XmlElement("DesgloseFactura")] public DesgloseFacturaRecibida DesgloseFactura { get; set; } = new();
    [XmlElement("ImporteTotal")] public string ImporteTotal { get; set; } = "0.00";
}

[XmlType(Namespace = SiiNamespaces.Info)]
public class TipoDesglose
{
    [XmlElement("DesgloseFactura")] public DesgloseFactura DesgloseFactura { get; set; } = new();
}

[XmlType(Namespace = SiiNamespaces.Info)]
public class DesgloseFactura
{
    [XmlElement("Sujeta")] public Sujeta Sujeta { get; set; } = new();
}

[XmlType(Namespace = SiiNamespaces.Info)]
public class Sujeta
{
    [XmlElement("NoExenta")] public NoExenta NoExenta { get; set; } = new();
}

[XmlType(Namespace = SiiNamespaces.Info)]
public class NoExenta
{
    [XmlElement("TipoNoExenta")] public string TipoNoExenta { get; set; } = "S1";
    [XmlElement("DesgloseIVA")] public DesgloseIVA DesgloseIVA { get; set; } = new();
}

[XmlType(Namespace = SiiNamespaces.Info)]
public class DesgloseIVA
{
    [XmlElement("DetalleIVA")] public List<DetalleIVA> DetalleIVA { get; set; } = new();
}

[XmlType(Namespace = SiiNamespaces.Info)]
public class DetalleIVA
{
    [XmlElement("TipoImpositivo")] public string TipoImpositivo { get; set; } = "21";
    [XmlElement("BaseImponible")] public string BaseImponible { get; set; } = "0.00";
    [XmlElement("CuotaRepercutida")] public string? CuotaRepercutida { get; set; }
    [XmlElement("CuotaSoportada")] public string? CuotaSoportada { get; set; }
}

[XmlType(Namespace = SiiNamespaces.Info)]
public class DesgloseFacturaRecibida
{
    [XmlElement("InversionSujetoPasivo")] public InversionSujetoPasivo? InversionSujetoPasivo { get; set; }
    [XmlElement("DesgloseIVA")] public DesgloseIVA DesgloseIVA { get; set; } = new();
}

[XmlType(Namespace = SiiNamespaces.Info)]
public class InversionSujetoPasivo
{
    [XmlElement("DetalleIVA")] public List<DetalleIVA> DetalleIVA { get; set; } = new();
}

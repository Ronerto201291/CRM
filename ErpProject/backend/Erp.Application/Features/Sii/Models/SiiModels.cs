using System.Xml.Serialization;

namespace Erp.Application.Features.Sii.Models;

// ================================================================
// SII — Suministro Inmediato de Información (AEAT Spain)
// XSD: https://www.agenciatributaria.es/AEAT/Contenidos_Comunes/La_Agencia_Tributaria/Modelos_y_formularios/Suministro_inmediato_informacion/FicherosSuministros/SuministroLR.xsd
// ================================================================

[XmlRoot("SuministroLRFacturasEmitidas", Namespace = "https://www2.agenciatributaria.gob.es/static_files/common/internet/dep/aplicaciones/es/aeat/ssii/fact/ws/SuministroLR.xsd")]
public class SuministroLRFacturasEmitidas
{
    [XmlElement("Cabecera")]
    public CabeceraSii Cabecera { get; set; } = new();

    [XmlElement("RegistroLRFacturasEmitidas")]
    public List<RegistroFacturaEmitida> Registros { get; set; } = new();
}

[XmlRoot("SuministroLRFacturasRecibidas", Namespace = "https://www2.agenciatributaria.gob.es/static_files/common/internet/dep/aplicaciones/es/aeat/ssii/fact/ws/SuministroLR.xsd")]
public class SuministroLRFacturasRecibidas
{
    [XmlElement("Cabecera")]
    public CabeceraSii Cabecera { get; set; } = new();

    [XmlElement("RegistroLRFacturasRecibidas")]
    public List<RegistroFacturaRecibida> Registros { get; set; } = new();
}

public class CabeceraSii
{
    [XmlElement("IDVersionSii")] public string IDVersionSii { get; set; } = "1.1";
    [XmlElement("Titular")] public PersonaFisicaJuridica Titular { get; set; } = new();
    [XmlElement("TipoComunicacion")] public string TipoComunicacion { get; set; } = "A0"; // A0=Alta, A1=Modificación
}

public class PersonaFisicaJuridica
{
    [XmlElement("NombreRazon")] public string NombreRazon { get; set; } = string.Empty;
    [XmlElement("NIF")] public string NIF { get; set; } = string.Empty;
}

public class RegistroFacturaEmitida
{
    [XmlElement("PeriodoLiquidacion")] public PeriodoLiquidacion PeriodoLiquidacion { get; set; } = new();
    [XmlElement("IDFactura")] public IDFactura IDFactura { get; set; } = new();
    [XmlElement("FacturaExpedida")] public FacturaExpedida FacturaExpedida { get; set; } = new();
}

public class RegistroFacturaRecibida
{
    [XmlElement("PeriodoLiquidacion")] public PeriodoLiquidacion PeriodoLiquidacion { get; set; } = new();
    [XmlElement("IDFactura")] public IDFacturaRecibida IDFactura { get; set; } = new();
    [XmlElement("FacturaRecibida")] public FacturaRecibida FacturaRecibida { get; set; } = new();
}

public class PeriodoLiquidacion
{
    [XmlElement("Ejercicio")] public string Ejercicio { get; set; } = string.Empty;   // "2026"
    [XmlElement("Periodo")] public string Periodo { get; set; } = string.Empty;       // "01"–"12"
}

public class IDFactura
{
    [XmlElement("IDEmisorFactura")] public PersonaFisicaJuridica IDEmisorFactura { get; set; } = new();
    [XmlElement("NumSerieFacturaEmisor")] public string NumSerieFacturaEmisor { get; set; } = string.Empty;
    [XmlElement("FechaExpedicionFacturaEmisor")] public string FechaExpedicion { get; set; } = string.Empty; // "dd-MM-yyyy"
}

public class IDFacturaRecibida
{
    [XmlElement("IDEmisorFactura")] public PersonaFisicaJuridica IDEmisorFactura { get; set; } = new();
    [XmlElement("NumSerieFacturaEmisor")] public string NumSerieFacturaEmisor { get; set; } = string.Empty;
    [XmlElement("FechaExpedicionFacturaEmisor")] public string FechaExpedicion { get; set; } = string.Empty;
}

public class FacturaExpedida
{
    [XmlElement("TipoFactura")] public string TipoFactura { get; set; } = "F1"; // F1=Normal, F2=Simplificada, R1=Rectificativa
    [XmlElement("ClaveRegimenEspecialOTrascendencia")] public string ClaveRegimen { get; set; } = "01"; // 01=Operación de régimen general
    [XmlElement("DescripcionOperacion")] public string DescripcionOperacion { get; set; } = "Venta de bienes/servicios";
    [XmlElement("Contraparte")] public PersonaFisicaJuridica? Contraparte { get; set; }
    [XmlElement("TipoDesglose")] public TipoDesglose TipoDesglose { get; set; } = new();
    [XmlElement("ImporteTotal")] public string ImporteTotal { get; set; } = "0.00";
}

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

public class TipoDesglose
{
    [XmlElement("DesgloseFactura")] public DesgloseFactura DesgloseFactura { get; set; } = new();
}

public class DesgloseFactura
{
    [XmlElement("Sujeta")] public Sujeta Sujeta { get; set; } = new();
}

public class Sujeta
{
    [XmlElement("NoExenta")] public NoExenta NoExenta { get; set; } = new();
}

public class NoExenta
{
    [XmlElement("TipoNoExenta")] public string TipoNoExenta { get; set; } = "S1"; // S1=Sin inversión del sujeto pasivo
    [XmlElement("DesgloseIVA")] public DesgloseIVA DesgloseIVA { get; set; } = new();
}

public class DesgloseIVA
{
    [XmlElement("DetalleIVA")] public List<DetalleIVA> DetalleIVA { get; set; } = new();
}

public class DetalleIVA
{
    [XmlElement("TipoImpositivo")] public string TipoImpositivo { get; set; } = "21";   // 21, 10, 4, 0
    [XmlElement("BaseImponible")] public string BaseImponible { get; set; } = "0.00";
    [XmlElement("CuotaRepercutida")] public string CuotaRepercutida { get; set; } = "0.00";
}

public class DesgloseFacturaRecibida
{
    [XmlElement("InversionSujetoPasivo")] public InversionSujetoPasivo? InversionSujetoPasivo { get; set; }
    [XmlElement("DesgloseIVA")] public DesgloseIVA DesgloseIVA { get; set; } = new();
}

public class InversionSujetoPasivo
{
    [XmlElement("DetalleIVA")] public List<DetalleIVA> DetalleIVA { get; set; } = new();
}

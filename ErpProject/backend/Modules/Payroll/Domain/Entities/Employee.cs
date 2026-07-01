using Erp.Domain.Common;

namespace Erp.Modules.Payroll.Domain.Entities;

/// <summary>Trabajador para nómina mensual, bases TGSS e IRPF (Modelo 111/190).</summary>
public class Employee : AuditableEntity
{
    public Guid CompanyId { get; set; }

    public string TaxId { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    /// <summary>Número de afiliación a la SS (NAF / CCC empleado).</summary>
    public string? SocialSecurityNumber { get; set; }

    public DateTime HireDate { get; set; }
    /// <summary>Indefinido, Temporal, etc.</summary>
    public string ContractType { get; set; } = "Indefinido";
    /// <summary>Jornada: Completa, Parcial, horas semanales.</summary>
    public decimal WeeklyHours { get; set; } = 40m;

    public bool IsActive { get; set; } = true;
    public string? Notes { get; set; }

    public ICollection<PayrollLine> PayrollLines { get; set; } = new List<PayrollLine>();
}

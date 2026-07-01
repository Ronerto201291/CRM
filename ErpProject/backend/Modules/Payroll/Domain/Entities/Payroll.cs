using Erp.Domain.Common;

namespace Erp.Modules.Payroll.Domain.Entities
{
    public class Payroll : AuditableEntity
    {
        public Guid CompanyId { get; set; }
        public Guid EmployeeId { get; set; }
        public int Year { get; set; }
        public int Month { get; set; }
        public decimal GrossSalary { get; set; }
        public decimal TaxableBase { get; set; } // Base 111/190
        public decimal SocialSecurityBase { get; set; } // Cotización SS
        public string Status { get; set; } = "Draft"; // Draft, Generated, Approved, Signed, Submitted
        public DateTime GeneratedDate { get; set; }
        public DateTime? SignatureDate { get; set; }
        public DateTime? SubmissionDate { get; set; }
    }

    public class PayrollDeduction : AuditableEntity
    {
        public Guid PayrollId { get; set; }
        public string Type { get; set; } = "IncomeTax"; // IncomeTax, SS, OtherDeductions
        public decimal Amount { get; set; }
        public string Description { get; set; } = string.Empty;
    }

    public class SocialSecurityContribution : AuditableEntity
    {
        public Guid CompanyId { get; set; }
        public Guid EmployeeId { get; set; }
        public int Year { get; set; }
        public int Month { get; set; }
        public decimal EmployeeContribution { get; set; } // SS del trabajador
        public decimal EmployerContribution { get; set; } // SS de la empresa
        public decimal UnemploymentIns { get; set; } // Desempleo
        public decimal ProfessionalTraining { get; set; } // Formación profesional
        public string TC { get; set; } = "TC2"; // TC1 o TC2
        public string Status { get; set; } = "Draft";
    }

    public class TaxableBase : AuditableEntity
    {
        public Guid CompanyId { get; set; }
        public Guid EmployeeId { get; set; }
        public int Year { get; set; }
        public decimal Base111 { get; set; } // Base 111 (IRPF)
        public decimal Base190 { get; set; } // Base 190 (SS)
        public string ReceivedTC1TC2 { get; set; } = string.Empty; // Referencia TC
        public DateTime? SubmittedToAeat { get; set; }
    }

    public class PayrollTemplate : AuditableEntity
    {
        public Guid CompanyId { get; set; }
        public string Name { get; set; } = string.Empty;
        public decimal BaseSalary { get; set; }
        public decimal Supplement1 { get; set; }
        public decimal Supplement2 { get; set; }
        public decimal SocialSecurityRate { get; set; } // % SS
        public decimal UnemploymentRate { get; set; } // % Desempleo
        public string Status { get; set; } = "Active";
    }
}

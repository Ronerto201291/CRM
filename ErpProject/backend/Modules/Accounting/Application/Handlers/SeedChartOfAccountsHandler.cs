using Erp.Application.Common.Events;
using Erp.Modules.Accounting.Application.Interfaces;
using Erp.Modules.Accounting.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Erp.Modules.Accounting.Application.Handlers;

/// <summary>
/// Reacciona a CompanyCreatedEvent sembrando el Plan General Contable (PGC)
/// español mínimo para la empresa nueva. Sin esto, cualquier cobro de factura
/// (PaymentReceivedEventHandler busca cuentas "570"/"572"/"430" por código
/// exacto) fallaría con InvalidOperationException para toda empresa registrada
/// por el flujo normal — hallazgo real verificado: el antiguo
/// Infrastructure/Seeding/PgcSeeder.cs nunca se invocaba desde ningún sitio, y
/// el único seeding real vivía inline en Program.cs solo para el bootstrap de
/// la empresa de desarrollo (arranque con BBDD vacía). Este handler sustituye
/// a ambos con una única fuente de verdad (ADR-0018).
///
/// Incluye "5721"/"5722" (TPV/Bizum pendiente de liquidar, ADR-0018 #42b) para
/// distinguir esos cobros de una transferencia bancaria normal ("572") y poder
/// conciliarlos por separado con BankReconciliationService (Treasury), que ya
/// filtra por prefijo de código de cuenta.
/// </summary>
public class SeedChartOfAccountsHandler : INotificationHandler<CompanyCreatedEvent>
{
    private readonly IAccountingDbContext _context;
    private readonly ILogger<SeedChartOfAccountsHandler> _logger;

    public SeedChartOfAccountsHandler(IAccountingDbContext context, ILogger<SeedChartOfAccountsHandler> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task Handle(CompanyCreatedEvent notification, CancellationToken ct)
    {
        var exists = await _context.Accounts
            .IgnoreQueryFilters()
            .AnyAsync(a => a.CompanyId == notification.CompanyId, ct);
        if (exists)
        {
            _logger.LogInformation("Plan contable ya existe para empresa {CompanyId}, se omite el seeding.", notification.CompanyId);
            return;
        }

        var companyId = notification.CompanyId;
        var accounts = new List<Account>
        {
            // Grupo 1 – Financiación básica
            new() { Id = Guid.NewGuid(), CompanyId = companyId, Code = "100", Name = "Capital social", Type = "Equity" },
            new() { Id = Guid.NewGuid(), CompanyId = companyId, Code = "118", Name = "Aportaciones de socios", Type = "Equity" },
            new() { Id = Guid.NewGuid(), CompanyId = companyId, Code = "129", Name = "Resultado del ejercicio", Type = "Equity" },

            // Grupo 2 – Activo no corriente
            new() { Id = Guid.NewGuid(), CompanyId = companyId, Code = "210", Name = "Terrenos y bienes naturales", Type = "Asset" },
            new() { Id = Guid.NewGuid(), CompanyId = companyId, Code = "211", Name = "Construcciones", Type = "Asset" },
            new() { Id = Guid.NewGuid(), CompanyId = companyId, Code = "213", Name = "Maquinaria", Type = "Asset" },
            new() { Id = Guid.NewGuid(), CompanyId = companyId, Code = "216", Name = "Mobiliario", Type = "Asset" },
            new() { Id = Guid.NewGuid(), CompanyId = companyId, Code = "217", Name = "Equipos para procesos de información", Type = "Asset" },
            new() { Id = Guid.NewGuid(), CompanyId = companyId, Code = "218", Name = "Elementos de transporte", Type = "Asset" },
            new() { Id = Guid.NewGuid(), CompanyId = companyId, Code = "280", Name = "Amortización acumulada inmovilizado material", Type = "Asset" },

            // Grupo 3 – Existencias
            new() { Id = Guid.NewGuid(), CompanyId = companyId, Code = "300", Name = "Mercaderías", Type = "Asset" },
            new() { Id = Guid.NewGuid(), CompanyId = companyId, Code = "310", Name = "Materias primas", Type = "Asset" },
            new() { Id = Guid.NewGuid(), CompanyId = companyId, Code = "350", Name = "Productos terminados", Type = "Asset" },

            // Grupo 4 – Acreedores y deudores comerciales
            new() { Id = Guid.NewGuid(), CompanyId = companyId, Code = "400", Name = "Proveedores", Type = "Liability" },
            new() { Id = Guid.NewGuid(), CompanyId = companyId, Code = "410", Name = "Acreedores por prestaciones de servicios", Type = "Liability" },
            new() { Id = Guid.NewGuid(), CompanyId = companyId, Code = "430", Name = "Clientes", Type = "Asset" },
            new() { Id = Guid.NewGuid(), CompanyId = companyId, Code = "440", Name = "Deudores", Type = "Asset" },
            new() { Id = Guid.NewGuid(), CompanyId = companyId, Code = "460", Name = "Anticipos de remuneraciones", Type = "Asset" },
            new() { Id = Guid.NewGuid(), CompanyId = companyId, Code = "470", Name = "Hacienda Pública deudora por IVA", Type = "Asset" },
            new() { Id = Guid.NewGuid(), CompanyId = companyId, Code = "4700", Name = "Hacienda Pública deudora por IRPF", Type = "Asset" },
            new() { Id = Guid.NewGuid(), CompanyId = companyId, Code = "472", Name = "Hacienda Pública, IVA soportado", Type = "Asset" },
            new() { Id = Guid.NewGuid(), CompanyId = companyId, Code = "473", Name = "Hacienda Pública, retenciones y pagos a cuenta", Type = "Asset" },
            new() { Id = Guid.NewGuid(), CompanyId = companyId, Code = "475", Name = "Hacienda Pública, acreedora por IVA", Type = "Liability" },
            new() { Id = Guid.NewGuid(), CompanyId = companyId, Code = "4751", Name = "Hacienda Pública acreedora por retenciones practicadas", Type = "Liability" },
            new() { Id = Guid.NewGuid(), CompanyId = companyId, Code = "477", Name = "Hacienda Pública, IVA repercutido", Type = "Liability" },
            new() { Id = Guid.NewGuid(), CompanyId = companyId, Code = "476", Name = "Organismos de la Seguridad Social acreedores", Type = "Liability" },

            // Grupo 5 – Cuentas financieras
            new() { Id = Guid.NewGuid(), CompanyId = companyId, Code = "520", Name = "Deudas a corto plazo con entidades de crédito", Type = "Liability" },
            new() { Id = Guid.NewGuid(), CompanyId = companyId, Code = "570", Name = "Caja, euros", Type = "Asset" },
            new() { Id = Guid.NewGuid(), CompanyId = companyId, Code = "572", Name = "Bancos e instituciones de crédito c/c vista, euros", Type = "Asset" },
            // ADR-0018 #42b — TPV/Bizum: cobro pendiente de liquidar en el banco (neto de
            // comisión, con desfase de días), distinto de una transferencia normal.
            new() { Id = Guid.NewGuid(), CompanyId = companyId, Code = "5721", Name = "TPV pendiente de liquidar", Type = "Asset" },
            new() { Id = Guid.NewGuid(), CompanyId = companyId, Code = "5722", Name = "Bizum pendiente de liquidar", Type = "Asset" },

            // Grupo 6 – Compras y gastos
            new() { Id = Guid.NewGuid(), CompanyId = companyId, Code = "600", Name = "Compras de mercaderías", Type = "Expense" },
            new() { Id = Guid.NewGuid(), CompanyId = companyId, Code = "601", Name = "Compras de materias primas", Type = "Expense" },
            new() { Id = Guid.NewGuid(), CompanyId = companyId, Code = "621", Name = "Arrendamientos y cánones", Type = "Expense" },
            new() { Id = Guid.NewGuid(), CompanyId = companyId, Code = "622", Name = "Reparaciones y conservación", Type = "Expense" },
            new() { Id = Guid.NewGuid(), CompanyId = companyId, Code = "623", Name = "Servicios de profesionales independientes", Type = "Expense" },
            new() { Id = Guid.NewGuid(), CompanyId = companyId, Code = "624", Name = "Transportes", Type = "Expense" },
            new() { Id = Guid.NewGuid(), CompanyId = companyId, Code = "625", Name = "Primas de seguros", Type = "Expense" },
            new() { Id = Guid.NewGuid(), CompanyId = companyId, Code = "626", Name = "Servicios bancarios y similares", Type = "Expense" },
            new() { Id = Guid.NewGuid(), CompanyId = companyId, Code = "627", Name = "Publicidad, propaganda y relaciones públicas", Type = "Expense" },
            new() { Id = Guid.NewGuid(), CompanyId = companyId, Code = "628", Name = "Suministros", Type = "Expense" },
            new() { Id = Guid.NewGuid(), CompanyId = companyId, Code = "629", Name = "Otros servicios", Type = "Expense" },
            new() { Id = Guid.NewGuid(), CompanyId = companyId, Code = "640", Name = "Sueldos y salarios", Type = "Expense" },
            new() { Id = Guid.NewGuid(), CompanyId = companyId, Code = "642", Name = "Seguridad Social a cargo de la empresa", Type = "Expense" },
            new() { Id = Guid.NewGuid(), CompanyId = companyId, Code = "681", Name = "Amortización del inmovilizado material", Type = "Expense" },
            // ADR-0018 #42b — ajuste por diferencia de caja en arqueo (falta)
            new() { Id = Guid.NewGuid(), CompanyId = companyId, Code = "668", Name = "Otras pérdidas en gestión corriente", Type = "Expense" },

            // Grupo 7 – Ventas e ingresos
            new() { Id = Guid.NewGuid(), CompanyId = companyId, Code = "700", Name = "Ventas de mercaderías", Type = "Income" },
            new() { Id = Guid.NewGuid(), CompanyId = companyId, Code = "701", Name = "Ventas de productos terminados", Type = "Income" },
            new() { Id = Guid.NewGuid(), CompanyId = companyId, Code = "705", Name = "Prestaciones de servicios", Type = "Income" },
            new() { Id = Guid.NewGuid(), CompanyId = companyId, Code = "708", Name = "Devoluciones de ventas y operaciones similares", Type = "Income" },
            new() { Id = Guid.NewGuid(), CompanyId = companyId, Code = "751", Name = "Subvenciones a la explotación", Type = "Income" },
            new() { Id = Guid.NewGuid(), CompanyId = companyId, Code = "760", Name = "Ingresos de participaciones en instrumentos de patrimonio", Type = "Income" },
            new() { Id = Guid.NewGuid(), CompanyId = companyId, Code = "770", Name = "Beneficios procedentes del inmovilizado material", Type = "Income" },
            // ADR-0018 #42b — ajuste por diferencia de caja en arqueo (sobra)
            new() { Id = Guid.NewGuid(), CompanyId = companyId, Code = "778", Name = "Ingresos excepcionales", Type = "Income" },
        };

        _context.Accounts.AddRange(accounts);
        await _context.SaveChangesAsync(ct);

        _logger.LogInformation("Plan contable PGC sembrado para empresa {CompanyId} ({Count} cuentas).", companyId, accounts.Count);
    }
}

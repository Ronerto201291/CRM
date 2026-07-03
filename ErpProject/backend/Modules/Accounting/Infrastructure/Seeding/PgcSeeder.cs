using Erp.Modules.Accounting.Domain.Entities;
using Erp.Modules.Accounting.Application.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Erp.Modules.Accounting.Infrastructure.Seeding;

/// <summary>
/// Siembra el Plan General Contable (PGC) espa�ol oficial.
/// Se ejecuta al crear un nuevo Tenant para garantizar coherencia fiscal.
/// </summary>
public class PgcSeeder
{
    private readonly IAccountingDbContext _context;
    private readonly ILogger<PgcSeeder> _logger;

    public PgcSeeder(IAccountingDbContext context, ILogger<PgcSeeder> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task SeedAsync(Guid companyId)
    {
        _logger.LogInformation("Iniciando seeding PGC para empresa {CompanyId}", companyId);

        // Verificar si ya existe
        var exists = await _context.Accounts
            .Where(a => a.CompanyId == companyId)
            .AnyAsync();

        if (exists)
        {
            _logger.LogInformation("PGC ya existe para empresa {CompanyId}", companyId);
            return;
        }

        var accounts = GetPgcSpanishAccounts(companyId);
        _context.Accounts.AddRange(accounts);
        await _context.SaveChangesAsync(CancellationToken.None);

        _logger.LogInformation("PGC sembrado exitosamente para empresa {CompanyId}", companyId);
    }

    private List<Account> GetPgcSpanishAccounts(Guid companyId)
    {
        return new List<Account>
        {
            // =============== ACTIVO (Grupo 1) ===============
            // 10 - Capital
            new() { Id = Guid.NewGuid(), CompanyId = companyId, Code = "100", Name = "Capital social", Type = "Equity" },
            new() { Id = Guid.NewGuid(), CompanyId = companyId, Code = "110", Name = "Acciones o participaciones del capital", Type = "Equity" },

            // 11 - Reservas
            new() { Id = Guid.NewGuid(), CompanyId = companyId, Code = "110", Name = "Aportaciones de socios", Type = "Equity" },
            new() { Id = Guid.NewGuid(), CompanyId = companyId, Code = "120", Name = "Revalorizaci�n del inmovilizado", Type = "Equity" },
            new() { Id = Guid.NewGuid(), CompanyId = companyId, Code = "121", Name = "Reserva legal", Type = "Equity" },
            new() { Id = Guid.NewGuid(), CompanyId = companyId, Code = "122", Name = "Reserva estatutaria", Type = "Equity" },

            // 12 - Resultados
            new() { Id = Guid.NewGuid(), CompanyId = companyId, Code = "129", Name = "P�rdidas de ejercicios anteriores", Type = "Equity" },

            // =============== INMOVILIZADO (Grupo 2) ===============
            // 20 - Inmovilizado intangible
            new() { Id = Guid.NewGuid(), CompanyId = companyId, Code = "200", Name = "Investigaci�n", Type = "Asset" },
            new() { Id = Guid.NewGuid(), CompanyId = companyId, Code = "210", Name = "Concesiones administrativas", Type = "Asset" },
            new() { Id = Guid.NewGuid(), CompanyId = companyId, Code = "220", Name = "Patentes y licencias", Type = "Asset" },
            new() { Id = Guid.NewGuid(), CompanyId = companyId, Code = "230", Name = "Fondo de comercio", Type = "Asset" },

            // 21 - Inmovilizado material
            new() { Id = Guid.NewGuid(), CompanyId = companyId, Code = "210", Name = "Terrenos", Type = "Asset" },
            new() { Id = Guid.NewGuid(), CompanyId = companyId, Code = "211", Name = "Construcciones", Type = "Asset" },
            new() { Id = Guid.NewGuid(), CompanyId = companyId, Code = "212", Name = "Instalaciones t�cnicas", Type = "Asset" },
            new() { Id = Guid.NewGuid(), CompanyId = companyId, Code = "213", Name = "Maquinaria", Type = "Asset" },
            new() { Id = Guid.NewGuid(), CompanyId = companyId, Code = "214", Name = "Equipos para procesos", Type = "Asset" },
            new() { Id = Guid.NewGuid(), CompanyId = companyId, Code = "215", Name = "Equipos inform�ticos", Type = "Asset" },
            new() { Id = Guid.NewGuid(), CompanyId = companyId, Code = "216", Name = "Elementos de transporte", Type = "Asset" },

            // 28 - Amortizaci�n acumulada
            new() { Id = Guid.NewGuid(), CompanyId = companyId, Code = "280", Name = "Amortizaci�n acumulada inmovilizado intangible", Type = "Asset" },
            new() { Id = Guid.NewGuid(), CompanyId = companyId, Code = "281", Name = "Amortizaci�n acumulada construcciones", Type = "Asset" },
            new() { Id = Guid.NewGuid(), CompanyId = companyId, Code = "282", Name = "Amortizaci�n acumulada instalaciones", Type = "Asset" },
            new() { Id = Guid.NewGuid(), CompanyId = companyId, Code = "283", Name = "Amortizaci�n acumulada maquinaria", Type = "Asset" },

            // =============== EXISTENCIAS (Grupo 3) ===============
            // 30 - Mercader�as
            new() { Id = Guid.NewGuid(), CompanyId = companyId, Code = "300", Name = "Mercader�as A", Type = "Asset" },
            new() { Id = Guid.NewGuid(), CompanyId = companyId, Code = "310", Name = "Materias primas", Type = "Asset" },
            new() { Id = Guid.NewGuid(), CompanyId = companyId, Code = "320", Name = "Productos semiterminados", Type = "Asset" },
            new() { Id = Guid.NewGuid(), CompanyId = companyId, Code = "330", Name = "Productos terminados", Type = "Asset" },

            // =============== DEUDORES (Grupo 4) ===============
            // 43 - Clientes
            new() { Id = Guid.NewGuid(), CompanyId = companyId, Code = "430", Name = "Clientes", Type = "Asset" },
            new() { Id = Guid.NewGuid(), CompanyId = companyId, Code = "431", Name = "Clientes, efectos comerciales a cobrar", Type = "Asset" },

            // 44 - Deudores varios
            new() { Id = Guid.NewGuid(), CompanyId = companyId, Code = "440", Name = "Deudores varios", Type = "Asset" },
            new() { Id = Guid.NewGuid(), CompanyId = companyId, Code = "465", Name = "Hacienda p�blica, deudora por varios conceptos", Type = "Asset" },

            // =============== ACREEDORES (Grupo 4) ===============
            // 40 - Proveedores
            new() { Id = Guid.NewGuid(), CompanyId = companyId, Code = "400", Name = "Proveedores", Type = "Liability" },
            new() { Id = Guid.NewGuid(), CompanyId = companyId, Code = "401", Name = "Proveedores, efectos comerciales a pagar", Type = "Liability" },

            // =============== CUENTA DE TESORER�A (Grupo 5) ===============
            // 57 - Bancos
            new() { Id = Guid.NewGuid(), CompanyId = companyId, Code = "570", Name = "Bancos", Type = "Asset" },
            new() { Id = Guid.NewGuid(), CompanyId = companyId, Code = "572", Name = "Bancos, cuentas de ahorro", Type = "Asset" },

            // 58 - Caja
            new() { Id = Guid.NewGuid(), CompanyId = companyId, Code = "580", Name = "Caja", Type = "Asset" },

            // =============== DEUDAS A LARGO PLAZO (Grupo 2) ===============
            // 17 - Deudas a largo plazo
            new() { Id = Guid.NewGuid(), CompanyId = companyId, Code = "170", Name = "Deudas a largo plazo con entidades de cr�dito", Type = "Liability" },
            new() { Id = Guid.NewGuid(), CompanyId = companyId, Code = "171", Name = "Deudas a largo plazo", Type = "Liability" },

            // =============== DEUDAS A CORTO PLAZO (Grupo 5) ===============
            // 52 - Deudas a corto plazo
            new() { Id = Guid.NewGuid(), CompanyId = companyId, Code = "520", Name = "Deudas a corto plazo con entidades de cr�dito", Type = "Liability" },
            new() { Id = Guid.NewGuid(), CompanyId = companyId, Code = "521", Name = "Deudas a corto plazo", Type = "Liability" },

            // =============== INGRESOS (Grupo 7) ===============
            // 70 - Venta de mercader�as
            new() { Id = Guid.NewGuid(), CompanyId = companyId, Code = "700", Name = "Venta de mercader�as", Type = "Income" },

            // 71 - Venta de producci�n propia
            new() { Id = Guid.NewGuid(), CompanyId = companyId, Code = "710", Name = "Variaci�n de existencias de productos terminados", Type = "Income" },
            new() { Id = Guid.NewGuid(), CompanyId = companyId, Code = "711", Name = "Variaci�n de existencias de productos semiterminados", Type = "Income" },
            new() { Id = Guid.NewGuid(), CompanyId = companyId, Code = "712", Name = "Variaci�n de existencias de materias primas", Type = "Income" },

            // 72 - Prestaci�n de servicios
            new() { Id = Guid.NewGuid(), CompanyId = companyId, Code = "720", Name = "Prestaci�n de servicios", Type = "Income" },

            // =============== GASTOS (Grupo 6) ===============
            // 60 - Compra de materiales
            new() { Id = Guid.NewGuid(), CompanyId = companyId, Code = "600", Name = "Compra de mercader�as", Type = "Expense" },
            new() { Id = Guid.NewGuid(), CompanyId = companyId, Code = "610", Name = "Variaci�n de existencias de materias primas", Type = "Expense" },
            new() { Id = Guid.NewGuid(), CompanyId = companyId, Code = "611", Name = "Variaci�n de existencias de productos semiterminados", Type = "Expense" },
            new() { Id = Guid.NewGuid(), CompanyId = companyId, Code = "612", Name = "Variaci�n de existencias de productos terminados", Type = "Expense" },

            // 62 - Servicios exteriores
            new() { Id = Guid.NewGuid(), CompanyId = companyId, Code = "620", Name = "Reparaci�n y conservaci�n", Type = "Expense" },
            new() { Id = Guid.NewGuid(), CompanyId = companyId, Code = "621", Name = "Servicios de profesionales independientes", Type = "Expense" },
            new() { Id = Guid.NewGuid(), CompanyId = companyId, Code = "622", Name = "Reparaci�n y conservaci�n", Type = "Expense" },
            new() { Id = Guid.NewGuid(), CompanyId = companyId, Code = "623", Name = "Suministros", Type = "Expense" },
            new() { Id = Guid.NewGuid(), CompanyId = companyId, Code = "624", Name = "Transportes", Type = "Expense" },
            new() { Id = Guid.NewGuid(), CompanyId = companyId, Code = "625", Name = "Primas de seguros", Type = "Expense" },
            new() { Id = Guid.NewGuid(), CompanyId = companyId, Code = "626", Name = "Servicios bancarios y similares", Type = "Expense" },
            new() { Id = Guid.NewGuid(), CompanyId = companyId, Code = "627", Name = "Publicidad, propaganda y relaciones p�blicas", Type = "Expense" },
            new() { Id = Guid.NewGuid(), CompanyId = companyId, Code = "628", Name = "Gastos diversos", Type = "Expense" },

            // 64 - Gastos de personal
            new() { Id = Guid.NewGuid(), CompanyId = companyId, Code = "640", Name = "Sueldos y salarios", Type = "Expense" },
            new() { Id = Guid.NewGuid(), CompanyId = companyId, Code = "641", Name = "Indemnizaciones", Type = "Expense" },
            new() { Id = Guid.NewGuid(), CompanyId = companyId, Code = "642", Name = "Seguridad social a cargo de la empresa", Type = "Expense" },

            // 68 - Dotaciones para amortizaciones
            new() { Id = Guid.NewGuid(), CompanyId = companyId, Code = "680", Name = "Amortizaci�n del inmovilizado material", Type = "Expense" },
            new() { Id = Guid.NewGuid(), CompanyId = companyId, Code = "681", Name = "Amortizaci�n del inmovilizado intangible", Type = "Expense" },

            // =============== IMPUESTOS (Grupo 4/7) ===============
            // 472 - IVA Soportado (Compras)
            new() { Id = Guid.NewGuid(), CompanyId = companyId, Code = "472", Name = "IVA soportado", Type = "Asset" },

            // 477 - IVA Repercutido (Ventas)
            new() { Id = Guid.NewGuid(), CompanyId = companyId, Code = "477", Name = "IVA repercutido", Type = "Liability" },

            // 475 - IVA a compensar
            new() { Id = Guid.NewGuid(), CompanyId = companyId, Code = "475", Name = "IVA a compensar", Type = "Asset" },

            // 4750 - Hacienda P�blica (IVA)
            new() { Id = Guid.NewGuid(), CompanyId = companyId, Code = "4750", Name = "Hacienda P�blica, acreedora por IVA", Type = "Liability" },

            // =============== RETENCIONES (Grupo 4) ===============
            // 476 - Retenciones practicadas
            new() { Id = Guid.NewGuid(), CompanyId = companyId, Code = "476", Name = "Organismos de la Administración Pública, deudores", Type = "Asset" },

            // 4751 - HP acreedora por retenciones practicadas (IRPF)
            new() { Id = Guid.NewGuid(), CompanyId = companyId, Code = "4751", Name = "Hacienda Pública, acreedora por retenciones practicadas", Type = "Liability" },

            // 4770 - HP acreedora por recargo de equivalencia
            new() { Id = Guid.NewGuid(), CompanyId = companyId, Code = "4770", Name = "Hacienda Pública, acreedora por recargo de equivalencia", Type = "Liability" },

            // =============== PERIODIFICACIONES (Grupo 4/5) ===============
            // 480 - Gastos anticipados (pago adelantado de gasto del siguiente período)
            new() { Id = Guid.NewGuid(), CompanyId = companyId, Code = "480", Name = "Gastos anticipados", Type = "Asset" },

            // 485 - Ingresos anticipados (cobro adelantado de ingreso del siguiente período)
            new() { Id = Guid.NewGuid(), CompanyId = companyId, Code = "485", Name = "Ingresos anticipados", Type = "Liability" }
        };
    }
}

using Erp.Application.Common.Interfaces;
using Erp.Modules.Payroll.Application.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Erp.Modules.Payroll.Application.Features.Pdf;

public record GetPayrollLinePdfQuery(Guid LineId) : IRequest<PayrollLinePdfResult>;

public record PayrollLinePdfResult(byte[] PdfBytes, string FileName);

public class GetPayrollLinePdfHandler : IRequestHandler<GetPayrollLinePdfQuery, PayrollLinePdfResult>
{
    private const string Disclaimer =
        "Recibo orientativo generado por el ERP. No sustituye nómina oficial ni documento homologado. " +
        "Revise importes con su gestoría antes de entregar al trabajador.";

    private readonly IPayrollDbContext _ctx;
    private readonly IApplicationDbContext _app;
    private readonly ITenantContext _tenant;
    private readonly IPayrollPayslipPdfService _pdfService;

    public GetPayrollLinePdfHandler(
        IPayrollDbContext ctx,
        IApplicationDbContext app,
        ITenantContext tenant,
        IPayrollPayslipPdfService pdfService)
    {
        _ctx = ctx;
        _app = app;
        _tenant = tenant;
        _pdfService = pdfService;
    }

    public async Task<PayrollLinePdfResult> Handle(GetPayrollLinePdfQuery request, CancellationToken ct)
    {
        var line = await _ctx.PayrollLines
            .Include(l => l.Settlement)
            .Include(l => l.Employee)
            .AsNoTracking()
            .FirstOrDefaultAsync(l => l.Id == request.LineId, ct)
            ?? throw new KeyNotFoundException("Línea de nómina no encontrada.");

        var companyId = _tenant.TenantId ?? throw new InvalidOperationException("Tenant not resolved");
        var company = await _app.Companies.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == companyId, ct)
            ?? throw new InvalidOperationException("Empresa no encontrada.");

        var settlement = line.Settlement
            ?? throw new InvalidOperationException("Liquidación no encontrada.");
        var employee = line.Employee
            ?? throw new InvalidOperationException("Empleado no encontrado.");

        var data = new PayrollPayslipPdfData(
            new PayslipCompanyInfo(company.Name, company.TaxId, company.Address),
            new PayslipEmployeeInfo(
                employee.FullName,
                employee.TaxId,
                employee.SocialSecurityNumber),
            new PayslipLineInfo(
                settlement.Year,
                settlement.Month,
                line.GrossSalary,
                line.CommonContingenciesBase,
                line.EmployeeSocialSecurity,
                line.EmployerSocialSecurity,
                line.IrpfBase,
                line.IrpfRate,
                line.IrpfWithheld,
                line.NetPay,
                Disclaimer));

        var pdfBytes = _pdfService.Generate(data);
        var fileName = $"Recibo_{employee.TaxId}_{settlement.Year}{settlement.Month:D2}.pdf";
        return new PayrollLinePdfResult(pdfBytes, fileName);
    }
}

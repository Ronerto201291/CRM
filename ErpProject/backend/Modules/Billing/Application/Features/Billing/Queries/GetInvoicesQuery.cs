using Erp.Application.DTOs;
using MediatR;
namespace Erp.Modules.Billing.Application.Features.Billing.Queries;
public class GetInvoicesQuery : IRequest<List<InvoiceDto>> { public string? Status { get; set; } }

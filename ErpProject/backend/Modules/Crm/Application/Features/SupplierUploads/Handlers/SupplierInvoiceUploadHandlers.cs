using Erp.Application.Common.Interfaces;
using Erp.Modules.Crm.Application.Features.SupplierUploads.Commands;
using Erp.Modules.Crm.Application.Features.SupplierUploads.Queries;
using Erp.Modules.Crm.Application.Interfaces;
using Erp.Modules.Crm.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Erp.Modules.Crm.Application.Features.SupplierUploads.Handlers;

public class UploadSupplierInvoiceByTokenHandler : IRequestHandler<UploadSupplierInvoiceByTokenCommand, UploadSupplierInvoiceByTokenResult>
{
    private readonly ICrmDbContext _ctx;
    private readonly IFileStorageService _storage;
    private readonly string _bucket;

    public UploadSupplierInvoiceByTokenHandler(ICrmDbContext ctx, IFileStorageService storage, IConfiguration config)
    {
        _ctx = ctx;
        _storage = storage;
        _bucket = config["Storage:SupplierInvoicesBucketName"] ?? "erp-supplier-invoices";
    }

    public async Task<UploadSupplierInvoiceByTokenResult> Handle(UploadSupplierInvoiceByTokenCommand request, CancellationToken ct)
    {
        var supplier = await _ctx.Suppliers.IgnoreQueryFilters()
            .FirstOrDefaultAsync(s => s.PublicUploadToken == request.Token && s.PublicUploadEnabled, ct)
            ?? throw new KeyNotFoundException("Token inválido o desactivado.");

        var objectKey = $"{supplier.CompanyId}/{Guid.NewGuid()}{Path.GetExtension(request.FileName)}";
        await using var stream = new MemoryStream(request.FileContent);
        await _storage.UploadAsync(_bucket, objectKey, stream, request.ContentType, ct);

        var upload = new SupplierInvoiceUpload
        {
            Id = Guid.NewGuid(),
            CompanyId = supplier.CompanyId,
            SupplierId = supplier.Id,
            PublicTokenUsed = request.Token,
            FileName = request.FileName,
            FilePath = objectKey,
            ContentType = request.ContentType,
            Comment = request.Comment,
            Status = "Pending",
        };

        _ctx.SupplierInvoiceUploads.Add(upload);
        await _ctx.SaveChangesAsync(ct);

        return new UploadSupplierInvoiceByTokenResult(upload.Id, "Factura recibida. Será revisada por el equipo de compras.");
    }
}

public class MarkSupplierInvoiceUploadReviewedHandler : IRequestHandler<MarkSupplierInvoiceUploadReviewedCommand, bool>
{
    private readonly ICrmDbContext _ctx;
    public MarkSupplierInvoiceUploadReviewedHandler(ICrmDbContext ctx) => _ctx = ctx;

    public async Task<bool> Handle(MarkSupplierInvoiceUploadReviewedCommand request, CancellationToken ct)
    {
        var upload = await _ctx.SupplierInvoiceUploads.FirstOrDefaultAsync(u => u.Id == request.Id, ct);
        if (upload == null) return false;

        upload.Status = "Reviewed";
        await _ctx.SaveChangesAsync(ct);
        return true;
    }
}

public class GetSupplierInvoiceUploadsHandler : IRequestHandler<GetSupplierInvoiceUploadsQuery, List<SupplierInvoiceUploadDto>>
{
    private readonly ICrmDbContext _ctx;
    public GetSupplierInvoiceUploadsHandler(ICrmDbContext ctx) => _ctx = ctx;

    public async Task<List<SupplierInvoiceUploadDto>> Handle(GetSupplierInvoiceUploadsQuery request, CancellationToken ct)
    {
        return await _ctx.SupplierInvoiceUploads
            .OrderByDescending(u => u.UploadedAt)
            .Select(u => new SupplierInvoiceUploadDto
            {
                Id = u.Id,
                SupplierId = u.SupplierId,
                SupplierName = u.Supplier != null ? u.Supplier.Name : string.Empty,
                FileName = u.FileName,
                Status = u.Status,
                Comment = u.Comment,
                UploadedAt = u.UploadedAt,
            })
            .ToListAsync(ct);
    }
}

public class GetSupplierInvoiceUploadDownloadUrlHandler : IRequestHandler<GetSupplierInvoiceUploadDownloadUrlQuery, string>
{
    private readonly ICrmDbContext _ctx;
    private readonly IFileStorageService _storage;
    private readonly string _bucket;

    public GetSupplierInvoiceUploadDownloadUrlHandler(ICrmDbContext ctx, IFileStorageService storage, IConfiguration config)
    {
        _ctx = ctx;
        _storage = storage;
        _bucket = config["Storage:SupplierInvoicesBucketName"] ?? "erp-supplier-invoices";
    }

    public async Task<string> Handle(GetSupplierInvoiceUploadDownloadUrlQuery request, CancellationToken ct)
    {
        var upload = await _ctx.SupplierInvoiceUploads.FirstOrDefaultAsync(u => u.Id == request.Id, ct)
            ?? throw new KeyNotFoundException("Factura de proveedor no encontrada.");

        return await _storage.GetSignedUrlAsync(_bucket, upload.FilePath, TimeSpan.FromMinutes(15));
    }
}

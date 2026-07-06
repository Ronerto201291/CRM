using Erp.Application.Common.Interfaces;
using Erp.Application.Features.Documents.Commands;
using Erp.Application.Features.Documents.Queries;
using Erp.Domain.Entities.Core;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Erp.Application.Features.Documents.Handlers;

public class UploadDocumentHandler : IRequestHandler<UploadDocumentCommand, DocumentDto>
{
    private const string Bucket = "erp-documents";
    private const long MaxSizeBytes = 20_000_000; // 20 MB

    private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "application/pdf", "image/jpeg", "image/png", "image/webp",
        "application/msword",
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        "application/vnd.ms-excel",
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
    };

    private readonly IApplicationDbContext _ctx;
    private readonly ITenantContext _tenant;
    private readonly IFileStorageService _storage;

    public UploadDocumentHandler(IApplicationDbContext ctx, ITenantContext tenant, IFileStorageService storage)
    {
        _ctx = ctx;
        _tenant = tenant;
        _storage = storage;
    }

    public async Task<DocumentDto> Handle(UploadDocumentCommand request, CancellationToken ct)
    {
        var companyId = _tenant.TenantId ?? throw new UnauthorizedAccessException("No tenant context.");

        if (request.Content.Length == 0)
            throw new InvalidOperationException("El archivo está vacío.");
        if (request.Content.Length > MaxSizeBytes)
            throw new InvalidOperationException($"El archivo supera el límite de {MaxSizeBytes / 1_000_000} MB.");
        if (!AllowedContentTypes.Contains(request.ContentType))
            throw new InvalidOperationException($"Tipo de archivo no permitido: {request.ContentType}.");

        var objectKey = $"{companyId}/{Guid.NewGuid()}{Path.GetExtension(request.FileName)}";
        await using (var stream = new MemoryStream(request.Content))
        {
            await _storage.UploadAsync(Bucket, objectKey, stream, request.ContentType, ct);
        }

        var document = new Document
        {
            CompanyId = companyId,
            UploadedByUserId = request.UploadedByUserId,
            FileName = request.FileName,
            ContentType = request.ContentType,
            SizeBytes = request.Content.Length,
            ObjectKey = objectKey,
            EntityType = request.EntityType,
            EntityId = request.EntityId,
            Description = request.Description,
        };

        _ctx.Documents.Add(document);
        await _ctx.SaveChangesAsync(ct);

        return ToDto(document);
    }

    private static DocumentDto ToDto(Document d) => new(
        d.Id, d.FileName, d.ContentType, d.SizeBytes, d.EntityType, d.EntityId, d.Description, d.CreatedAt);
}

public class GetDocumentsHandler : IRequestHandler<GetDocumentsQuery, PaginatedDocumentsResult>
{
    private readonly IApplicationDbContext _ctx;
    private readonly ITenantContext _tenant;

    public GetDocumentsHandler(IApplicationDbContext ctx, ITenantContext tenant)
    {
        _ctx = ctx;
        _tenant = tenant;
    }

    public async Task<PaginatedDocumentsResult> Handle(GetDocumentsQuery request, CancellationToken ct)
    {
        var companyId = _tenant.TenantId ?? throw new UnauthorizedAccessException("No tenant context.");
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);

        var query = _ctx.Documents.Where(d => d.CompanyId == companyId);
        if (!string.IsNullOrWhiteSpace(request.EntityType))
            query = query.Where(d => d.EntityType == request.EntityType);
        if (request.EntityId.HasValue)
            query = query.Where(d => d.EntityId == request.EntityId);

        var totalCount = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(d => d.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(d => new DocumentDto(
                d.Id, d.FileName, d.ContentType, d.SizeBytes, d.EntityType, d.EntityId, d.Description, d.CreatedAt))
            .ToListAsync(ct);

        return new PaginatedDocumentsResult(items, totalCount);
    }
}

public class GetDocumentDownloadUrlHandler : IRequestHandler<GetDocumentDownloadUrlQuery, string>
{
    private const string Bucket = "erp-documents";

    private readonly IApplicationDbContext _ctx;
    private readonly ITenantContext _tenant;
    private readonly IFileStorageService _storage;

    public GetDocumentDownloadUrlHandler(IApplicationDbContext ctx, ITenantContext tenant, IFileStorageService storage)
    {
        _ctx = ctx;
        _tenant = tenant;
        _storage = storage;
    }

    public async Task<string> Handle(GetDocumentDownloadUrlQuery request, CancellationToken ct)
    {
        var companyId = _tenant.TenantId ?? throw new UnauthorizedAccessException("No tenant context.");

        var document = await _ctx.Documents
            .FirstOrDefaultAsync(d => d.Id == request.Id && d.CompanyId == companyId, ct)
            ?? throw new KeyNotFoundException("Documento no encontrado.");

        return await _storage.GetSignedUrlAsync(Bucket, document.ObjectKey, TimeSpan.FromMinutes(15));
    }
}

public class DeleteDocumentHandler : IRequestHandler<DeleteDocumentCommand, bool>
{
    private const string Bucket = "erp-documents";

    private readonly IApplicationDbContext _ctx;
    private readonly ITenantContext _tenant;
    private readonly IFileStorageService _storage;

    public DeleteDocumentHandler(IApplicationDbContext ctx, ITenantContext tenant, IFileStorageService storage)
    {
        _ctx = ctx;
        _tenant = tenant;
        _storage = storage;
    }

    public async Task<bool> Handle(DeleteDocumentCommand request, CancellationToken ct)
    {
        var companyId = _tenant.TenantId ?? throw new UnauthorizedAccessException("No tenant context.");

        var document = await _ctx.Documents
            .FirstOrDefaultAsync(d => d.Id == request.Id && d.CompanyId == companyId, ct);
        if (document is null) return false;

        await _storage.DeleteAsync(Bucket, document.ObjectKey, ct);
        _ctx.Documents.Remove(document);
        await _ctx.SaveChangesAsync(ct);
        return true;
    }
}

using Erp.Application.Common.Events;
using Erp.Application.Common.Interfaces;
using Erp.Modules.Expenses.Application.Interfaces;
using Erp.Modules.Expenses.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Erp.Modules.Expenses.Application.Features.Expenses.Commands;

public record UploadExpenseByTokenResult(Guid UploadId, string Message);

public class UploadExpenseByTokenCommand : IRequest<UploadExpenseByTokenResult>
{
    public string Token { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public byte[] FileContent { get; set; } = [];
    public string? Comment { get; set; }
}

public class UploadExpenseByTokenHandler : IRequestHandler<UploadExpenseByTokenCommand, UploadExpenseByTokenResult>
{
    private readonly IExpensesDbContext _expenses;
    private readonly IApplicationDbContext _app;
    private readonly IPublisher _publisher;
    private readonly IFileStorageService _storage;
    private readonly string _bucket;

    public UploadExpenseByTokenHandler(
        IExpensesDbContext expenses,
        IApplicationDbContext app,
        IPublisher publisher,
        IFileStorageService storage,
        IConfiguration config)
    {
        _expenses = expenses;
        _app = app;
        _publisher = publisher;
        _storage = storage;
        _bucket = config["Storage:BucketName"] ?? "erp-expenses";
    }

    public async Task<UploadExpenseByTokenResult> Handle(UploadExpenseByTokenCommand request, CancellationToken ct)
    {
        var company = await _app.Companies.IgnoreQueryFilters()
            .FirstOrDefaultAsync(c => c.PublicUploadToken == request.Token && c.QrUploadEnabled, ct)
            ?? throw new KeyNotFoundException("Token invalido o desactivado.");

        var objectKey = $"{company.Id}/{Guid.NewGuid()}{Path.GetExtension(request.FileName)}";
        await using var stream = new MemoryStream(request.FileContent);
        await _storage.UploadAsync(_bucket, objectKey, stream, request.ContentType, ct);

        var upload = new ExpenseUpload
        {
            Id = Guid.NewGuid(),
            CompanyId = company.Id,
            PublicTokenUsed = request.Token,
            FileName = request.FileName,
            FilePath = objectKey,
            ContentType = request.ContentType,
            Comment = request.Comment,
            Status = "Pending"
        };

        _expenses.ExpenseUploads.Add(upload);
        await _expenses.SaveChangesAsync(ct);

        await _publisher.Publish(new ExpenseUploadCreatedEvent
        {
            UploadId = upload.Id,
            CompanyId = company.Id,
            FileName = request.FileName,
            Comment = request.Comment
        }, ct);

        return new UploadExpenseByTokenResult(upload.Id, "Documento recibido. Sera procesado automaticamente.");
    }
}

using Erp.Application.DTOs;
using MediatR;

namespace Erp.Modules.Accounting.Application.Queries;

public class GetJournalEntriesQuery : IRequest<List<JournalEntryDto>> { }

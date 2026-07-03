using Erp.Domain.Entities.Core;

namespace Erp.Application.Common.Interfaces;

public interface IJwtProvider
{
    string Generate(User user, Guid? activeCompanyId = null);
}

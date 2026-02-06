using api.Helpers;

namespace api.Interfaces;

public interface IMemberRepository
{
    public Task<OperationResult<PagedList<AppUser>>> GetAllAsync(MemberParams memberParams, CancellationToken cancellationToken);

    public Task<OperationResult<MemberDto>> GetByUserNameAsync(string userName, CancellationToken cancellationToken);
}

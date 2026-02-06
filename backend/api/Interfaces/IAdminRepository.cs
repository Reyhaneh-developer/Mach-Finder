namespace api.Interfaces;

public interface IAdminRepository
{
    public Task<OperationResult<IEnumerable<UserWithRoleDto>>> GetUsersWithRolesAsync();
    public Task<OperationResult<DeleteResult>> DeleteUserAsync(string targetUserName, CancellationToken cancellationToken);
}
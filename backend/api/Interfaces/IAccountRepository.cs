namespace api.Interfaces;

public interface IAccountRepository
{
    public Task<OperationResult<LoggedInDto>> RegisterAsync(RegisterDto userInput, CancellationToken cancellationToken);
    public Task<OperationResult<LoggedInDto>> LoginAsync(LoginDto userInput, CancellationToken cancellationToken);
    public Task<OperationResult<DeleteResult>> DeleteByIdAsync(string userId, CancellationToken cancellationToken);
    public Task<OperationResult<LoggedInDto>> ReloadLoggedInUserAsync(string userId, string token, CancellationToken cancellationToken);
    public Task<OperationResult<UpdateResult>> UpdateLastActive(string userId, CancellationToken cancellationToken);
}
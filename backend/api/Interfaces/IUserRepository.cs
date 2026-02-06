namespace api.Interfaces;

public interface IUserRepository
{
    public Task<OperationResult<AppUser>> GetByIdAsync(string userId, CancellationToken cancellationToken);
    public Task<OperationResult<UpdateResult>> UpdateByIdAsync(string userId, UserUpdateDto userInput, CancellationToken cancellationToken);
    public Task<OperationResult<Photo>> UploadPhotoAsync(IFormFile file, string userId, CancellationToken cancellationToken);
    public Task<OperationResult<UpdateResult>> SetMainPhotoAsync(string userId, string photoUrlIn, CancellationToken cancellationToken);
    public Task<OperationResult<UpdateResult>> DeletePhotoAsync(string userId, string? urlIn, CancellationToken cancellationToken);
}

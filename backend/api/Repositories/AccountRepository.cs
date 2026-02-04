using api.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;

namespace api.Repositories;

[AllowAnonymous]
public class AccountRepository : IAccountRepository
{

    #region Mongodb
    private readonly IMongoCollection<AppUser> _collection;
    private readonly ITokenService _tokenService;
    private readonly UserManager<AppUser> _userManager;

    // Dependency Injection
    public AccountRepository(IMongoClient client, IMyMongoDbSettings dbSettings, ITokenService tokenService, UserManager<AppUser> userManager)
    {
        var dbName = client.GetDatabase(dbSettings.DatabaseName);
        _collection = dbName.GetCollection<AppUser>("users");
        _userManager = userManager;
        _tokenService = tokenService;
    }
    #endregion

    public async Task<OperationResult<LoggedInDto>> RegisterAsync(RegisterDto userInput, CancellationToken cancellationToken)
    {
        AppUser appUser = Mappers.ConvertRegisterDtoToAppUser(userInput);

        IdentityResult userCreationResult = await _userManager.CreateAsync(appUser, userInput.Password);

        if (!userCreationResult.Succeeded)
        {
            var errors = userCreationResult.Errors
                .Select(e => e.Description)
                .ToList();

           return new OperationResult<LoggedInDto>(
                IsSuccess: false,
                Error: new CustomError(
                    ErrorCode.NetIdentityFailed,
                    "User creation failed!"
                )
           );
        }

        IdentityResult roleResult = await _userManager.AddToRoleAsync(appUser, "member");

        if (!roleResult.Succeeded)
        {
            var roleErrors = roleResult.Errors
                .Select(e => e.Description)
                .ToList();

            return new OperationResult<LoggedInDto>(
                IsSuccess: false,
                Error: new CustomError(
                    ErrorCode.NetIdentityRoleFailed,
                    "Add role to user failed!"
                )
            );
        }

        string? token = await _tokenService.CreateToken(appUser);

        if (string.IsNullOrEmpty(token))
        {
            return new OperationResult<LoggedInDto>(
                IsSuccess: false,
                Error: new CustomError(
                    ErrorCode.IsTokenFailed,
                    "Token creation failed!"
                )
            );
        }

        LoggedInDto loggedInDto = Mappers.ConvertAppUserToLoggedInDto(appUser, token);

        return new OperationResult<LoggedInDto>(
            IsSuccess: true,
            Result: loggedInDto,
            null    
        );
    }


    public async Task<OperationResult<LoggedInDto>> LoginAsync(LoginDto userInput, CancellationToken cancellationToken)
    {
        AppUser? appUser = await _userManager.FindByEmailAsync(userInput.Email);

        if (appUser is null)
        {
            return new OperationResult<LoggedInDto>(
              IsSuccess:false,
              Error: new CustomError(
                ErrorCode.IsNotFound,
                ""
              )
             );
            
        }

        bool isPassCorrect = await _userManager.CheckPasswordAsync(appUser, userInput.Password);

        if (!isPassCorrect)
        {
           return new OperationResult<LoggedInDto>(
              IsSuccess:false,
              Error: new CustomError(
                ErrorCode.IsWrongCreds,
                ""
              )
             );

        }

        string? token = await _tokenService.CreateToken(appUser);

        if (string.IsNullOrEmpty(token))
        {
            return new OperationResult<loggedInDto>(
                IsSuccess: false,
                Error: new CustomError(
                    ErrorCode.IsTokenFailed,
                    ""
                )
            );
        }

        LoggedInDto loggedInDto =  Mappers.ConvertAppUserToLoggedInDto(appUser, token);

        return new OperationResult<LoggedInDto>(
            IsSuccess: true,
            Result: loggedInDto,
            null    
        );
    }

    [Authorize]
    public async Task<OperationResult<DeleteResult>> DeleteByIdAsync(string userId, CancellationToken cancellationToken)
    {
        AppUser appUser = await _collection.Find<AppUser>(doc => doc.Id.ToString() == userId).FirstOrDefaultAsync(cancellationToken);

        if (appUser is null)
        {
r           return new OperationResult<DeleteResult>(
              IsSuccess:false,
              Error: new CustomError(
                ErrorCode.IsNotFound,
                ""
              )
              );       
              }

        DeleteResult deleteResult =await _collection.DeleteOneAsync<AppUser>(doc => doc.Id.ToString() == userId, cancellationToken);
       
        return new OperationResult<DeleteResult>(
            IsSuccess:true,
            result:deleteResult,
            null
        )
    }

    public async Task<OperationResult<LoggedInDto>> ReloadLoggedInUserAsync(string userId, string token, CancellationToken cancellationToken)
    {
        AppUser? appUser = await _collection.Find<AppUser>(doc => doc.Id.ToString() == userId).FirstOrDefaultAsync(cancellationToken);

        if (appUser is null)
         return new OperationResult<DeleteResult>(
              IsSuccess:false,
              Error: new CustomError(
                ErrorCode.IsNotFound,
                ""
              )
             );
        
         LoggedInDto loggedInDto =Mappers.ConvertAppUserToLoggedInDto(appUser, token);

        return new OperationResult<LoggedInDto>(
            IsSuccess: true,
            Result: loggedInDto,
            null    
        );
    }

    public async Task<OperationResult<UpdateResult>> UpdateLastActive(string userId, CancellationToken cancellationToken)
    {
        if (appUser is null)
         return new OperationResult<UpdateResult>(
              IsSuccess:false,
              Error: new CustomError(
                ErrorCode.IsNotFound,
                ""
              )
             );

        UpdateDefinition<AppUser> updateUserLastActive = Builders<AppUser>.Update.
            Set(appUser => appUser.LastActive, DateTime.UtcNow);

        UpdateResult updateDef = await _collection.UpdateOneAsync(doc => doc.Id.ToString() == userId, updateUserLastActive, null, cancellationToken);
        
        return new OperationResult<UpdateResult>(
            IsSuccess: true,
            Result: updateDef,
            null    
        );
    }
}
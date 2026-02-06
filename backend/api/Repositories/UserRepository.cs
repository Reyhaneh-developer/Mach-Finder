namespace api.Repositories;

public class UserRepository : IUserRepository
{
    private readonly IMongoCollection<AppUser> _collection;
    private readonly ITokenService _tokenService;
    private readonly IPhotoService _photoService;
    private readonly ILogger<UserRepository> _logger;

    // Dependency Injection
    public UserRepository(IMongoClient client, IMyMongoDbSettings dbSettings, ITokenService tokenService, IPhotoService photoService, ILogger<UserRepository> logger)
    {
        var dbName = client.GetDatabase(dbSettings.DatabaseName);
        _collection = dbName.GetCollection<AppUser>("users");

        _tokenService = tokenService;
        _photoService = photoService;
        _logger = logger;
    }

    public async Task<OperationResult<AppUser>> GetByIdAsync(string userId, CancellationToken cancellationToken)
    {
        AppUser? appUser = await _collection.Find(doc => doc.Id.ToString() == userId).SingleOrDefaultAsync(cancellationToken);

        if (appUser is null)
            return new OperationResult<DeleteResult>(
                         IsSuccess: false,
                         Error: new CustomError(
                           ErrorCode.IsNotFound,
                           ""
                         )

                        );

        return new OperationResult<AppUser>(
                   IsSuccess: true,
                   Result: appUser,
                   null
               );
    }

    public async Task<UpdateResult> UpdateByIdAsync(string userId, UserUpdateDto userInput, CancellationToken cancellationToken)
    {
        UpdateDefinition<AppUser> updateDef = Builders<AppUser>.Update
        .Set(appUser => appUser.Introduction, userInput.Introduction.Trim())
        .Set(appUser => appUser.LookingFor, userInput.LookingFor.Trim())
        .Set(appUser => appUser.Interests, userInput.Interests.Trim())
        .Set(appUser => appUser.City, userInput.City.Trim().ToLower())
        .Set(appUser => appUser.Country, userInput.Country.Trim().ToLower()); 

        return await _collection.UpdateOneAsync(user
            => user.Id.ToString() == userId, updateDef, null, cancellationToken);
    }

    public async Task<OperationResult<Photo>> UploadPhotoAsync(IFormFile file, string userId, CancellationToken cancellationToken)
    {
        AppUser? appUser = await GetByIdAsync(userId, cancellationToken);


        if (appUser is null)
            return new OperationResult<Photo>(
                         IsSuccess: false,
                         Error: new CustomError(
                           ErrorCode.IsNotFound,
                           ""
                         )

                        );

        // ObjectId objectId = ObjectId.Parse(userId);

        if (!ObjectId.TryParse(userId, out var objectId))
            return new OperationResult<Photo>(
              IsSuccess: false,
              Error: new CustomError(
                ErrorCode.IsTokenFailed,
                ""
              )

             );

        string[]? imageUrls = await _photoService.AddPhotoToDiskAsync(file, objectId);

        if (imageUrls is not null)
        {
            Photo photo;

            photo = appUser.Photos.Count == 0
                ? Mappers.ConvertPhotoUrlsToPhoto(imageUrls, isMain: true)
                : Mappers.ConvertPhotoUrlsToPhoto(imageUrls, isMain: false);

            appUser.Photos.Add(photo);

            UpdateDefinition<AppUser> updatedUser = Builders<AppUser>.Update
                .Set(doc => doc.Photos, appUser.Photos);

            UpdateResult result = await _collection.UpdateOneAsync(doc => doc.Id.ToString() == userId, updatedUser, null, cancellationToken);

            // return result.ModifiedCount == 1 ? photo : null;

            if (result.ModifiedCount == 1)
            {
                return new OperationResult<Photo>(
                    true,
                    photo,
                    null
                );
            }

            return new OperationResult<Photo>(
                false,
                Error: new CustomError(
                    ErrorCode.UpdateFailed,
                    "Photo upload failed try again."
                )
            );
        }

        return new OperationResult<Photo>(
            false,
            Error: new CustomError(
                ErrorCode.UrlsAreNull,
                "Image urls are null"
            )
        );
    }

    public async Task<UpdateResult?> SetMainPhotoAsync(string userId, string photoUrlIn, CancellationToken cancellationToken)
    {
        #region  UNSET the previous main photo: Find the photo with IsMain True; update IsMain to False
        // set query
        FilterDefinition<AppUser>? filterOld = Builders<AppUser>.Filter
            .Where(appUser =>
                appUser.Id.ToString() == userId && appUser.Photos.Any<Photo>(photo => photo.IsMain == true));

        UpdateDefinition<AppUser>? updateOld = Builders<AppUser>.Update
            .Set(appUser => appUser.Photos.FirstMatchingElement().IsMain, false);

        // UpdateOneAsync(appUser => appUser.Photos.IsMain, false, null, cancellationToken);
        await _collection.UpdateOneAsync(filterOld, updateOld, null, cancellationToken);
        #endregion

        #region  SET the new main photo: find new photo by its Url_165; update IsMain to True
        FilterDefinition<AppUser>? filterNew = Builders<AppUser>.Filter
            .Where(appUser =>
                appUser.Id.ToString() == userId && appUser.Photos.Any<Photo>(photo => photo.Url_165 == photoUrlIn));

        UpdateDefinition<AppUser>? updateNew = Builders<AppUser>.Update
            .Set(appUser => appUser.Photos.FirstMatchingElement().IsMain, true);

        return await _collection.UpdateOneAsync(filterNew, updateNew, null, cancellationToken);
        #endregion
    }

    public async Task<OperationResult<UpdateResult>> DeletePhotoAsync(string userId, string? url_165_In, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(url_165_In))
        {
            return new OperationResult<UpdateResult>(
                IsSuccess: false,
                Error: new CustomError(
                    ErrorCode.InvalidUrl,
                    "URL cannot be null or empty"
                )
            );
        }

        // Find the photo in AppUser
        Photo photo = await _collection.AsQueryable()
            .Where(appUser => appUser.Id.ToString() == userId) // filter by user Id
            .SelectMany(appUser => appUser.Photos) // flatten the Photos array
            .Where(photo => photo.Url_165 == url_165_In) // filter by photo url
            .FirstOrDefaultAsync(cancellationToken); // return the photo or null

        if (photo is null)
        {
            return new OperationResult<Photo>(
                IsSuccess: false,
                Error: new CustomError(
                    ErrorCode.IsNotFound,
                    "Photo not found"
                )
            );
        } // Warning: should be handled with Exception handling Middlewear to log the app's bug since it's a bug!

        if (photo.IsMain)
        {
            return new OperationResult<Photo>(
                IsSuccess: false,
                Error: new CustomError(
                    ErrorCode.InvalidOperation,
                    "Cannot delete main photo"
                )
            );
        } // prevent from deleting main photo!

        bool isDeleteSuccess = await _photoService.DeletePhotoFromDisk(photo);
        if (!isDeleteSuccess)
        {
            _logger.LogError("Delete Photo form disk failed for photoId: {PhotoId}", photo.Id);

            return new OperationResult<bool>(
                IsSuccess: false,
                Error: new CustomError(
                    ErrorCode.FileSystemError,
                    "Could not delete photo file from storage"
                )
            );
        }

        UpdateDefinition<AppUser> update = Builders<AppUser>.Update
        .PullFilter(appUser => appUser.Photos, photo => photo.Url_165 == url_165_In);

        UpdateResult updateResult = await _collection.UpdateOneAsync<AppUser>(appUser => appUser.Id.ToString() == userId, update, null, cancellationToken);

        return new OperationResult<UpdateResult>(
            IsSuccess: true,
            ReadResult: updateResult,
            null
        );
    }
}

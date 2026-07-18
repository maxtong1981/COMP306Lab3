using Amazon.S3;
using Amazon.S3.Model;
using Amazon.S3.Transfer;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace _301379036_chen_lab3.Services
{
    /// <summary>
    /// Handles episode audio/video files stored in Amazon S3.
    /// </summary>
    public class S3Service
    {
        private readonly IAmazonS3 _s3Client;
        private readonly ILogger<S3Service> _logger;
        private readonly string _bucketName;

        // Adjust this list if your application accepts additional media formats.
        private static readonly HashSet<string> AllowedExtensions = new(
            StringComparer.OrdinalIgnoreCase)
        {
            ".mp3", ".wav", ".m4a", ".aac", ".ogg",
            ".mp4", ".mov", ".avi", ".mkv", ".webm"
        };

        public S3Service(IAmazonS3 s3Client,
            IConfiguration configuration,
            ILogger<S3Service> logger)
        {
            _s3Client = s3Client ?? throw new ArgumentNullException(nameof(s3Client));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            // appsettings.json: "AWS": { "S3BucketName": "comp306-lab3-bryan" }
            _bucketName = configuration["AWS:S3BucketName"]
                ?? "comp306-lab3-bryan";
        }

        //public async Task<string> UploadFileAsync(IFormFile file)
        //{
        //    string fileName = $"{Guid.NewGuid()}_{file.FileName}";
        //    using var stream = file.OpenReadStream();
        //    var uploadRequest = new TransferUtilityUploadRequest
        //    {
        //        InputStream = stream,
        //        Key = fileName,
        //        BucketName = _bucketName,
        //        ContentType = file.ContentType
        //    };

        //    var transferUtility = new TransferUtility(_s3Client);
        //    await transferUtility.UploadAsync(uploadRequest);

        //    return $"https://{_bucketName}.s3.amazonaws.com/{fileName}";
        //}
        /// <summary>
        /// Uploads an episode audio/video file to S3 and returns its absolute URL.
        /// Store the returned URL in EpisodeModel.AudioFileURL.
        /// </summary>
        public async Task<string> UploadFileAsync(
            IFormFile file,
            CancellationToken cancellationToken = default)
        {
            ValidateMediaFile(file);

            string objectKey = CreateObjectKey(file.FileName);

            try
            {
                await using Stream stream = file.OpenReadStream();

                var uploadRequest = new TransferUtilityUploadRequest
                {
                    BucketName = _bucketName,
                    Key = objectKey,
                    InputStream = stream,
                    ContentType = string.IsNullOrWhiteSpace(file.ContentType)
                        ? "application/octet-stream"
                        : file.ContentType,
                    AutoCloseStream = false
                };

                // Useful metadata for inspecting the object in the AWS console.
                uploadRequest.Metadata["original-file-name"] =
                    Path.GetFileName(file.FileName);

                var transferUtility = new TransferUtility(_s3Client);
                await transferUtility.UploadAsync(uploadRequest, cancellationToken);

                string fileUrl = BuildObjectUrl(objectKey);

                _logger.LogInformation(
                    "Uploaded media file to S3. Bucket: {Bucket}; Key: {Key}",
                    _bucketName,
                    objectKey);

                return fileUrl;
            }
            catch (AmazonS3Exception ex)
            {
                _logger.LogError(
                    ex,
                    "AWS S3 upload failed. Bucket: {Bucket}; Key: {Key}",
                    _bucketName,
                    objectKey);

                throw new InvalidOperationException(
                    "The multimedia file could not be uploaded to Amazon S3.",
                    ex);
            }
        }

        /// <summary>
        /// Deletes an S3 object. The argument may be either an S3 object key or
        /// the absolute URL previously returned by UploadFileAsync.
        /// </summary>
        public async Task DeleteFileAsync(
            string keyOrUrl,
            CancellationToken cancellationToken = default)
        {
            string objectKey = ExtractObjectKey(keyOrUrl);

            try
            {
                var deleteRequest = new DeleteObjectRequest
                {
                    BucketName = _bucketName,
                    Key = objectKey
                };

                await _s3Client.DeleteObjectAsync(deleteRequest, cancellationToken);

                _logger.LogInformation(
                    "Deleted media file from S3. Bucket: {Bucket}; Key: {Key}",
                    _bucketName,
                    objectKey);
            }
            catch (AmazonS3Exception ex)
            {
                _logger.LogError(
                    ex,
                    "AWS S3 delete failed. Bucket: {Bucket}; Key: {Key}",
                    _bucketName,
                    objectKey);

                throw new InvalidOperationException(
                    "The multimedia file could not be deleted from Amazon S3.",
                    ex);
            }
        }

        /// <summary>
        /// Uploads a replacement file and then deletes the old S3 object.
        /// Returns the new absolute URL that should replace AudioFileURL in SQL Server.
        /// </summary>
        public async Task<string> ReplaceFileAsync(
            IFormFile newFile,
            string? oldKeyOrUrl,
            CancellationToken cancellationToken = default)
        {
            // Upload first so the episode does not lose its working media file if
            // the new upload fails.
            string newFileUrl = await UploadFileAsync(newFile, cancellationToken);

            if (string.IsNullOrWhiteSpace(oldKeyOrUrl))
            {
                return newFileUrl;
            }

            try
            {
                await DeleteFileAsync(oldKeyOrUrl, cancellationToken);
                return newFileUrl;
            }
            catch
            {
                // Roll back the new object if the old object cannot be deleted.
                // This prevents an unused replacement object from being left in S3.
                try
                {
                    await DeleteFileAsync(newFileUrl, cancellationToken);
                }
                catch (Exception rollbackException)
                {
                    _logger.LogError(
                        rollbackException,
                        "S3 replacement rollback failed for new URL: {NewFileUrl}",
                        newFileUrl);
                }

                throw;
            }
        }

        /// <summary>
        /// Converts either a full S3 URL or a raw object key into the object key.
        /// </summary>
        public string ExtractObjectKey(string keyOrUrl)
        {
            if (string.IsNullOrWhiteSpace(keyOrUrl))
            {
                throw new ArgumentException(
                    "The S3 object key or URL is required.",
                    nameof(keyOrUrl));
            }

            string value = keyOrUrl.Trim();

            if (!Uri.TryCreate(value, UriKind.Absolute, out Uri? uri))
            {
                return Uri.UnescapeDataString(value.TrimStart('/'));
            }

            // Supports URLs such as:
            // https://bucket-name.s3.amazonaws.com/episodes/file.mp3
            // https://bucket-name.s3.ca-central-1.amazonaws.com/episodes/file.mp3
            return Uri.UnescapeDataString(uri.AbsolutePath.TrimStart('/'));
        }

        private static void ValidateMediaFile(IFormFile file)
        {
            if (file is null)
            {
                throw new ArgumentNullException(nameof(file));
            }

            if (file.Length <= 0)
            {
                throw new ArgumentException("The uploaded file is empty.", nameof(file));
            }

            string extension = Path.GetExtension(file.FileName);

            if (string.IsNullOrWhiteSpace(extension) ||
                !AllowedExtensions.Contains(extension))
            {
                throw new ArgumentException(
                    $"Unsupported media format '{extension}'. " +
                    $"Allowed formats: {string.Join(", ", AllowedExtensions)}",
                    nameof(file));
            }

            // Browser content types are user-provided and cannot be trusted as the
            // only security check, but this rejects obvious non-media uploads.
            if (!string.IsNullOrWhiteSpace(file.ContentType) &&
                !file.ContentType.StartsWith("audio/", StringComparison.OrdinalIgnoreCase) &&
                !file.ContentType.StartsWith("video/", StringComparison.OrdinalIgnoreCase) &&
                !file.ContentType.Equals("application/octet-stream", StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException(
                    "Only audio or video files can be uploaded.",
                    nameof(file));
            }
        }

        private static string CreateObjectKey(string originalFileName)
        {
            string extension = Path.GetExtension(originalFileName).ToLowerInvariant();
            string baseName = Path.GetFileNameWithoutExtension(originalFileName);

            // Keep the key URL-safe and prevent path traversal characters.
            string safeBaseName = new string(
                baseName
                    .Where(character => char.IsLetterOrDigit(character) ||
                                        character is '-' or '_')
                    .ToArray());

            if (string.IsNullOrWhiteSpace(safeBaseName))
            {
                safeBaseName = "episode-media";
            }

            return $"episodes/{DateTime.UtcNow:yyyy/MM}/{Guid.NewGuid():N}_{safeBaseName}{extension}";
        }

        private string BuildObjectUrl(string objectKey)
        {
            string escapedKey = string.Join(
                "/",
                objectKey
                    .Split('/', StringSplitOptions.RemoveEmptyEntries)
                    .Select(Uri.EscapeDataString));

            string regionName = _s3Client.Config.RegionEndpoint?.SystemName ?? "us-east-1";

            return regionName.Equals("us-east-1", StringComparison.OrdinalIgnoreCase)
                ? $"https://{_bucketName}.s3.amazonaws.com/{escapedKey}"
                : $"https://{_bucketName}.s3.{regionName}.amazonaws.com/{escapedKey}";
        }
    }
}

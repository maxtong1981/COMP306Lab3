using _301379036_chen_lab3.Models;
using Amazon.DynamoDBv2.DataModel;
using System.Security.Cryptography;
namespace _301379036_chen_lab3.Services
{
    public sealed class DynamoDbCommentService : ICommentService
    {
        private static readonly TimeSpan EditWindow =
            TimeSpan.FromHours(24);

        private readonly IDynamoDBContext _context;
        private readonly ILogger<DynamoDbCommentService> _logger;
        private readonly string _tableName;

        public DynamoDbCommentService(
            IDynamoDBContext context,
            IConfiguration configuration,
            ILogger<DynamoDbCommentService> logger)
        {
            _context = context;
            _logger = logger;

            _tableName =
                configuration["AWS:DynamoDbCommentsTableName"]
                ?? "Comments";
        }

        /// <summary>
        /// Adds a new comment to DynamoDB.
        /// </summary>
        public async Task<CommentsModel> AddCommentAsync(
            string episodeId,
            string podcastId,
            string userId,
            string text,
            CancellationToken cancellationToken = default)
        {
            ValidateCreateInput(
                episodeId,
                podcastId,
                userId,
                text
            );

            var comment = new CommentsModel
            {
                EpisodeId = episodeId.Trim(),
                CommentId = Guid.NewGuid().ToString(),
                PodcastId = podcastId.Trim(),
                UserId = userId.Trim(),
                Text = text.Trim(),
                Timestamp = DateTime.UtcNow
            };

            await _context.SaveAsync(
                comment,
                CreateOperationConfig(),
                cancellationToken
            );

            _logger.LogInformation(
                "Comment {CommentId} was added to episode {EpisodeId} by user {UserId}.",
                comment.CommentId,
                comment.EpisodeId,
                comment.UserId
            );

            return comment;
        }

        /// <summary>
        /// Returns all comments for one episode.
        /// Comments are sorted by creation time, newest first.
        /// </summary>
        public async Task<IReadOnlyList<CommentsModel>>
            GetCommentsByEpisodeAsync(
                string episodeId,
                CancellationToken cancellationToken = default)
        {
            ValidateRequiredId(
                episodeId,
                nameof(episodeId),
                "Episode ID is required."
            );

            IAsyncSearch<CommentsModel> search =
                _context.QueryAsync<CommentsModel>(
                    episodeId.Trim(),
                    CreateOperationConfig()
                );

            List<CommentsModel> comments =
                await search.GetRemainingAsync(
                    cancellationToken
                );

            return comments
                .OrderByDescending(comment => comment.Timestamp)
                .ToList();
        }

        /// <summary>
        /// Returns one comment by episode ID and comment ID.
        /// </summary>
        public async Task<CommentsModel?> GetCommentAsync(
            string episodeId,
            string commentId,
            CancellationToken cancellationToken = default)
        {
            ValidateRequiredId(
                episodeId,
                nameof(episodeId),
                "Episode ID is required."
            );

            ValidateRequiredId(
                commentId,
                nameof(commentId),
                "Comment ID is required."
            );

            return await _context.LoadAsync<CommentsModel>(
                episodeId.Trim(),
                commentId.Trim(),
                CreateOperationConfig(),
                cancellationToken
            );
        }

        /// <summary>
        /// Updates a comment after checking ownership and the 24-hour rule.
        /// </summary>
        public async Task<CommentUpdateResult>
            UpdateCommentAsync(
                string episodeId,
                string commentId,
                string currentUserId,
                string newText,
                CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(currentUserId))
            {
                return new CommentUpdateResult
                {
                    Status = CommentUpdateStatus.NotOwner,
                    Message = "A valid current user ID is required."
                };
            }

            if (string.IsNullOrWhiteSpace(newText))
            {
                return new CommentUpdateResult
                {
                    Status = CommentUpdateStatus.InvalidText,
                    Message = "Comment text cannot be empty."
                };
            }

            CommentsModel? comment =
                await GetCommentAsync(
                    episodeId,
                    commentId,
                    cancellationToken
                );

            if (comment == null)
            {
                return new CommentUpdateResult
                {
                    Status = CommentUpdateStatus.NotFound,
                    Message = "The comment was not found."
                };
            }

            if (!IsOwner(comment, currentUserId))
            {
                return new CommentUpdateResult
                {
                    Status = CommentUpdateStatus.NotOwner,
                    Message =
                        "Only the user who created the comment can edit it."
                };
            }

            if (!IsWithinEditWindow(comment))
            {
                return new CommentUpdateResult
                {
                    Status =
                        CommentUpdateStatus.EditWindowExpired,

                    Message =
                        "This comment cannot be edited because it is more than 24 hours old."
                };
            }

            comment.Text = newText.Trim();

            // Keep Timestamp unchanged.
            // The 24-hour edit window must remain based on creation time.
            await _context.SaveAsync(
                comment,
                CreateOperationConfig(),
                cancellationToken
            );

            _logger.LogInformation(
                "Comment {CommentId} for episode {EpisodeId} was edited by user {UserId}.",
                comment.CommentId,
                comment.EpisodeId,
                currentUserId
            );

            return new CommentUpdateResult
            {
                Status = CommentUpdateStatus.Success,
                Comment = comment,
                Message = "Comment updated successfully."
            };
        }

        /// <summary>
        /// Checks whether the current user owns the comment.
        /// </summary>
        public async Task<bool> UserOwnsCommentAsync(
            string episodeId,
            string commentId,
            string currentUserId,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(currentUserId))
            {
                return false;
            }

            CommentsModel? comment =
                await GetCommentAsync(
                    episodeId,
                    commentId,
                    cancellationToken
                );

            return comment != null &&
                   IsOwner(comment, currentUserId);
        }

        /// <summary>
        /// Checks whether the comment is still within the 24-hour edit window.
        /// </summary>
        public async Task<bool> IsCommentEditableAsync(
            string episodeId,
            string commentId,
            CancellationToken cancellationToken = default)
        {
            CommentsModel? comment =
                await GetCommentAsync(
                    episodeId,
                    commentId,
                    cancellationToken
                );

            return comment != null &&
                   IsWithinEditWindow(comment);
        }

        /// <summary>
        /// Creates DynamoDB operation configuration with the configured table name.
        /// </summary>
        private DynamoDBOperationConfig CreateOperationConfig()
        {
            return new DynamoDBOperationConfig
            {
                OverrideTableName = _tableName
            };
        }

        /// <summary>
        /// Checks comment ownership.
        /// </summary>
        private static bool IsOwner(
            CommentsModel comment,
            string currentUserId)
        {
            return string.Equals(
                comment.UserId,
                currentUserId.Trim(),
                StringComparison.Ordinal
            );
        }

        /// <summary>
        /// Checks whether a comment was created less than 24 hours ago.
        /// </summary>
        private static bool IsWithinEditWindow(
            CommentsModel comment)
        {
            DateTime timestampUtc =
                comment.Timestamp.Kind switch
                {
                    DateTimeKind.Utc =>
                        comment.Timestamp,

                    DateTimeKind.Local =>
                        comment.Timestamp.ToUniversalTime(),

                    _ =>
                        DateTime.SpecifyKind(
                            comment.Timestamp,
                            DateTimeKind.Utc
                        )
                };

            TimeSpan commentAge =
                DateTime.UtcNow - timestampUtc;

            return commentAge >= TimeSpan.Zero &&
                   commentAge < EditWindow;
        }

        /// <summary>
        /// Validates values required when creating a comment.
        /// </summary>
        private static void ValidateCreateInput(
            string episodeId,
            string podcastId,
            string userId,
            string text)
        {
            ValidateRequiredId(
                episodeId,
                nameof(episodeId),
                "Episode ID is required."
            );

            ValidateRequiredId(
                podcastId,
                nameof(podcastId),
                "Podcast ID is required."
            );

            ValidateRequiredId(
                userId,
                nameof(userId),
                "User ID is required."
            );

            if (string.IsNullOrWhiteSpace(text))
            {
                throw new ArgumentException(
                    "Comment text is required.",
                    nameof(text)
                );
            }
        }

        /// <summary>
        /// Validates a required string ID.
        /// </summary>
        private static void ValidateRequiredId(
            string value,
            string parameterName,
            string errorMessage)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException(
                    errorMessage,
                    parameterName
                );
            }
        }
    }
}
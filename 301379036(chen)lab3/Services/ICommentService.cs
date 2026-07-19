using _301379036_chen_lab3.Models;

namespace _301379036_chen_lab3.Services
{
    public interface ICommentService
    {
        Task<CommentsModel> AddCommentAsync(
            string episodeId,
            string podcastId,
            string userId,
            string text,
            CancellationToken cancellationToken = default
        );

        Task<IReadOnlyList<CommentsModel>> GetCommentsByEpisodeAsync(
            string episodeId,
            CancellationToken cancellationToken = default
        );

        Task<CommentsModel?> GetCommentAsync(
            string episodeId,
            string commentId,
            CancellationToken cancellationToken = default
        );

        Task<CommentUpdateResult> UpdateCommentAsync(
            string episodeId,
            string commentId,
            string currentUserId,
            string newText,
            CancellationToken cancellationToken = default
        );

        Task<bool> UserOwnsCommentAsync(
            string episodeId,
            string commentId,
            string currentUserId,
            CancellationToken cancellationToken = default
        );

        Task<bool> IsCommentEditableAsync(
            string episodeId,
            string commentId,
            CancellationToken cancellationToken = default
        );
    }
}
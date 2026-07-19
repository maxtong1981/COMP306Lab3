namespace _301379036_chen_lab3.Models
{
    public enum CommentUpdateStatus
    {
        Success,
        NotFound,
        NotOwner,
        EditWindowExpired,
        InvalidText
    }

    public sealed class CommentUpdateResult
    {
        public CommentUpdateStatus Status { get; init; }

        public CommentsModel? Comment { get; init; }

        public string Message { get; init; } = string.Empty;

        public bool IsSuccess =>
            Status == CommentUpdateStatus.Success;
    }
}

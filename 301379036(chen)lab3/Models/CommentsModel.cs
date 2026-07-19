using Amazon.DynamoDBv2.DataModel;
namespace _301379036_chen_lab3.Models
{
    [DynamoDBTable("COMP306_Lab3_PodcastComments")]
    public class CommentsModel
    {
        [DynamoDBHashKey("EpisodeID")]
        public string EpisodeId { get; set; } = string.Empty;

        [DynamoDBRangeKey("CommentID")]
        public string CommentId { get; set; } = string.Empty;

        [DynamoDBProperty("PodcastID")]
        public string PodcastId { get; set; } = string.Empty;

        [DynamoDBProperty("UserID")]
        public string UserId { get; set; } = string.Empty;

        [DynamoDBProperty("Text")]
        public string Text { get; set; } = string.Empty;

        [DynamoDBProperty("Timestamp")]
        public DateTime Timestamp { get; set; }
    }
}

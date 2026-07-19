using Microsoft.AspNetCore.Mvc;
using _301379036_chen_lab3.Models;
using _301379036_chen_lab3.Services;

namespace _301379036_chen_lab3.Controllers
{
    public sealed class CommentsDemoController : Controller
    {
        private readonly ICommentService _commentService;
        private readonly ILogger<CommentsDemoController> _logger;

        public CommentsDemoController(
            ICommentService commentService,
            ILogger<CommentsDemoController> logger)
        {
            _commentService = commentService;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> Index(
            string episodeId = "1",
            string currentUserId = "1",
            CancellationToken cancellationToken = default)
        {
            IReadOnlyList<CommentsModel> comments =
                await _commentService
                    .GetCommentsByEpisodeAsync(
                        episodeId,
                        cancellationToken);

            ViewBag.EpisodeId = episodeId;
            ViewBag.CurrentUserId = currentUserId;

            return View(comments);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Add(
            string episodeId,
            string podcastId,
            string userId,
            string text,
            CancellationToken cancellationToken)
        {
            try
            {
                CommentsModel comment =
                    await _commentService.AddCommentAsync(
                        episodeId,
                        podcastId,
                        userId,
                        text,
                        cancellationToken);

                TempData["SuccessMessage"] =
                    $"Comment {comment.CommentId} was added successfully.";
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Failed to add a comment to episode {EpisodeId}.",
                    episodeId);

                TempData["ErrorMessage"] = ex.Message;
            }

            return RedirectToAction(
                nameof(Index),
                new
                {
                    episodeId,
                    currentUserId = userId
                });
        }

        [HttpGet]
        public async Task<IActionResult> Edit(
            string episodeId,
            string commentId,
            string currentUserId,
            CancellationToken cancellationToken)
        {
            CommentsModel? comment =
                await _commentService.GetCommentAsync(
                    episodeId,
                    commentId,
                    cancellationToken);

            if (comment == null)
            {
                return NotFound();
            }

            bool ownsComment =
                await _commentService.UserOwnsCommentAsync(
                    episodeId,
                    commentId,
                    currentUserId,
                    cancellationToken);

            if (!ownsComment)
            {
                TempData["ErrorMessage"] =
                    "The selected user does not own this comment.";

                return RedirectToAction(
                    nameof(Index),
                    new
                    {
                        episodeId,
                        currentUserId
                    });
            }

            bool editable =
                await _commentService.IsCommentEditableAsync(
                    episodeId,
                    commentId,
                    cancellationToken);

            if (!editable)
            {
                TempData["ErrorMessage"] =
                    "This comment is more than 24 hours old and cannot be edited.";

                return RedirectToAction(
                    nameof(Index),
                    new
                    {
                        episodeId,
                        currentUserId
                    });
            }

            ViewBag.CurrentUserId = currentUserId;

            return View(comment);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(
            string episodeId,
            string commentId,
            string currentUserId,
            string text,
            CancellationToken cancellationToken)
        {
            CommentUpdateResult result =
                await _commentService.UpdateCommentAsync(
                    episodeId,
                    commentId,
                    currentUserId,
                    text,
                    cancellationToken);

            if (result.IsSuccess)
            {
                TempData["SuccessMessage"] =
                    result.Message;
            }
            else
            {
                TempData["ErrorMessage"] =
                    result.Message;
            }

            return RedirectToAction(
                nameof(Index),
                new
                {
                    episodeId,
                    currentUserId
                });
        }
    }
}

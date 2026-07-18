using Microsoft.AspNetCore.Mvc;
using _301379036_chen_lab3.Services;

namespace _301379036_chen_lab3.Controllers
{
    public class S3DemoController : Controller
    {
        private readonly S3Service _s3Service;
        private readonly ILogger<S3DemoController> _logger;

        public S3DemoController(
            S3Service s3Service,
            ILogger<S3DemoController> logger)
        {
            _s3Service = s3Service;
            _logger = logger;
        }

        /// <summary>
        /// Displays the S3 testing page.
        /// </summary>
        [HttpGet]
        public IActionResult Index()
        {
            return View("Index");
        }

        /// <summary>
        /// Uploads a new audio/video file to S3.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Upload(
            IFormFile mediaFile,
            CancellationToken cancellationToken)
        {
            if (mediaFile == null || mediaFile.Length == 0)
            {
                TempData["ErrorMessage"] =
                    "Please select an audio or video file.";

                return RedirectToAction(nameof(Index));
            }

            try
            {
                string fileUrl = await _s3Service.UploadFileAsync(
                    mediaFile,
                    cancellationToken
                );

                TempData["SuccessMessage"] =
                    "File uploaded successfully.";

                TempData["FileUrl"] = fileUrl;

                return RedirectToAction(nameof(Index));
            }
            catch (ArgumentException ex)
            {
                TempData["ErrorMessage"] = ex.Message;
                return RedirectToAction(nameof(Index));
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogError(ex, "S3 upload test failed.");

                TempData["ErrorMessage"] =
                    "Upload failed. Please check the AWS configuration.";

                return RedirectToAction(nameof(Index));
            }
        }

        /// <summary>
        /// Deletes an S3 file by absolute URL or object key.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(
            string keyOrUrl,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(keyOrUrl))
            {
                TempData["ErrorMessage"] =
                    "Please enter an S3 file URL or object key.";

                return RedirectToAction(nameof(Index));
            }

            try
            {
                await _s3Service.DeleteFileAsync(
                    keyOrUrl,
                    cancellationToken
                );

                TempData["SuccessMessage"] =
                    "File deleted successfully.";

                return RedirectToAction(nameof(Index));
            }
            catch (ArgumentException ex)
            {
                TempData["ErrorMessage"] = ex.Message;
                return RedirectToAction(nameof(Index));
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogError(ex, "S3 delete test failed.");

                TempData["ErrorMessage"] =
                    "Delete failed. Please check the URL, object key, and AWS permissions.";

                return RedirectToAction(nameof(Index));
            }
        }

        /// <summary>
        /// Uploads a new file and deletes the old S3 file.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Replace(
            IFormFile newMediaFile,
            string oldKeyOrUrl,
            CancellationToken cancellationToken)
        {
            if (newMediaFile == null || newMediaFile.Length == 0)
            {
                TempData["ErrorMessage"] =
                    "Please select a replacement audio or video file.";

                return RedirectToAction(nameof(Index));
            }

            if (string.IsNullOrWhiteSpace(oldKeyOrUrl))
            {
                TempData["ErrorMessage"] =
                    "Please enter the URL or key of the old S3 file.";

                return RedirectToAction(nameof(Index));
            }

            try
            {
                string newFileUrl =
                    await _s3Service.ReplaceFileAsync(
                        newMediaFile,
                        oldKeyOrUrl,
                        cancellationToken
                    );

                TempData["SuccessMessage"] =
                    "File replaced successfully.";

                TempData["FileUrl"] = newFileUrl;

                return RedirectToAction(nameof(Index));
            }
            catch (ArgumentException ex)
            {
                TempData["ErrorMessage"] = ex.Message;
                return RedirectToAction(nameof(Index));
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogError(ex, "S3 replace test failed.");

                TempData["ErrorMessage"] =
                    "Replace failed. Please check AWS configuration and permissions.";

                return RedirectToAction(nameof(Index));
            }
        }
    }
}
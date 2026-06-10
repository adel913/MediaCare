using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MedicalApp.API.Controllers
{
    /// <summary>
    /// Handles file uploads (license images, profile images, etc.).
    /// Files are stored locally in wwwroot/uploads.
    /// </summary>
    [Authorize]
    public class UploadController : BaseApiController
    {
        private readonly IWebHostEnvironment _env;

        public UploadController(IWebHostEnvironment env)
        {
            _env = env;
        }

        /// <summary>
        /// Upload a license image (for Doctor or Clinic registration).
        /// </summary>
        [HttpPost("license")]
        [AllowAnonymous]
        public async Task<IActionResult> UploadLicense(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return BadRequest(new { isSuccess = false, message = "File is required" });

            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".pdf" };
            var extension = Path.GetExtension(file.FileName).ToLower();

            if (!allowedExtensions.Contains(extension))
                return BadRequest(new { isSuccess = false, message = "Unsupported file type. Supported types: jpg, jpeg, png, pdf" });

            if (file.Length > 5 * 1024 * 1024) // 5MB max
                return BadRequest(new { isSuccess = false, message = "File size must not exceed 5 MB" });

            var uploadsDir = Path.Combine(_env.ContentRootPath, "wwwroot", "uploads", "licenses");
            Directory.CreateDirectory(uploadsDir);

            var fileName = $"{Guid.NewGuid()}{extension}";
            var filePath = Path.Combine(uploadsDir, fileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            var fileUrl = $"/uploads/licenses/{fileName}";

            return Ok(new { isSuccess = true, message = "File uploaded successfully", data = new { url = fileUrl } });
        }

        [HttpPost("profile-image")]
        public async Task<IActionResult> UploadProfileImage(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return BadRequest(new { isSuccess = false, message = "File is required" });

            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png" };
            var extension = Path.GetExtension(file.FileName).ToLower();

            if (!allowedExtensions.Contains(extension))
                return BadRequest(new { isSuccess = false, message = "Unsupported file type. Supported types: jpg, jpeg, png" });

            if (file.Length > 3 * 1024 * 1024) // 3MB max
                return BadRequest(new { isSuccess = false, message = "File size must not exceed 3 MB" });

            var uploadsDir = Path.Combine(_env.ContentRootPath, "wwwroot", "uploads", "profiles");
            Directory.CreateDirectory(uploadsDir);

            var fileName = $"{Guid.NewGuid()}{extension}";
            var filePath = Path.Combine(uploadsDir, fileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            var fileUrl = $"/uploads/profiles/{fileName}";

            return Ok(new { isSuccess = true, message = "Image uploaded successfully", data = new { url = fileUrl } });
        }
    }
}

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MedicalApp.API.DTOs.Review;
using MedicalApp.API.Services.Interfaces;

namespace MedicalApp.API.Controllers
{
    public class ReviewController : BaseApiController
    {
        private readonly IReviewService _reviewService;

        public ReviewController(IReviewService reviewService)
        {
            _reviewService = reviewService;
        }

        [Authorize(Roles = "Patient")]
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateReviewDto dto)
        {
            var result = await _reviewService.CreateReviewAsync(GetUserId(), dto);
            return StatusCode(result.StatusCode, result);
        }

        [HttpGet("doctor/{doctorId}")]
        public async Task<IActionResult> GetDoctorReviews(int doctorId)
        {
            var result = await _reviewService.GetDoctorReviewsAsync(doctorId);
            return StatusCode(result.StatusCode, result);
        }
    }
}

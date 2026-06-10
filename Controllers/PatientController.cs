using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MedicalApp.API.DTOs.Patient;
using MedicalApp.API.Services.Interfaces;

namespace MedicalApp.API.Controllers
{
    [Authorize]
    public class PatientController : BaseApiController
    {
        private readonly IPatientService _patientService;

        public PatientController(IPatientService patientService)
        {
            _patientService = patientService;
        }

        /// <summary>
        /// Get current patient's profile.
        /// </summary>
        [Authorize(Roles = "Patient")]
        [HttpGet("profile")]
        public async Task<IActionResult> GetProfile()
        {
            var result = await _patientService.GetProfileAsync(GetUserId());
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// Update current patient's profile.
        /// </summary>
        [Authorize(Roles = "Patient")]
        [HttpPut("profile")]
        public async Task<IActionResult> UpdateProfile([FromBody] UpdatePatientProfileDto dto)
        {
            var result = await _patientService.UpdateProfileAsync(GetUserId(), dto);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// Toggle favorite doctor status (add to or remove from favorites).
        /// </summary>
        [Authorize(Roles = "Patient")]
        [HttpPost("favorite/{doctorId}")]
        public async Task<IActionResult> ToggleFavorite(int doctorId)
        {
            var result = await _patientService.ToggleFavoriteDoctorAsync(GetUserId(), doctorId);
            return StatusCode(result.StatusCode, result);
        }

        [Authorize(Roles = "Patient")]
        [HttpGet("favorites")]
        public async Task<IActionResult> GetFavorites()
        {
            var result = await _patientService.GetFavoritesAsync(GetUserId());
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// Search existing patients by name, ID or phone number (available to ClinicAdmin and Doctor).
        /// </summary>
        [Authorize(Roles = "ClinicAdmin,Doctor")]
        [HttpGet("search")]
        public async Task<IActionResult> Search([FromQuery] string query)
        {
            var result = await _patientService.SearchPatientsAsync(query);
            return StatusCode(result.StatusCode, result);
        }

        // ===== Family Members =====

        [Authorize(Roles = "Patient")]
        [HttpGet("family-members")]
        public async Task<IActionResult> GetFamilyMembers()
        {
            var result = await _patientService.GetFamilyMembersAsync(GetUserId());
            return StatusCode(result.StatusCode, result);
        }

        [Authorize(Roles = "Patient")]
        [HttpPost("family-members")]
        public async Task<IActionResult> AddFamilyMember([FromBody] CreateFamilyMemberDto dto)
        {
            var result = await _patientService.AddFamilyMemberAsync(GetUserId(), dto);
            return StatusCode(result.StatusCode, result);
        }

        [Authorize(Roles = "Patient")]
        [HttpDelete("family-members/{memberId}")]
        public async Task<IActionResult> RemoveFamilyMember(int memberId)
        {
            var result = await _patientService.RemoveFamilyMemberAsync(GetUserId(), memberId);
            return StatusCode(result.StatusCode, result);
        }
    }
}

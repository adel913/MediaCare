using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MedicalApp.API.DTOs.Doctor;
using MedicalApp.API.DTOs.Schedule;
using MedicalApp.API.DTOs.MedicalRecord;
using MedicalApp.API.Services.Interfaces;

namespace MedicalApp.API.Controllers
{
    public class DoctorController : BaseApiController
    {
        private readonly IDoctorService _doctorService;

        public DoctorController(IDoctorService doctorService)
        {
            _doctorService = doctorService;
        }

        /// <summary>
        /// Get all doctors (public - for patients to browse with filters).
        /// </summary>
        /// <summary>
        /// Get all doctors (public - for patients to browse with filters).
        /// </summary>
        [HttpGet("nearby", Order = 0)]
        public async Task<IActionResult> GetNearby(
            [FromQuery] double lat,
            [FromQuery] double lng,
            [FromQuery] double radiusKm = 5,
            [FromQuery] string? specialization = null,
            [FromQuery] string? search = null)
        {
            var result = await _doctorService.GetNearbyDoctorsAsync(lat, lng, radiusKm, specialization, search);
            return StatusCode(result.StatusCode, result);
        }

        [HttpGet]
        public async Task<IActionResult> GetAll(
            [FromQuery] string? specialization,
            [FromQuery] string? search,
            [FromQuery] string? government,
            [FromQuery] string? area,
            [FromQuery] string? appointmentDay,
            [FromQuery] MedicalApp.API.Models.Enums.Gender? gender,
            [FromQuery] decimal? minFee,
            [FromQuery] decimal? maxFee,
            [FromQuery] double? minRating,
            [FromQuery] double? userLat = null,
            [FromQuery] double? userLng = null)
        {
            var result = await _doctorService.GetAllDoctorsAsync(
                specialization,
                search,
                government,
                area,
                appointmentDay,
                gender,
                minFee,
                maxFee,
                minRating,
                GetOptionalUserId(),
                userLat,
                userLng);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// Get all unique doctor specializations (public - for grid).
        /// </summary>
        [HttpGet("specializations")]
        public async Task<IActionResult> GetSpecializations()
        {
            var result = await _doctorService.GetSpecializationsAsync();
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// Get popular doctors ordered by actual rating (public - for home screen).
        /// </summary>
        [HttpGet("popular")]
        public async Task<IActionResult> GetPopular(
            [FromQuery] double? userLat = null,
            [FromQuery] double? userLng = null)
        {
            var result = await _doctorService.GetPopularDoctorsAsync(GetOptionalUserId(), userLat, userLng);
            return StatusCode(result.StatusCode, result);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var result = await _doctorService.GetDoctorByIdAsync(id);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// Get current doctor's profile (Doctor only).
        /// </summary>
        [Authorize(Roles = "Doctor")]
        [HttpGet("profile")]
        public async Task<IActionResult> GetProfile()
        {
            var result = await _doctorService.GetProfileAsync(GetUserId());
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// Update current doctor's profile (Doctor only).
        /// </summary>
        [Authorize(Roles = "Doctor")]
        [HttpPut("profile")]
        public async Task<IActionResult> UpdateProfile([FromBody] UpdateDoctorProfileDto dto)
        {
            var result = await _doctorService.UpdateProfileAsync(GetUserId(), dto);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// Get doctor's schedules (public).
        /// </summary>
        [HttpGet("{id}/schedules")]
        public async Task<IActionResult> GetSchedules(int id)
        {
            var result = await _doctorService.GetSchedulesAsync(id);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// Add a new schedule for a doctor (ClinicAdmin only).
        /// </summary>
        [Authorize(Roles = "ClinicAdmin")]
        [HttpPost("{doctorId}/schedules")]
        public async Task<IActionResult> AddSchedule(int doctorId, [FromBody] CreateScheduleDto dto)
        {
            var result = await _doctorService.AddScheduleAsync(GetUserId(), doctorId, dto);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// Delete a schedule (ClinicAdmin only).
        /// </summary>
        [Authorize(Roles = "ClinicAdmin")]
        [HttpDelete("schedules/{scheduleId}")]
        public async Task<IActionResult> DeleteSchedule(int scheduleId)
        {
            var result = await _doctorService.DeleteScheduleAsync(GetUserId(), scheduleId);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// Get available time slots for a doctor on a specific date (public).
        /// </summary>
        [HttpGet("{id}/available-slots")]
        public async Task<IActionResult> GetAvailableSlots(int id, [FromQuery] DateTime date)
        {
            var result = await _doctorService.GetAvailableSlotsAsync(id, date);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// Get today's stats dashboard (Doctor only).
        /// </summary>
        [Authorize(Roles = "Doctor")]
        [HttpGet("dashboard")]
        public async Task<IActionResult> GetDashboard()
        {
            var result = await _doctorService.GetDoctorDashboardAsync(GetUserId());
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// Get today's live queue list (Doctor only).
        /// </summary>
        [Authorize(Roles = "Doctor")]
        [HttpGet("live-queue")]
        public async Task<IActionResult> GetLiveQueue([FromQuery] string? status)
        {
            var result = await _doctorService.GetDoctorLiveQueueAsync(GetUserId(), status);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// Get unique QR Code content key for this doctor (Doctor only).
        /// </summary>
        [Authorize(Roles = "Doctor")]
        [HttpGet("qr-code")]
        public async Task<IActionResult> GetQrCode()
        {
            var result = await _doctorService.GetDoctorQrCodeAsync(GetUserId());
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// Get patient history for checkup (Doctor only).
        /// </summary>
        [Authorize(Roles = "Doctor")]
        [HttpGet("patients/{patientId}/history")]
        public async Task<IActionResult> GetPatientHistory(int patientId)
        {
            var result = await _doctorService.GetPatientHistoryAsync(GetUserId(), patientId);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// Get consultation screen with patient info and full medical history (Doctor only).
        /// </summary>
        [Authorize(Roles = "Doctor")]
        [HttpGet("consultation/{appointmentId}")]
        public async Task<IActionResult> GetConsultationScreen(int appointmentId)
        {
            var result = await _doctorService.GetConsultationScreenAsync(GetUserId(), appointmentId);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// Get doctor's active consultations for today (Doctor only).
        /// </summary>
        [Authorize(Roles = "Doctor")]
        [HttpGet("active-consultations")]
        public async Task<IActionResult> GetActiveConsultations()
        {
            var result = await _doctorService.GetActiveConsultationsAsync(GetUserId());
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// Complete consultation with diagnosis and optional medications (Doctor only).
        /// </summary>
        [Authorize(Roles = "Doctor")]
        [HttpPost("consultation/{appointmentId}/complete")]
        public async Task<IActionResult> CompleteConsultation(int appointmentId, [FromBody] CompleteConsultationDto dto)
        {
            var result = await _doctorService.CompleteConsultationAsync(GetUserId(), appointmentId, dto);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// Submit checkup vitals, SOAP summary, and prescription details to complete consultation (Doctor only).
        /// </summary>
        [Authorize(Roles = "Doctor")]
        [HttpPost("session/{appointmentId}")]
        public async Task<IActionResult> SubmitSession(int appointmentId, [FromBody] CreateMedicalRecordDto dto)
        {
            var result = await _doctorService.SubmitConsultationSessionAsync(GetUserId(), appointmentId, dto);
            return StatusCode(result.StatusCode, result);
        }
    }
}

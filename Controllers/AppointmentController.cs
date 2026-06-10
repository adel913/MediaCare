using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MedicalApp.API.DTOs.Appointment;
using MedicalApp.API.Models.Enums;
using MedicalApp.API.Services.Interfaces;

namespace MedicalApp.API.Controllers
{
    [Authorize]
    public class AppointmentController : BaseApiController
    {
        private readonly IAppointmentService _appointmentService;

        public AppointmentController(IAppointmentService appointmentService)
        {
            _appointmentService = appointmentService;
        }

        /// <summary>
        /// Book an appointment (Patient app only).
        /// </summary>
        [HttpPost]
        [Authorize(Roles = "Patient")]
        public async Task<IActionResult> CreateAppointment([FromBody] CreateAppointmentDto dto)
        {
            var result = await _appointmentService.CreateAppointmentAsync(GetUserId(), dto);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// Book an appointment from the Clinic or Doctor side (Offline / Walk-in patient).
        /// </summary>
        [HttpPost("clinic-booking")]
        [Authorize(Roles = "ClinicAdmin,Doctor")]
        public async Task<IActionResult> CreateClinicAppointment([FromBody] ClinicCreateAppointmentDto dto)
        {
            var result = await _appointmentService.CreateClinicAppointmentAsync(GetUserId(), dto);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// Get patient's appointments (Patient only).
        /// Supports filter ("upcoming", "completed", "cancelled") or direct status.
        /// </summary>
        [Authorize(Roles = "Patient")]
        [HttpGet("patient")]
        public async Task<IActionResult> GetPatientAppointments([FromQuery] string? filter, [FromQuery] AppointmentStatus? status)
        {
            var result = await _appointmentService.GetPatientAppointmentsAsync(GetUserId(), filter, status);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// Get doctor's appointments (Doctor only).
        /// </summary>
        [Authorize(Roles = "Doctor")]
        [HttpGet("doctor")]
        public async Task<IActionResult> GetDoctorAppointments([FromQuery] DateTime? date, [FromQuery] AppointmentStatus? status)
        {
            var result = await _appointmentService.GetDoctorAppointmentsAsync(GetUserId(), date, status);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// Get today's queue for the doctor (Doctor only).
        /// </summary>
        [Authorize(Roles = "Doctor")]
        [HttpGet("queue/today")]
        public async Task<IActionResult> GetTodayQueue()
        {
            var result = await _appointmentService.GetTodayQueueAsync(GetUserId());
            return StatusCode(result.StatusCode, result);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var result = await _appointmentService.GetAppointmentByIdAsync(id, GetUserId());
            return StatusCode(result.StatusCode, result);
        }

        [HttpPut("{id}/cancel")]
        public async Task<IActionResult> Cancel(int id, [FromBody] CancelAppointmentDto dto)
        {
            var result = await _appointmentService.CancelAppointmentAsync(id, GetUserId(), dto);
            return StatusCode(result.StatusCode, result);
        }

        [HttpPut("{id}/reschedule")]
        public async Task<IActionResult> Reschedule(int id, [FromBody] RescheduleAppointmentDto dto)
        {
            var result = await _appointmentService.RescheduleAppointmentAsync(id, GetUserId(), dto);
            return StatusCode(result.StatusCode, result);
        }

        [HttpPut("{id}/status")]
        [Authorize(Roles = "Doctor")]
        public async Task<IActionResult> UpdateStatus(int id, [FromBody] UpdateAppointmentStatusDto dto)
        {
            var result = await _appointmentService.UpdateStatusAsync(id, GetUserId(), dto);
            return StatusCode(result.StatusCode, result);
        }

        // ==========================================
        //         LIVE QUEUE ENDPOINTS
        // ==========================================

        /// <summary>
        /// For Patient: Track live queue status, patients ahead, and estimated wait time.
        /// </summary>
        [HttpGet("queue/tracker/{id}")]
        [Authorize(Roles = "Patient")]
        public async Task<IActionResult> GetLiveQueueTracker(int id)
        {
            var result = await _appointmentService.GetLiveQueueTrackerAsync(id, GetUserId());
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// For Doctor: Call the next patient in the queue (finishes current, starts next).
        /// </summary>
        [HttpPost("queue/call-next")]
        [Authorize(Roles = "Doctor")]
        public async Task<IActionResult> CallNextPatient()
        {
            var result = await _appointmentService.CallNextPatientInQueueAsync(GetUserId());
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// Get clinic today's overview dashboard stats (Clinic Admin only).
        /// </summary>
        [Authorize(Roles = "ClinicAdmin")]
        [HttpGet("clinic/dashboard")]
        public async Task<IActionResult> GetClinicDashboardOverview([FromQuery] int? doctorId)
        {
            var result = await _appointmentService.GetClinicDashboardOverviewAsync(GetUserId(), doctorId);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// Get clinic today's live queue for a specific doctor (Clinic Admin only).
        /// </summary>
        [Authorize(Roles = "ClinicAdmin")]
        [HttpGet("clinic/queue")]
        public async Task<IActionResult> GetClinicTodayQueue([FromQuery] int doctorId)
        {
            var result = await _appointmentService.GetClinicTodayQueueAsync(GetUserId(), doctorId);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// Start checkup for a patient (Clinic Admin only).
        /// </summary>
        [Authorize(Roles = "ClinicAdmin")]
        [HttpPost("{id}/start-checkup")]
        public async Task<IActionResult> StartCheckup(int id)
        {
            var result = await _appointmentService.StartCheckupAsync(GetUserId(), id);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// Get clinic payments dashboard overview and recent transactions (Clinic Admin only).
        /// </summary>
        [Authorize(Roles = "ClinicAdmin")]
        [HttpGet("clinic/payments-dashboard")]
        public async Task<IActionResult> GetClinicPaymentsDashboard([FromQuery] int? doctorId, [FromQuery] string timeframe = "today")
        {
            var result = await _appointmentService.GetPaymentsDashboardAsync(GetUserId(), doctorId, timeframe);
            return StatusCode(result.StatusCode, result);
        }
    }
}

using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MediaCare.DTOs;
using MediaCare.Models;
using MediaCare.Services;

namespace MediaCare.Controllers;

// ─── Base Controllers ─────────────────────────────────────────────────────────

[ApiController]
public abstract class BaseController : ControllerBase
{
    protected int CurrentUserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    protected UserRole CurrentUserRole => Enum.Parse<UserRole>(User.FindFirstValue(ClaimTypes.Role)!);
}

[ApiController]
public abstract class ClinicBaseController : ControllerBase
{
    protected int CurrentClinicUserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    protected int CurrentClinicId => int.Parse(User.FindFirstValue("ClinicId")!);
    protected ClinicRole CurrentClinicRole => Enum.Parse<ClinicRole>(User.FindFirstValue(ClaimTypes.Role)!);
}

// ─── Auth Controller ──────────────────────────────────────────────────────────

/// <summary>
/// POST /auth/register       — Register new user (Patient / Doctor)
/// POST /auth/login          — Login
/// GET  /auth/profile        — Get current user profile
/// </summary>
[Route("auth")]
public class AuthController : BaseController
{
    private readonly IAuthService _auth;
    public AuthController(IAuthService auth) => _auth = auth;

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest req)
    {
        var result = await _auth.RegisterAsync(req);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest req)
    {
        var result = await _auth.LoginAsync(req);
        return result.Success ? Ok(result) : Unauthorized(result);
    }

    [Authorize]
    [HttpGet("profile")]
    public async Task<IActionResult> Profile()
    {
        var result = await _auth.GetProfileAsync(CurrentUserId);
        return result.Success ? Ok(result) : NotFound(result);
    }
}

// ─── Doctors Search & Popular ─────────────────────────────────────────────────

/// <summary>
/// GET /api/doctors/popular  — Search + filter + sort doctors (public)
/// GET /api/doctors          — Get all available doctors
/// GET /api/doctors/{id}     — Get doctor by id
/// </summary>
[Route("api/doctors")]
public class DoctorsController : BaseController
{
    private readonly IDoctorService _doctors;
    public DoctorsController(IDoctorService doctors) => _doctors = doctors;

    /// <summary>
    /// GET /api/doctors/popular
    /// Query params: governorate, area, specialization, maxFee, gender, availableDate (yyyy-MM-dd)
    /// Sorted descending by rating. Returns doctors with available slots on the given date.
    /// </summary>
    [HttpGet("popular")]
    public async Task<IActionResult> GetPopular([FromQuery] PopularDoctorFilterRequest filter)
    {
        var result = await _doctors.GetPopularAsync(filter);
        return Ok(result);
    }

    [HttpGet]
    public async Task<IActionResult> GetAll() => Ok(await _doctors.GetAllAsync());

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var result = await _doctors.GetByIdAsync(id);
        return result.Success ? Ok(result) : NotFound(result);
    }
}

// ─── Doctor (self) Controller ─────────────────────────────────────────────────

/// <summary>
/// GET    /api/doctor/qr
/// GET    /api/doctor/availability
/// POST   /api/doctor/availability
/// PUT    /api/doctor/availability/{id}
/// DELETE /api/doctor/availability/{id}
/// PUT    /api/doctor/profile
/// GET    /api/doctor/appointments
/// PATCH  /api/doctor/appointments/{id}/status
/// GET    /api/doctor/queue?date=today
/// GET    /api/doctor/dashboard/overview
/// GET    /api/doctor/dashboard/earnings?period=daily|weekly|monthly
/// GET    /api/doctor/dashboard/cases
/// </summary>
[Route("api/doctor")]
[Authorize(Roles = "Doctor")]
public class DoctorController : BaseController
{
    private readonly IDoctorService _doctors;
    private readonly IDoctorAvailabilityService _availability;
    private readonly IAppointmentService _appointments;
    private readonly IDashboardService _dashboard;
    private readonly IQueueService _queue;

    public DoctorController(
        IDoctorService doctors,
        IDoctorAvailabilityService availability,
        IAppointmentService appointments,
        IDashboardService dashboard,
        IQueueService queue)
    {
        _doctors = doctors;
        _availability = availability;
        _appointments = appointments;
        _dashboard = dashboard;
        _queue = queue;
    }

    [HttpGet("qr")]
    public async Task<IActionResult> GetQr()
    {
        var result = await _doctors.GetQrAsync(CurrentUserId);
        return result.Success ? Ok(result) : NotFound(result);
    }

    [HttpPut("profile")]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateDoctorRequest req)
    {
        var result = await _doctors.UpdateDoctorAsync(CurrentUserId, req);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    // ── Availability ──────────────────────────────────────────────────────────

    [HttpPost("availability")]
    public async Task<IActionResult> CreateAvailability([FromBody] DoctorAvailabilityRequest req)
    {
        var result = await _availability.CreateAsync(CurrentUserId, req);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpGet("availability")]
    public async Task<IActionResult> GetAvailability()
    {
        var result = await _availability.GetAsync(CurrentUserId);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPut("availability/{id:int}")]
    public async Task<IActionResult> UpdateAvailability(int id, [FromBody] DoctorAvailabilityRequest req)
    {
        var result = await _availability.UpdateAsync(CurrentUserId, id, req);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpDelete("availability/{id:int}")]
    public async Task<IActionResult> DeleteAvailability(int id)
    {
        var result = await _availability.DeleteAsync(CurrentUserId, id);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    // ── Appointments ──────────────────────────────────────────────────────────

    [HttpGet("appointments")]
    public async Task<IActionResult> GetAppointments([FromQuery] DateTime? date)
    {
        var result = await _appointments.GetForUserAsync(CurrentUserId, UserRole.Doctor);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>
    /// PATCH /api/doctor/appointments/{id}/status
    /// Body: { "status": "Completed" }
    /// Triggers PatientsCount++ when Completed.
    /// </summary>
    [HttpPatch("appointments/{id:int}/status")]
    public async Task<IActionResult> UpdateAppointmentStatus(int id, [FromBody] UpdateAppointmentStatusRequest req)
    {
        var result = await _appointments.UpdateStatusAsync(id, CurrentUserId, req.Status);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    // ── Queue ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// GET /api/doctor/{id}/queue?date=today
    /// Returns Waiting / InProgress / Completed groups.
    /// </summary>
    [HttpGet("{doctorId:int}/queue")]
    public async Task<IActionResult> GetQueue(int doctorId, [FromQuery] DateTime? date)
    {
        var result = await _queue.GetQueueAsync(doctorId, date);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    // ── Dashboard ─────────────────────────────────────────────────────────────

    [HttpGet("dashboard/overview")]
    public async Task<IActionResult> DashboardOverview()
    {
        var result = await _dashboard.GetDoctorOverviewAsync(CurrentUserId);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>
    /// GET /api/doctor/dashboard/earnings?period=daily|weekly|monthly
    /// Only counts Completed appointments.
    /// </summary>
    [HttpGet("dashboard/earnings")]
    public async Task<IActionResult> DashboardEarnings([FromQuery] string period = "daily")
    {
        var result = await _dashboard.GetDoctorEarningsAsync(CurrentUserId, period);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpGet("dashboard/cases")]
    public async Task<IActionResult> DashboardCases([FromQuery] DateTime? date)
    {
        var result = await _dashboard.GetDoctorCasesAsync(CurrentUserId, date);
        return result.Success ? Ok(result) : BadRequest(result);
    }
}

// ─── Appointments Controller ──────────────────────────────────────────────────

/// <summary>
/// POST   /api/appointments          — Book appointment (User / FamilyMember / WalkIn)
/// GET    /api/appointments          — Get my appointments
/// DELETE /api/appointments/{id}     — Cancel appointment
/// GET    /api/appointments/{id}/tracking
/// </summary>
[Route("api/appointments")]
[Authorize]
public class AppointmentsController : BaseController
{
    private readonly IAppointmentService _appointments;
    public AppointmentsController(IAppointmentService appointments) => _appointments = appointments;

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateAppointmentRequest req)
    {
        var result = await _appointments.CreateAsync(CurrentUserId, req);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
        => Ok(await _appointments.GetForUserAsync(CurrentUserId, CurrentUserRole));

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Cancel(int id)
    {
        var result = await _appointments.CancelAsync(id, CurrentUserId, CurrentUserRole);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpGet("{id:int}/tracking")]
    public async Task<IActionResult> Tracking(int id)
    {
        var result = await _appointments.GetTrackingAsync(id, CurrentUserId);
        return result.Success ? Ok(result) : BadRequest(result);
    }
}

// ─── Rating Controller ────────────────────────────────────────────────────────

/// <summary>
/// POST /api/ratings            — Submit rating (only if appointment is Completed)
/// GET  /api/ratings/doctor/{id}
/// </summary>
[Route("api/ratings")]
[Authorize]
public class RatingController : BaseController
{
    private readonly IRatingService _ratings;
    public RatingController(IRatingService ratings) => _ratings = ratings;

    [HttpPost]
    [Authorize(Roles = "Patient")]
    public async Task<IActionResult> Create([FromBody] CreateRatingRequest req)
    {
        var result = await _ratings.CreateAsync(CurrentUserId, req);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpGet("doctor/{doctorId:int}")]
    public async Task<IActionResult> GetByDoctor(int doctorId)
        => Ok(await _ratings.GetByDoctorAsync(doctorId));
}

// ─── Favorites Controller ─────────────────────────────────────────────────────

/// <summary>
/// POST /api/doctors/{id}/favorite  — Toggle favorite (add or remove)
/// GET  /api/favorites              — Get my favorite doctors
/// </summary>
[Route("api")]
[Authorize]
public class FavoritesController : BaseController
{
    private readonly IFavoriteService _favorites;
    public FavoritesController(IFavoriteService favorites) => _favorites = favorites;

    /// <summary>
    /// POST /api/doctors/{id}/favorite
    /// Toggles: adds if not in list, removes if already favorited.
    /// Stored in DB (not local storage).
    /// </summary>
    [HttpPost("doctors/{id:int}/favorite")]
    public async Task<IActionResult> Toggle(int id)
    {
        var result = await _favorites.ToggleFavoriteAsync(CurrentUserId, id);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpGet("favorites")]
    public async Task<IActionResult> GetFavorites()
        => Ok(await _favorites.GetFavoritesAsync(CurrentUserId));
}

// ─── Family Member Controller ─────────────────────────────────────────────────

/// <summary>
/// POST   /api/family-members
/// GET    /api/family-members
/// DELETE /api/family-members/{id}
/// </summary>
[Route("api/family-members")]
[Authorize(Roles = "Patient")]
public class FamilyMemberController : BaseController
{
    private readonly IFamilyMemberService _family;
    public FamilyMemberController(IFamilyMemberService family) => _family = family;

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateFamilyMemberRequest req)
    {
        var result = await _family.CreateAsync(CurrentUserId, req);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
        => Ok(await _family.GetAsync(CurrentUserId));

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var result = await _family.DeleteAsync(CurrentUserId, id);
        return result.Success ? Ok(result) : BadRequest(result);
    }
}

// ─── Medical History Controller ───────────────────────────────────────────────

/// <summary>
/// POST /api/medical-history           — Create (Doctor only)
/// GET  /api/medical-history/{patientId}
/// GET  /api/patients/{id}/history     — Doctor views patient full history
/// POST /api/prescriptions             — Add prescription (Doctor only)
/// PUT  /api/prescriptions/{id}        — Update prescription (Doctor only)
/// </summary>
[Route("api/medical-history")]
[Authorize]
public class MedicalHistoryController : BaseController
{
    private readonly IMedicalHistoryService _history;
    public MedicalHistoryController(IMedicalHistoryService history) => _history = history;

    [HttpPost]
    [Authorize(Roles = "Doctor")]
    public async Task<IActionResult> Create([FromBody] CreateMedicalHistoryRequest req)
    {
        var result = await _history.CreateAsync(CurrentUserId, req);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpGet("{patientId:int}")]
    public async Task<IActionResult> GetByPatient(int patientId)
    {
        var result = await _history.GetByPatientAsync(patientId, CurrentUserId, CurrentUserRole);
        return result.Success ? Ok(result) : BadRequest(result);
    }
}

[Route("api/patients")]
[Authorize(Roles = "Doctor")]
public class PatientHistoryController : BaseController
{
    private readonly IMedicalHistoryService _history;
    public PatientHistoryController(IMedicalHistoryService history) => _history = history;

    [HttpGet("{id:int}/history")]
    public async Task<IActionResult> GetHistory(int id)
    {
        var result = await _history.GetPatientHistoryAsync(id, CurrentUserId);
        return result.Success ? Ok(result) : BadRequest(result);
    }
}

[Route("api/prescriptions")]
[Authorize(Roles = "Doctor")]
public class PrescriptionController : BaseController
{
    private readonly IMedicalHistoryService _history;
    public PrescriptionController(IMedicalHistoryService history) => _history = history;

    [HttpPost]
    public async Task<IActionResult> Add([FromBody] CreatePrescriptionRequest req)
    {
        var result = await _history.AddPrescriptionAsync(CurrentUserId, req);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdatePrescriptionRequest req)
    {
        var result = await _history.UpdatePrescriptionAsync(CurrentUserId, id, req);
        return result.Success ? Ok(result) : BadRequest(result);
    }
}

// ─── Consultation Controller ──────────────────────────────────────────────────

/// <summary>
/// POST /api/consultations
/// GET  /api/consultations
/// GET  /api/consultations/{id}
/// POST /api/consultations/{id}/messages
/// </summary>
[Route("api/consultations")]
[Authorize]
public class ConsultationsController : BaseController
{
    private readonly IConsultationService _consultations;
    public ConsultationsController(IConsultationService consultations) => _consultations = consultations;

    [HttpPost]
    [Authorize(Roles = "Patient")]
    public async Task<IActionResult> Create([FromBody] CreateConsultationRequest req)
    {
        var result = await _consultations.CreateAsync(CurrentUserId, req);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
        => Ok(await _consultations.GetForUserAsync(CurrentUserId, CurrentUserRole));

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var result = await _consultations.GetByIdAsync(id, CurrentUserId);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("{id:int}/messages")]
    public async Task<IActionResult> SendMessage(int id, [FromBody] SendMessageRequest req)
    {
        var result = await _consultations.SendMessageAsync(id, CurrentUserId, req);
        return result.Success ? Ok(result) : BadRequest(result);
    }
}

// ─── AI Chatbot Controller ────────────────────────────────────────────────────

/// <summary>
/// POST /api/ai/chat         — Send message, get AI reply
/// GET  /api/ai/chat/history — Get my chat history
/// </summary>
[Route("api/ai")]
[Authorize]
public class AiChatController : BaseController
{
    private readonly IAiChatService _ai;
    public AiChatController(IAiChatService ai) => _ai = ai;

    [HttpPost("chat")]
    public async Task<IActionResult> Chat([FromBody] AiChatRequest req)
    {
        var result = await _ai.SendAsync(CurrentUserId, req);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpGet("chat/history")]
    public async Task<IActionResult> History()
        => Ok(await _ai.GetHistoryAsync(CurrentUserId));
}

// ─── Blog / Posts Controller ──────────────────────────────────────────────────

/// <summary>
/// POST   /api/posts            — Create post (Doctor only)
/// PUT    /api/posts/{id}       — Update post (Doctor only)
/// DELETE /api/posts/{id}       — Delete post (Doctor only)
/// GET    /api/posts            — Get all posts (newest first), optional ?category=
/// GET    /api/posts/{id}       — Get post by id
/// NOTE: Comments feature has been removed entirely.
/// </summary>
[Route("api/posts")]
public class PostController : BaseController
{
    private readonly IPostService _posts;
    public PostController(IPostService posts) => _posts = posts;

    [HttpPost]
    [Authorize(Roles = "Doctor")]
    public async Task<IActionResult> Create([FromBody] CreatePostRequest req)
    {
        var result = await _posts.CreateAsync(CurrentUserId, req);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = "Doctor")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdatePostRequest req)
    {
        var result = await _posts.UpdateAsync(CurrentUserId, id, req);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Doctor")]
    public async Task<IActionResult> Delete(int id)
    {
        var result = await _posts.DeleteAsync(CurrentUserId, id);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] PostCategory? category)
        => Ok(await _posts.GetAllAsync(category));

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var result = await _posts.GetByIdAsync(id);
        return result.Success ? Ok(result) : NotFound(result);
    }
}

// ─── User Notifications Controller ───────────────────────────────────────────

/// <summary>
/// GET   /api/notifications          — Get my notifications (with AppointmentId for deep-link)
/// PATCH /api/notifications/{id}/read
/// </summary>
[Route("api/notifications")]
[Authorize]
public class UserNotificationsController : BaseController
{
    private readonly IUserNotificationService _notifications;
    public UserNotificationsController(IUserNotificationService notifications) => _notifications = notifications;

    [HttpGet]
    public async Task<IActionResult> GetAll()
        => Ok(await _notifications.GetAsync(CurrentUserId));

    [HttpPatch("{id:int}/read")]
    public async Task<IActionResult> MarkRead(int id)
    {
        var result = await _notifications.MarkReadAsync(CurrentUserId, id);
        return result.Success ? Ok(result) : BadRequest(result);
    }
}

// ─── Clinic Auth Controller ───────────────────────────────────────────────────

[Route("api/clinic-auth")]
public class ClinicAuthController : ControllerBase
{
    private readonly IClinicAuthService _auth;
    public ClinicAuthController(IClinicAuthService auth) => _auth = auth;

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] ClinicLoginRequest req)
    {
        var result = await _auth.LoginAsync(req);
        return result.Success ? Ok(result) : Unauthorized(result);
    }
}

// ─── Clinic Admin Controller ──────────────────────────────────────────────────

/// <summary>
/// Clinic Admin (Role = Clinic) has full control over linked doctors:
/// PUT  /api/clinic/doctors/{doctorId}             — Update doctor profile
/// PUT  /api/clinic/doctors/{doctorId}/fee         — Update consultation fee
/// PUT  /api/clinic/doctors/{doctorId}/schedule/{availId} — Update doctor schedule
/// GET  /api/clinic/profile
/// PUT  /api/clinic/profile
/// GET  /api/clinic/hours
/// PUT  /api/clinic/hours
/// GET  /api/clinic/doctors        — List linked doctors
/// POST /api/clinic/doctors/link   — Link doctor via QR
/// POST /api/clinic/availability/{id}/approve
/// </summary>
[Route("api/clinic")]
[Authorize(Roles = "Clinic,Nurse")]
public class ClinicController : ClinicBaseController
{
    private readonly IClinicService _clinic;
    private readonly IDoctorClinicService _doctorClinic;
    private readonly IDoctorService _doctors;
    private readonly IDoctorAvailabilityService _availability;

    public ClinicController(
        IClinicService clinic,
        IDoctorClinicService doctorClinic,
        IDoctorService doctors,
        IDoctorAvailabilityService availability)
    {
        _clinic = clinic;
        _doctorClinic = doctorClinic;
        _doctors = doctors;
        _availability = availability;
    }

    // ── Clinic Profile ────────────────────────────────────────────────────────

    [HttpGet("profile")]
    public async Task<IActionResult> GetProfile()
        => Ok(await _clinic.GetProfileAsync(CurrentClinicId));

    [HttpPut("profile")]
    [Authorize(Roles = "Clinic")]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateClinicProfileRequest req)
    {
        var result = await _clinic.UpdateProfileAsync(CurrentClinicId, req);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpGet("hours")]
    public async Task<IActionResult> GetHours()
        => Ok(await _clinic.GetHoursAsync(CurrentClinicId));

    [HttpPut("hours")]
    [Authorize(Roles = "Clinic")]
    public async Task<IActionResult> UpdateHours([FromBody] UpdateClinicHoursRequest req)
    {
        var result = await _clinic.UpdateHoursAsync(CurrentClinicId, req);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    // ── Doctors Management (Clinic Admin only) ────────────────────────────────

    [HttpGet("doctors")]
    public async Task<IActionResult> GetLinkedDoctors()
        => Ok(await _doctorClinic.GetLinkedDoctorsAsync(CurrentClinicId));

    [HttpPost("doctors/link")]
    [Authorize(Roles = "Clinic")]
    public async Task<IActionResult> LinkDoctor([FromBody] LinkDoctorRequest req)
    {
        var result = await _doctorClinic.LinkDoctorAsync(CurrentClinicId, req);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>Clinic Admin updates doctor profile data.</summary>
    [HttpPut("doctors/{doctorId:int}")]
    [Authorize(Roles = "Clinic")]
    public async Task<IActionResult> UpdateDoctor(int doctorId, [FromBody] UpdateDoctorRequest req)
    {
        var result = await _doctors.AdminUpdateDoctorAsync(CurrentClinicId, doctorId, req);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>Clinic Admin updates doctor consultation fee.</summary>
    [HttpPut("doctors/{doctorId:int}/fee")]
    [Authorize(Roles = "Clinic")]
    public async Task<IActionResult> UpdateDoctorFee(int doctorId, [FromBody] UpdateFeeRequest req)
    {
        var result = await _doctors.AdminUpdateDoctorFeeAsync(CurrentClinicId, doctorId, req.Fee);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>Clinic Admin updates doctor schedule (availability).</summary>
    [HttpPut("doctors/{doctorId:int}/schedule/{availabilityId:int}")]
    [Authorize(Roles = "Clinic")]
    public async Task<IActionResult> UpdateDoctorSchedule(int doctorId, int availabilityId, [FromBody] DoctorAvailabilityRequest req)
    {
        var result = await _availability.AdminUpdateAsync(CurrentClinicId, availabilityId, req);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("availability/{id:int}/approve")]
    [Authorize(Roles = "Clinic")]
    public async Task<IActionResult> ApproveAvailability(int id)
    {
        var result = await _availability.ApproveAsync(CurrentClinicId, id);
        return result.Success ? Ok(result) : BadRequest(result);
    }
}

// Helper DTO for fee update
public class UpdateFeeRequest
{
    public decimal Fee { get; set; }
}

// ─── Provider Controller ──────────────────────────────────────────────────────

/// <summary>
/// POST   /api/providers
/// GET    /api/providers
/// PUT    /api/providers/{id}
/// DELETE /api/providers/{id}
/// POST   /api/providers/{id}/schedule
/// GET    /api/providers/{id}/schedule
/// GET    /api/providers/{id}/slots
/// POST   /api/providers/{id}/slots/generate
/// </summary>
[Route("api/providers")]
[Authorize(Roles = "Clinic,Nurse")]
public class ProviderController : ClinicBaseController
{
    private readonly IProviderService _providers;
    public ProviderController(IProviderService providers) => _providers = providers;

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] ProviderRequest req)
    {
        var result = await _providers.CreateAsync(CurrentClinicId, req);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
        => Ok(await _providers.GetAsync(CurrentClinicId));

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] ProviderRequest req)
    {
        var result = await _providers.UpdateAsync(CurrentClinicId, id, req);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var result = await _providers.DeleteAsync(CurrentClinicId, id);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("{id:int}/schedule")]
    public async Task<IActionResult> SetSchedule(int id, [FromBody] ProviderScheduleRequest req)
    {
        var result = await _providers.SetScheduleAsync(CurrentClinicId, id, req);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpGet("{id:int}/schedule")]
    public async Task<IActionResult> GetSchedule(int id)
    {
        var result = await _providers.GetScheduleAsync(CurrentClinicId, id);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpGet("{id:int}/slots")]
    public async Task<IActionResult> GetSlots(int id, [FromQuery] DateTime? date)
        => Ok(await _providers.GetSlotsAsync(id, date));

    [HttpPost("{id:int}/slots/generate")]
    public async Task<IActionResult> GenerateSlots(int id, [FromBody] RegenerateSlotsRequest req)
    {
        var result = await _providers.GenerateSlotsAsync(CurrentClinicId, id, req);
        return result.Success ? Ok(result) : BadRequest(result);
    }
}

// ─── Clinic Appointments Controller ──────────────────────────────────────────

/// <summary>
/// GET  /api/clinic-appointments/provider/{providerId}  — Get appointments by provider
/// PUT  /api/clinic-appointments/{id}/status            — Update appointment status
/// </summary>
[Route("api/clinic-appointments")]
[Authorize(Roles = "Clinic,Nurse")]
public class ClinicAppointmentsController : ClinicBaseController
{
    private readonly IClinicAppointmentService _appointments;
    public ClinicAppointmentsController(IClinicAppointmentService appointments) => _appointments = appointments;

    [HttpGet("provider/{providerId:int}")]
    public async Task<IActionResult> GetByProvider(int providerId, [FromQuery] DateTime? date)
    {
        var result = await _appointments.GetByProviderAsync(providerId, date);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPut("{id:int}/status")]
    public async Task<IActionResult> UpdateStatus(int id, [FromBody] UpdateClinicAppointmentStatusRequest req)
    {
        var result = await _appointments.UpdateStatusAsync(id, req.Status);
        return result.Success ? Ok(result) : BadRequest(result);
    }
}

// ─── Queue Controller ─────────────────────────────────────────────────────────

/// <summary>
/// GET  /api/doctors/{id}/queue?date=today  — Get doctor queue (Waiting/InProgress/Completed)
/// PUT  /api/queue/{id}/status              — Update queue item status
/// GET  /api/queue/provider/{providerId}    — Get queue by provider
/// </summary>
[Route("api")]
public class QueueController : ClinicBaseController
{
    private readonly IQueueService _queue;
    public QueueController(IQueueService queue) => _queue = queue;

    /// <summary>
    /// GET /api/doctors/{id}/queue?date=today
    /// Returns groups: Waiting, InProgress, Completed.
    /// </summary>
    [HttpGet("doctors/{id:int}/queue")]
    [Authorize(Roles = "Clinic,Nurse,Doctor")]
    public async Task<IActionResult> GetDoctorQueue(int id, [FromQuery] DateTime? date)
    {
        var result = await _queue.GetQueueAsync(id, date);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPut("queue/{id:int}/status")]
    [Authorize(Roles = "Clinic,Nurse")]
    public async Task<IActionResult> UpdateStatus(int id, [FromBody] UpdateQueueStatusRequest req)
    {
        var result = await _queue.UpdateStatusAsync(id, req.Status);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpGet("queue/provider/{providerId:int}")]
    [Authorize(Roles = "Clinic,Nurse,Doctor")]
    public async Task<IActionResult> GetByProvider(int providerId)
        => Ok(await _queue.GetByProviderAsync(providerId));
}

// ─── Walk-In Controller ───────────────────────────────────────────────────────

/// <summary>
/// POST /api/walk-ins
/// GET  /api/walk-ins/provider/{providerId}
/// </summary>
[Route("api/walk-ins")]
[Authorize(Roles = "Clinic,Nurse")]
public class WalkInController : ClinicBaseController
{
    private readonly IWalkInService _walkIn;
    public WalkInController(IWalkInService walkIn) => _walkIn = walkIn;

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] WalkInRequest req)
    {
        var result = await _walkIn.CreateAsync(req);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpGet("provider/{providerId:int}")]
    public async Task<IActionResult> GetByProvider(int providerId)
        => Ok(await _walkIn.GetByProviderAsync(providerId));
}

// ─── Payment Controller ───────────────────────────────────────────────────────

/// <summary>
/// POST /api/payments                  — Create payment for appointment
/// PUT  /api/payments/{id}/mark-paid   — Mark as paid
/// GET  /api/payments/summary          — Revenue summary
/// </summary>
[Route("api/payments")]
[Authorize(Roles = "Clinic,Nurse")]
public class PaymentController : ClinicBaseController
{
    private readonly IPaymentService _payments;
    public PaymentController(IPaymentService payments) => _payments = payments;

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreatePaymentRequest req)
    {
        var result = await _payments.CreateAsync(req.AppointmentId, req.Amount);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPut("{id:int}/mark-paid")]
    public async Task<IActionResult> MarkPaid(int id)
    {
        var result = await _payments.MarkPaidAsync(id);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpGet("summary")]
    public async Task<IActionResult> Summary([FromQuery] DateTime? date)
        => Ok(await _payments.GetSummaryAsync(CurrentClinicId, date));
}

public class CreatePaymentRequest
{
    public int AppointmentId { get; set; }
    public decimal Amount { get; set; }
}

// ─── Dashboard Controller ─────────────────────────────────────────────────────

/// <summary>
/// GET /api/dashboard/overview
/// GET /api/dashboard/revenue
/// GET /api/dashboard/appointments-stats
/// </summary>
[Route("api/dashboard")]
[Authorize(Roles = "Clinic,Nurse")]
public class DashboardController : ClinicBaseController
{
    private readonly IDashboardService _dashboard;
    public DashboardController(IDashboardService dashboard) => _dashboard = dashboard;

    [HttpGet("overview")]
    public async Task<IActionResult> Overview()
        => Ok(await _dashboard.GetOverviewAsync(CurrentClinicId));

    [HttpGet("revenue")]
    public async Task<IActionResult> Revenue([FromQuery] DateTime? date)
        => Ok(await _dashboard.GetRevenueAsync(CurrentClinicId, date));

    [HttpGet("appointments-stats")]
    public async Task<IActionResult> AppointmentsStats([FromQuery] DateTime? date)
        => Ok(await _dashboard.GetAppointmentsStatsAsync(CurrentClinicId, date));
}

// ─── Clinic Staff Notifications Controller ────────────────────────────────────

/// <summary>
/// GET   /api/clinic-notifications
/// PATCH /api/clinic-notifications/{id}/read
/// </summary>
[Route("api/clinic-notifications")]
[Authorize(Roles = "Clinic,Nurse")]
public class ClinicNotificationsController : ClinicBaseController
{
    private readonly INotificationService _notifications;
    public ClinicNotificationsController(INotificationService notifications) => _notifications = notifications;

    [HttpGet]
    public async Task<IActionResult> GetAll()
        => Ok(await _notifications.GetAsync(CurrentClinicUserId));

    [HttpPatch("{id:int}/read")]
    public async Task<IActionResult> MarkRead(int id)
    {
        var result = await _notifications.MarkReadAsync(CurrentClinicUserId, id);
        return result.Success ? Ok(result) : BadRequest(result);
    }
}

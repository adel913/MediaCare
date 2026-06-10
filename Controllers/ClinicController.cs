using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MedicalApp.API.DTOs.Clinic;
using MedicalApp.API.DTOs.Doctor;
using MedicalApp.API.Services.Interfaces;

namespace MedicalApp.API.Controllers
{
    public class ClinicController : BaseApiController
    {
        private readonly IClinicService _clinicService;

        public ClinicController(IClinicService clinicService)
        {
            _clinicService = clinicService;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll([FromQuery] string? search)
        {
            var result = await _clinicService.GetAllClinicsAsync(search);
            return StatusCode(result.StatusCode, result);
        }

        [HttpGet("nearby", Order = 0)]
        public async Task<IActionResult> GetNearby(
            [FromQuery] double lat,
            [FromQuery] double lng,
            [FromQuery] double radiusKm = 5,
            [FromQuery] string? specialization = null,
            [FromQuery] string? search = null)
        {
            var result = await _clinicService.GetNearbyClinicsAsync(lat, lng, radiusKm, specialization, search);
            return StatusCode(result.StatusCode, result);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var result = await _clinicService.GetClinicByIdAsync(id);
            return StatusCode(result.StatusCode, result);
        }

        [Authorize(Roles = "ClinicAdmin")]
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateClinicDto dto)
        {
            var result = await _clinicService.CreateClinicAsync(GetUserId(), dto);
            return StatusCode(result.StatusCode, result);
        }

        [Authorize(Roles = "ClinicAdmin")]
        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] UpdateClinicDto dto)
        {
            var result = await _clinicService.UpdateClinicAsync(id, GetUserId(), dto);
            return StatusCode(result.StatusCode, result);
        }

        [Authorize(Roles = "ClinicAdmin")]
        [HttpGet("doctors")]
        public async Task<IActionResult> GetClinicDoctors()
        {
            var result = await _clinicService.GetClinicDoctorsAsync(GetUserId());
            return StatusCode(result.StatusCode, result);
        }

        [Authorize(Roles = "ClinicAdmin")]
        [HttpGet("doctors/scan/{qrCodeKey}")]
        public async Task<IActionResult> ScanDoctorQr(string qrCodeKey)
        {
            var result = await _clinicService.ScanDoctorQrAsync(GetUserId(), qrCodeKey);
            return StatusCode(result.StatusCode, result);
        }

        [Authorize(Roles = "ClinicAdmin")]
        [HttpPost("doctors/register")]
        public async Task<IActionResult> RegisterClinicDoctor([FromBody] UpdateClinicDoctorDto dto)
        {
            var result = await _clinicService.RegisterClinicDoctorAsync(GetUserId(), dto);
            return StatusCode(result.StatusCode, result);
        }

        [Authorize(Roles = "ClinicAdmin")]
        [HttpGet("doctors/{doctorId}")]
        public async Task<IActionResult> GetClinicDoctorDetails(int doctorId)
        {
            var result = await _clinicService.GetClinicDoctorDetailsAsync(GetUserId(), doctorId);
            return StatusCode(result.StatusCode, result);
        }

        [Authorize(Roles = "ClinicAdmin")]
        [HttpPut("doctors/{doctorId}")]
        public async Task<IActionResult> UpdateClinicDoctor(int doctorId, [FromBody] UpdateClinicDoctorDto dto)
        {
            var result = await _clinicService.UpdateClinicDoctorAsync(GetUserId(), doctorId, dto);
            return StatusCode(result.StatusCode, result);
        }

        [Authorize(Roles = "ClinicAdmin")]
        [HttpDelete("doctors/{doctorId}")]
        public async Task<IActionResult> RemoveClinicDoctor(int doctorId)
        {
            var result = await _clinicService.RemoveClinicDoctorAsync(GetUserId(), doctorId);
            return StatusCode(result.StatusCode, result);
        }

        [Authorize(Roles = "ClinicAdmin")]
        [HttpGet("profile")]
        public async Task<IActionResult> GetClinicProfile()
        {
            var result = await _clinicService.GetClinicProfileAsync(GetUserId());
            return StatusCode(result.StatusCode, result);
        }

        [Authorize(Roles = "ClinicAdmin")]
        [HttpPut("profile")]
        public async Task<IActionResult> UpdateClinicProfile([FromBody] UpdateClinicDto dto)
        {
            var result = await _clinicService.UpdateClinicProfileAsync(GetUserId(), dto);
            return StatusCode(result.StatusCode, result);
        }
    }
}

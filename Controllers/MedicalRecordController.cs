using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MedicalApp.API.DTOs.MedicalRecord;
using MedicalApp.API.Services.Interfaces;

namespace MedicalApp.API.Controllers
{
    [Authorize]
    public class MedicalRecordController : BaseApiController
    {
        private readonly IMedicalRecordService _medicalRecordService;

        public MedicalRecordController(IMedicalRecordService medicalRecordService)
        {
            _medicalRecordService = medicalRecordService;
        }

        [Authorize(Roles = "Doctor")]
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateMedicalRecordDto dto)
        {
            var result = await _medicalRecordService.CreateRecordAsync(GetUserId(), dto);
            return StatusCode(result.StatusCode, result);
        }

        [Authorize(Roles = "Doctor,Patient")]
        [HttpGet("patient/{patientId}")]
        public async Task<IActionResult> GetPatientRecords(int patientId)
        {
            var result = await _medicalRecordService.GetPatientRecordsAsync(patientId, GetUserId());
            return StatusCode(result.StatusCode, result);
        }

        [Authorize(Roles = "Doctor,Patient")]
        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var result = await _medicalRecordService.GetRecordByIdAsync(id, GetUserId());
            return StatusCode(result.StatusCode, result);
        }
    }
}

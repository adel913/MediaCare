using Microsoft.EntityFrameworkCore;
using MedicalApp.API.Data.Repositories;
using MedicalApp.API.DTOs.MedicalRecord;
using MedicalApp.API.Helpers;
using MedicalApp.API.Services.Interfaces;
using System.Text.Json;

namespace MedicalApp.API.Services.Implementations
{
    public class MedicalRecordService : IMedicalRecordService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ITelegramOtpService _telegramOtpService;

        public MedicalRecordService(IUnitOfWork unitOfWork, ITelegramOtpService telegramOtpService)
        {
            _unitOfWork = unitOfWork;
            _telegramOtpService = telegramOtpService;
        }

        public async Task<ApiResponse<MedicalRecordDto>> CreateRecordAsync(int userId, CreateMedicalRecordDto dto)
        {
            var doctor = await _unitOfWork.Doctors.Query().Include(d => d.User)
                .FirstOrDefaultAsync(d => d.UserId == userId);
            if (doctor == null)
                return ApiResponse<MedicalRecordDto>.Failure("Doctor profile not found", 404);

            var patient = await _unitOfWork.Patients.Query().Include(p => p.User)
                .FirstOrDefaultAsync(p => p.Id == dto.PatientId);
            if (patient == null)
                return ApiResponse<MedicalRecordDto>.Failure("Patient not found", 404);

            // Handle structured prescriptions JSON serialization
            string? prescriptionContent = dto.Prescription;
            if (dto.Medications != null && dto.Medications.Any())
            {
                prescriptionContent = JsonSerializer.Serialize(dto.Medications);
            }

            // Handle structured treatment plan JSON serialization
            string? treatmentPlanContent = dto.TreatmentPlan;
            if (dto.RecommendedCare != null && dto.RecommendedCare.Any())
            {
                treatmentPlanContent = JsonSerializer.Serialize(dto.RecommendedCare);
            }

            // Handle observations (saved to Notes)
            string? notesContent = dto.Notes;
            if (!string.IsNullOrWhiteSpace(dto.Observations))
            {
                notesContent = dto.Observations;
            }

            if (dto.AppointmentId.HasValue)
            {
                var linkedAppointment = await _unitOfWork.Appointments.Query()
                    .FirstOrDefaultAsync(a => a.Id == dto.AppointmentId.Value);
                if (linkedAppointment?.Status == Models.Enums.AppointmentStatus.Completed)
                    return ApiResponse<MedicalRecordDto>.Failure(
                        "Medical records are append-only. Completed consultations cannot be modified.", 400);
            }

            var record = new Models.Entities.MedicalRecord
            {
                PatientId = dto.PatientId,
                FamilyMemberId = dto.FamilyMemberId,
                DoctorId = doctor.Id,
                AppointmentId = dto.AppointmentId,
                Diagnosis = dto.Diagnosis,
                Prescription = prescriptionContent,
                Instructions = dto.Instructions,
                TreatmentPlan = treatmentPlanContent,
                Notes = notesContent,
                Symptoms = dto.Symptoms,
                Subjective = dto.Subjective,
                Objective = dto.Objective,
                Assessment = dto.Assessment,
                Plan = dto.Plan,
                BloodPressure = dto.BloodPressure,
                HeartRate = dto.HeartRate,
                Weight = dto.Weight,
                VisitDate = DateTime.UtcNow
            };

            await _unitOfWork.MedicalRecords.AddAsync(record);
            await _unitOfWork.CompleteAsync();

            // Create notification and try to send Telegram mapping
            string notificationPrescriptionText = "";
            if (dto.Medications != null && dto.Medications.Any())
            {
                notificationPrescriptionText = string.Join("\n", dto.Medications.Select(m => $"- *{m.Name}* ({m.Category}) - {m.Dosage} | {m.Duration}"));
            }
            else
            {
                notificationPrescriptionText = dto.Prescription ?? string.Empty;
            }

            string notificationCareText = "";
            if (dto.RecommendedCare != null && dto.RecommendedCare.Any())
            {
                notificationCareText = string.Join("\n", dto.RecommendedCare.Select(c => $"- {c}"));
            }
            else
            {
                notificationCareText = dto.TreatmentPlan ?? string.Empty;
            }

            string notificationNotesText = dto.Observations ?? dto.Notes ?? "No additional notes.";

            if (!string.IsNullOrWhiteSpace(notificationPrescriptionText) && patient.User != null && !string.IsNullOrEmpty(patient.User.PhoneNumber))
            {
                try
                {
                    var msgText = $"📝 *A new prescription has been recorded for you*\n\n" +
                                  $"👨‍⚕️ *Doctor:* Dr. {doctor.User.FullName}\n" +
                                  $"📋 *Main diagnosis:* {dto.Diagnosis}\n\n" +
                                  $"💊 *Prescription and required medications:*\n{notificationPrescriptionText}\n\n" +
                                  (string.IsNullOrWhiteSpace(notificationCareText) ? "" : $"📅 *Care and treatment instructions:*\n{notificationCareText}\n\n") +
                                  $"💡 *Doctor's notes:* {notificationNotesText}\n\n" +
                                  $"This prescription has been automatically saved to your medical history in the app ✅";

                    await _telegramOtpService.SendNotificationAsync(patient.User.PhoneNumber, msgText);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Prescription Telegram Notification Error] {ex.Message}");
                }
            }

            record.Doctor = doctor;
            record.Patient = patient;

            var result = MapToDto(record);
            return ApiResponse<MedicalRecordDto>.Success(result, "Medical record created successfully", 201);
        }

        public async Task<ApiResponse<List<MedicalRecordDto>>> GetPatientRecordsAsync(int patientId, int userId)
        {
            var user = await _unitOfWork.Users.GetByIdAsync(userId);
            if (user == null) return ApiResponse<List<MedicalRecordDto>>.Failure("User not found", 404);

            // 1. If Patient: Can only view their own records
            if (user.Role == Models.Enums.UserRole.Patient)
            {
                var patient = await _unitOfWork.Patients.Query().FirstOrDefaultAsync(p => p.UserId == userId);
                if (patient == null || patient.Id != patientId)
                    return ApiResponse<List<MedicalRecordDto>>.Failure("You are not authorized to view this record", 403);
            }

            // 2. If Doctor: Can only view records of patients they have treated (has an appointment with them)
            if (user.Role == Models.Enums.UserRole.Doctor)
            {
                var doctor = await _unitOfWork.Doctors.Query().FirstOrDefaultAsync(d => d.UserId == userId);
                if (doctor == null) return ApiResponse<List<MedicalRecordDto>>.Failure("Unauthorized", 403);

                var hasBooking = await _unitOfWork.Appointments.Query()
                    .AnyAsync(a => a.DoctorId == doctor.Id
                        && a.PatientId == patientId
                        && a.Status != Models.Enums.AppointmentStatus.Cancelled);

                if (!hasBooking)
                    return ApiResponse<List<MedicalRecordDto>>.Failure("You are not authorized to view this patient's records", 403);
            }

            var records = await _unitOfWork.MedicalRecords.Query()
                .Include(r => r.Patient).ThenInclude(p => p!.User)
                .Include(r => r.FamilyMember)
                .Include(r => r.Doctor).ThenInclude(d => d.User)
                .Where(r => r.PatientId == patientId)
                .OrderByDescending(r => r.VisitDate)
                .ToListAsync();

            var dtos = records.Select(MapToDto).ToList();
            return ApiResponse<List<MedicalRecordDto>>.Success(dtos);
        }

        public async Task<ApiResponse<MedicalRecordDto>> GetRecordByIdAsync(int recordId, int userId)
        {
            var record = await _unitOfWork.MedicalRecords.Query()
                .Include(r => r.Patient).ThenInclude(p => p!.User)
                .Include(r => r.Doctor).ThenInclude(d => d.User)
                .FirstOrDefaultAsync(r => r.Id == recordId);

            if (record == null)
                return ApiResponse<MedicalRecordDto>.Failure("Medical record not found", 404);

            var user = await _unitOfWork.Users.GetByIdAsync(userId);
            if (user == null) return ApiResponse<MedicalRecordDto>.Failure("User not found", 404);

            // 1. If Patient: Can only view their own record
            if (user.Role == Models.Enums.UserRole.Patient)
            {
                var patient = await _unitOfWork.Patients.Query().FirstOrDefaultAsync(p => p.UserId == userId);
                if (patient == null || record.PatientId != patient.Id)
                    return ApiResponse<MedicalRecordDto>.Failure("You are not authorized to view this record", 403);
            }

            // 2. If Doctor: Can only view records they created OR records of patients they have treated
            if (user.Role == Models.Enums.UserRole.Doctor)
            {
                var doctor = await _unitOfWork.Doctors.Query().FirstOrDefaultAsync(d => d.UserId == userId);
                if (doctor == null) return ApiResponse<MedicalRecordDto>.Failure("Unauthorized", 403);

                if (record.DoctorId != doctor.Id)
                {
                    var hasBooking = await _unitOfWork.Appointments.Query()
                        .AnyAsync(a => a.DoctorId == doctor.Id
                            && a.PatientId == record.PatientId
                            && a.Status != Models.Enums.AppointmentStatus.Cancelled);

                    if (!hasBooking)
                        return ApiResponse<MedicalRecordDto>.Failure("You cannot view this record", 403);
                }
            }

            return ApiResponse<MedicalRecordDto>.Success(MapToDto(record));
        }

        // Unified mapper helper
        private MedicalRecordDto MapToDto(Models.Entities.MedicalRecord record)
        {
            var dto = new MedicalRecordDto
            {
                Id = record.Id,
                PatientId = record.PatientId,
                PatientName = record.FamilyMember?.Name ?? record.Patient?.User?.FullName ?? string.Empty,
                FamilyMemberId = record.FamilyMemberId,
                FamilyMemberName = record.FamilyMember?.Name,
                DoctorId = record.DoctorId,
                DoctorName = record.Doctor?.User?.FullName ?? string.Empty,
                DoctorSpecialization = record.Doctor?.Specialization ?? string.Empty,
                DoctorProfileImageUrl = record.Doctor?.User?.ProfileImageUrl ?? string.Empty,
                AppointmentId = record.AppointmentId,
                Diagnosis = record.Diagnosis,
                Prescription = record.Prescription,
                Instructions = record.Instructions,
                TreatmentPlan = record.TreatmentPlan,
                Notes = record.Notes,
                Symptoms = record.Symptoms,
                Subjective = record.Subjective,
                Objective = record.Objective,
                Assessment = record.Assessment,
                Plan = record.Plan,
                BloodPressure = record.BloodPressure,
                HeartRate = record.HeartRate,
                Weight = record.Weight,
                Observations = record.Notes,
                VisitDate = record.VisitDate,
                CreatedAt = record.CreatedAt
            };

            // Safely parse Medications JSON
            if (!string.IsNullOrWhiteSpace(record.Prescription))
            {
                var trimPresc = record.Prescription.Trim();
                if (trimPresc.StartsWith("[") && trimPresc.EndsWith("]"))
                {
                    try
                    {
                        dto.Medications = JsonSerializer.Deserialize<List<PrescribedMedicationDto>>(record.Prescription, new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true
                        });
                    }
                    catch
                    {
                        dto.Medications = new List<PrescribedMedicationDto>
                        {
                            new PrescribedMedicationDto { Name = record.Prescription }
                        };
                    }
                }
                else
                {
                    dto.Medications = new List<PrescribedMedicationDto>
                    {
                        new PrescribedMedicationDto { Name = record.Prescription }
                    };
                }
            }

            // Safely parse Recommended Care JSON
            if (!string.IsNullOrWhiteSpace(record.TreatmentPlan))
            {
                var trimPlan = record.TreatmentPlan.Trim();
                if (trimPlan.StartsWith("[") && trimPlan.EndsWith("]"))
                {
                    try
                    {
                        dto.RecommendedCare = JsonSerializer.Deserialize<List<string>>(record.TreatmentPlan, new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true
                        });
                    }
                    catch
                    {
                        dto.RecommendedCare = new List<string> { record.TreatmentPlan };
                    }
                }
                else
                {
                    dto.RecommendedCare = new List<string> { record.TreatmentPlan };
                }
            }

            return dto;
        }
    }
}

using System.Data;
using Microsoft.EntityFrameworkCore;
using MedicalApp.API.Data.Repositories;
using MedicalApp.API.DTOs.Appointment;
using MedicalApp.API.Helpers;
using MedicalApp.API.Models.Entities;
using MedicalApp.API.Models.Enums;
using MedicalApp.API.Services.Interfaces;

namespace MedicalApp.API.Services.Implementations
{
    public class AppointmentService : IAppointmentService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<AppointmentService> _logger;

        public AppointmentService(IUnitOfWork unitOfWork, ILogger<AppointmentService> logger)
        {
            _unitOfWork = unitOfWork;
            _logger = logger;
        }

        // ===== Create Patient Appointment (Online Booking via App) =====
        public async Task<ApiResponse<AppointmentDto>> CreateAppointmentAsync(int userId, CreateAppointmentDto dto)
        {
            // Find Patient by UserId
            var patient = await _unitOfWork.Patients.Query()
                .Include(p => p.User)
                .FirstOrDefaultAsync(p => p.UserId == userId);

            if (patient == null)
                return ApiResponse<AppointmentDto>.Failure("Patient account not found", 404);

            // Find Doctor
            var doctor = await _unitOfWork.Doctors.Query()
                .Include(d => d.User)
                .FirstOrDefaultAsync(d => d.Id == dto.DoctorId);

            if (doctor == null)
                return ApiResponse<AppointmentDto>.Failure("Selected doctor not found", 404);

            // Check if slot falls on active doctor schedule day
            var dayOfWeek = dto.AppointmentDate.DayOfWeek;
            var schedule = await _unitOfWork.DoctorSchedules.Query()
                .FirstOrDefaultAsync(s => s.DoctorId == dto.DoctorId && s.DayOfWeek == dayOfWeek && s.IsActive);

            if (schedule == null)
                return ApiResponse<AppointmentDto>.Failure("Doctor has no available working hours on this day", 400);

            // Validate that the slot is inside operating hours
            if (dto.StartTime < schedule.StartTime || dto.StartTime >= schedule.EndTime)
                return ApiResponse<AppointmentDto>.Failure("Booking time is outside the doctor's official working hours", 400);

            using var transaction = await _unitOfWork.Context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            try
            {
                // Prevent double booking (Check for active appointments in the same slot)
                var doubleBookingExists = await _unitOfWork.Appointments.Query()
                    .AnyAsync(a => a.DoctorId == dto.DoctorId
                        && a.AppointmentDate.Date == dto.AppointmentDate.Date
                        && a.StartTime == dto.StartTime
                        && a.Status != AppointmentStatus.Cancelled);

                if (doubleBookingExists)
                    return ApiResponse<AppointmentDto>.Failure("Sorry, this slot is already booked! Please choose another time", 409);

                // Prevent a patient or family member from booking overlapping appointments
                var hasOverlapForPerson = await _unitOfWork.Appointments.Query()
                    .AnyAsync(a => a.AppointmentDate.Date == dto.AppointmentDate.Date
                        && a.StartTime == dto.StartTime
                        && a.Status != AppointmentStatus.Cancelled
                        && (
                            a.PatientId == patient.Id
                            || (dto.FamilyMemberId.HasValue && a.FamilyMemberId == dto.FamilyMemberId)
                        ));

                if (hasOverlapForPerson)
                    return ApiResponse<AppointmentDto>.Failure("You cannot book more than one appointment for the same person at the same time", 409);

                // Calculate Queue Number for the day
                var todayAppointmentsCount = await _unitOfWork.Appointments.Query()
                    .CountAsync(a => a.DoctorId == dto.DoctorId 
                        && a.AppointmentDate.Date == dto.AppointmentDate.Date 
                        && a.Status != AppointmentStatus.Cancelled);

            var queueNumber = todayAppointmentsCount + 1;
            var consultationFee = await ResolveConsultationFeeAsync(dto.DoctorId);

            // Validate Family Member if provided
            Models.Entities.FamilyMember? familyMember = null;
            if (dto.FamilyMemberId.HasValue)
            {
                familyMember = await _unitOfWork.FamilyMembers.Query()
                    .FirstOrDefaultAsync(fm => fm.Id == dto.FamilyMemberId.Value && fm.PatientId == patient.Id);

                if (familyMember == null)
                    return ApiResponse<AppointmentDto>.Failure("Family member not found or does not belong to this account", 400);
            }

            // Create appointment
            var appointment = new Appointment
            {
                PatientId = patient.Id,
                FamilyMemberId = dto.FamilyMemberId,
                DoctorId = dto.DoctorId,
                AppointmentDate = dto.AppointmentDate.Date,
                StartTime = dto.StartTime,
                EndTime = dto.StartTime.Add(TimeSpan.FromMinutes(schedule.SlotDurationMinutes)),
                Status = AppointmentStatus.Confirmed,
                QueueNumber = queueNumber,
                QueueStatus = QueueStatus.Waiting,
                ConsultationFee = consultationFee,
                PaymentStatus = PaymentStatus.Pending,
                Notes = dto.Notes
            };

            await _unitOfWork.Appointments.AddAsync(appointment);
            
            // Create notification for patient
            var notification = new Notification
            {
                UserId = userId,
                Title = "Booking confirmed",
                Message = $"Your booking with Dr. {doctor.User.FullName} on {appointment.AppointmentDate:yyyy-MM-dd} at {appointment.StartTime:hh\\:mm} has been confirmed. Consultation fee: {consultationFee:N2}."
            };
            await _unitOfWork.Notifications.AddAsync(notification);

            await _unitOfWork.CompleteAsync();
            await transaction.CommitAsync();

            _logger.LogInformation("Appointment created successfully. Queue: {Queue}", queueNumber);

            return ApiResponse<AppointmentDto>.Success(MapToDto(appointment), "Appointment booked successfully", 201);
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

        // ===== Create Clinic Walk-in Booking (Offline/Online via Admin Panel) =====
        public async Task<ApiResponse<AppointmentDto>> CreateClinicAppointmentAsync(int userId, ClinicCreateAppointmentDto dto)
        {
            // Verify User Authorization
            var requestingUser = await _unitOfWork.Users.GetByIdAsync(userId);
            if (requestingUser != null)
            {
                if (requestingUser.Role == UserRole.ClinicAdmin)
                {
                    var admin = await _unitOfWork.ClinicAdmins.Query().FirstOrDefaultAsync(ca => ca.UserId == userId);
                    if (admin == null || !await _unitOfWork.DoctorClinics.AnyAsync(dc => dc.ClinicId == admin.ClinicId && dc.DoctorId == dto.DoctorId && dc.IsActive))
                        return ApiResponse<AppointmentDto>.Failure("You are not authorized to book for this doctor", 403);
                }
                else if (requestingUser.Role == UserRole.Doctor)
                {
                    var requestingDoctor = await _unitOfWork.Doctors.Query().FirstOrDefaultAsync(d => d.UserId == userId);
                    if (requestingDoctor == null || requestingDoctor.Id != dto.DoctorId)
                        return ApiResponse<AppointmentDto>.Failure("You are not authorized to book on behalf of another doctor", 403);
                }
            }

            // Verify Doctor exists
            var doctor = await _unitOfWork.Doctors.Query()
                .Include(d => d.User)
                .FirstOrDefaultAsync(d => d.Id == dto.DoctorId);

            if (doctor == null)
                return ApiResponse<AppointmentDto>.Failure("Selected doctor not found", 404);

            // Check if slot falls on active doctor schedule day
            var dayOfWeek = dto.AppointmentDate.DayOfWeek;
            var schedule = await _unitOfWork.DoctorSchedules.Query()
                .FirstOrDefaultAsync(s => s.DoctorId == dto.DoctorId && s.DayOfWeek == dayOfWeek && s.IsActive);

            if (schedule == null)
                return ApiResponse<AppointmentDto>.Failure("Doctor has no available working hours on this day", 400);

            // Validate slot inside operating hours
            if (dto.StartTime < schedule.StartTime || dto.StartTime >= schedule.EndTime)
                return ApiResponse<AppointmentDto>.Failure("Booking time is outside the doctor's official working hours", 400);

            using var transaction = await _unitOfWork.Context.Database.BeginTransactionAsync(IsolationLevel.Serializable);

            // Prevent double booking
            var doubleBookingExists = await _unitOfWork.Appointments.Query()
                .AnyAsync(a => a.DoctorId == dto.DoctorId
                    && a.AppointmentDate.Date == dto.AppointmentDate.Date
                    && a.StartTime == dto.StartTime
                    && a.Status != AppointmentStatus.Cancelled);

            if (doubleBookingExists)
                return ApiResponse<AppointmentDto>.Failure("Sorry, this slot is already booked! Please choose another time", 409);

            if (dto.PatientId.HasValue)
            {
                var patientOverlapExists = await _unitOfWork.Appointments.Query()
                    .AnyAsync(a => a.AppointmentDate.Date == dto.AppointmentDate.Date
                        && a.StartTime == dto.StartTime
                        && a.Status != AppointmentStatus.Cancelled
                        && a.PatientId == dto.PatientId.Value);

                if (patientOverlapExists)
                    return ApiResponse<AppointmentDto>.Failure("You cannot book more than one appointment for the same patient at the same time", 409);
            }

            // Determine if booking is for a registered patient or offline walk-in
            Patient? registeredPatient = null;
            if (dto.PatientId.HasValue)
            {
                registeredPatient = await _unitOfWork.Patients.Query()
                    .Include(p => p.User)
                    .FirstOrDefaultAsync(p => p.Id == dto.PatientId.Value);

                if (registeredPatient == null)
                    return ApiResponse<AppointmentDto>.Failure("Registered patient not found", 404);
            }
            else if (string.IsNullOrWhiteSpace(dto.OfflinePatientName))
            {
                return ApiResponse<AppointmentDto>.Failure("Please provide the offline patient name or specify a registered patient ID", 400);
            }

            // Calculate Queue Number for the day
            var todayAppointmentsCount = await _unitOfWork.Appointments.Query()
                .CountAsync(a => a.DoctorId == dto.DoctorId 
                    && a.AppointmentDate.Date == dto.AppointmentDate.Date 
                    && a.Status != AppointmentStatus.Cancelled);

            var queueNumber = todayAppointmentsCount + 1;
            var consultationFee = await ResolveConsultationFeeAsync(dto.DoctorId);

            // Create appointment
            var appointment = new Appointment
            {
                PatientId = dto.PatientId,
                DoctorId = dto.DoctorId,
                AppointmentDate = dto.AppointmentDate.Date,
                StartTime = dto.StartTime,
                EndTime = dto.StartTime.Add(TimeSpan.FromMinutes(schedule.SlotDurationMinutes)),
                Status = AppointmentStatus.Confirmed,
                QueueNumber = queueNumber,
                QueueStatus = QueueStatus.Waiting,
                ConsultationFee = consultationFee,
                IsPaid = dto.IsPaid,
                PaymentStatus = dto.IsPaid ? PaymentStatus.Paid : PaymentStatus.Pending,
                Notes = dto.Notes,
                OfflinePatientName = dto.OfflinePatientName,
                OfflinePatientPhone = dto.OfflinePatientPhone,
                IsEmergency = dto.IsEmergency,
                ChiefComplaint = dto.ChiefComplaint,
                PaymentMethod = dto.PaymentMethod,
                OfflinePatientAge = dto.OfflinePatientAge,
                OfflinePatientGender = dto.OfflinePatientGender
            };

            await _unitOfWork.Appointments.AddAsync(appointment);

            // Create notification for registered patient if applicable
            if (dto.PatientId.HasValue && registeredPatient != null)
            {
                var notification = new Notification
                {
                    UserId = registeredPatient.UserId,
                    Title = "Booking confirmed",
                    Message = $"Your booking with Dr. {doctor.User.FullName} on {appointment.AppointmentDate:yyyy-MM-dd} at {appointment.StartTime:hh\\:mm} has been confirmed. Consultation fee: {consultationFee:N2}."
                };
                await _unitOfWork.Notifications.AddAsync(notification);
            }

            await _unitOfWork.CompleteAsync();
            await transaction.CommitAsync();

            _logger.LogInformation("Walk-in clinic booking created successfully. Queue: {Queue}", queueNumber);

            // Load navigations if patient registered
            if (dto.PatientId.HasValue && appointment.Patient == null)
            {
                appointment.Patient = registeredPatient;
            }
            appointment.Doctor = doctor;

            return ApiResponse<AppointmentDto>.Success(MapToDto(appointment), "Clinic appointment booked successfully", 201);
        }

        // ===== Get Patient's Bookings list =====
        public async Task<ApiResponse<List<AppointmentDto>>> GetPatientAppointmentsAsync(int userId, string? filter = null, AppointmentStatus? status = null)
        {
            var patient = await _unitOfWork.Patients.Query()
                .FirstOrDefaultAsync(p => p.UserId == userId);

            if (patient == null)
                return ApiResponse<List<AppointmentDto>>.Failure("Patient account not found", 404);

            var query = _unitOfWork.Appointments.Query()
                .Include(a => a.Doctor)
                    .ThenInclude(d => d.User)
                .Include(a => a.Doctor)
                    .ThenInclude(d => d.DoctorClinics)
                        .ThenInclude(dc => dc.Clinic)
                .Include(a => a.FamilyMember)
                .Where(a => a.PatientId == patient.Id);

            if (!string.IsNullOrWhiteSpace(filter))
            {
                if (filter.Equals("upcoming", StringComparison.OrdinalIgnoreCase))
                {
                    query = query.Where(a => a.Status == AppointmentStatus.Confirmed 
                                          || a.Status == AppointmentStatus.InProgress 
                                          || a.Status == AppointmentStatus.Pending);
                    
                    var appointmentsList = await query
                        .OrderBy(a => a.AppointmentDate)
                        .ThenBy(a => a.StartTime)
                        .ToListAsync();
                    
                    var dtosList = appointmentsList.Select(MapToDto).ToList();
                    return ApiResponse<List<AppointmentDto>>.Success(dtosList, "Upcoming patient appointments retrieved successfully");
                }
                else if (filter.Equals("completed", StringComparison.OrdinalIgnoreCase))
                {
                    query = query.Where(a => a.Status == AppointmentStatus.Completed);
                }
                else if (filter.Equals("cancelled", StringComparison.OrdinalIgnoreCase))
                {
                    query = query.Where(a => a.Status == AppointmentStatus.Cancelled 
                                          || a.Status == AppointmentStatus.NoShow);
                }
            }
            else if (status.HasValue)
            {
                query = query.Where(a => a.Status == status.Value);
            }

            var appointments = await query
                .OrderByDescending(a => a.AppointmentDate)
                .ThenByDescending(a => a.StartTime)
                .ToListAsync();

            var dtos = appointments.Select(MapToDto).ToList();
            return ApiResponse<List<AppointmentDto>>.Success(dtos, "Patient appointments retrieved successfully");
        }

        // ===== Get Doctor's Appointments list =====
        public async Task<ApiResponse<List<AppointmentDto>>> GetDoctorAppointmentsAsync(int userId, DateTime? date = null, AppointmentStatus? status = null)
        {
            var doctor = await _unitOfWork.Doctors.Query()
                .FirstOrDefaultAsync(d => d.UserId == userId);

            if (doctor == null)
                return ApiResponse<List<AppointmentDto>>.Failure("Doctor account not found", 404);

            var query = _unitOfWork.Appointments.Query()
                .Include(a => a.Patient)
                    .ThenInclude(p => p!.User)
                .Include(a => a.FamilyMember)
                .Where(a => a.DoctorId == doctor.Id);

            if (date.HasValue)
            {
                query = query.Where(a => a.AppointmentDate.Date == date.Value.Date);
            }

            if (status.HasValue)
            {
                query = query.Where(a => a.Status == status.Value);
            }

            var appointments = await query
                .OrderBy(a => a.AppointmentDate)
                .ThenBy(a => a.StartTime)
                .ToListAsync();

            var dtos = appointments.Select(MapToDto).ToList();
            return ApiResponse<List<AppointmentDto>>.Success(dtos, "Doctor appointments retrieved successfully");
        }

        // ===== Get Appointment By ID =====
        public async Task<ApiResponse<AppointmentDto>> GetAppointmentByIdAsync(int appointmentId, int userId)
        {
            var appointment = await _unitOfWork.Appointments.Query()
                .Include(a => a.Patient)
                    .ThenInclude(p => p!.User)
                .Include(a => a.FamilyMember)
                .Include(a => a.Doctor)
                    .ThenInclude(d => d.User)
                .Include(a => a.Doctor)
                    .ThenInclude(d => d.DoctorClinics)
                        .ThenInclude(dc => dc.Clinic)
                .FirstOrDefaultAsync(a => a.Id == appointmentId);

            if (appointment == null)
                return ApiResponse<AppointmentDto>.Failure("Appointment not found", 404);

            // Authorization check
            var user = await _unitOfWork.Users.GetByIdAsync(userId);
            if (user == null)
                return ApiResponse<AppointmentDto>.Failure("User not found", 404);

            if (user.Role == UserRole.Patient && appointment.PatientId != null)
            {
                var patient = await _unitOfWork.Patients.Query().FirstOrDefaultAsync(p => p.UserId == userId);
                if (patient == null || appointment.PatientId != patient.Id)
                    return ApiResponse<AppointmentDto>.Failure("You are not authorized to view this appointment", 403);
            }
            else if (user.Role == UserRole.Doctor)
            {
                var doctor = await _unitOfWork.Doctors.Query().FirstOrDefaultAsync(d => d.UserId == userId);
                if (doctor == null || appointment.DoctorId != doctor.Id)
                    return ApiResponse<AppointmentDto>.Failure("You are not authorized to view this appointment", 403);
            }
            else if (user.Role == UserRole.ClinicAdmin)
            {
                // Check if admin belongs to the clinic that the doctor operates in
                var admin = await _unitOfWork.ClinicAdmins.Query().FirstOrDefaultAsync(a => a.UserId == userId);
                var schedule = await _unitOfWork.DoctorSchedules.Query()
                    .FirstOrDefaultAsync(s => s.DoctorId == appointment.DoctorId);

                if (admin == null || schedule == null || schedule.ClinicId != admin.ClinicId)
                    return ApiResponse<AppointmentDto>.Failure("You are not authorized to view this appointment", 403);
            }

            return ApiResponse<AppointmentDto>.Success(MapToDto(appointment), "Appointment retrieved successfully");
        }

        // ===== Cancel Appointment =====
        public async Task<ApiResponse<AppointmentDto>> CancelAppointmentAsync(int appointmentId, int userId, CancelAppointmentDto dto)
        {
            var appointment = await _unitOfWork.Appointments.Query()
                .Include(a => a.Patient)
                    .ThenInclude(p => p!.User)
                .Include(a => a.Doctor)
                    .ThenInclude(d => d.User)
                .FirstOrDefaultAsync(a => a.Id == appointmentId);

            if (appointment == null)
                return ApiResponse<AppointmentDto>.Failure("Appointment not found", 404);

            var user = await _unitOfWork.Users.GetByIdAsync(userId);
            if (user == null)
                return ApiResponse<AppointmentDto>.Failure("User not found", 404);

            // Authorization check for cancel
            if (user.Role == UserRole.Patient)
            {
                var patient = await _unitOfWork.Patients.Query().FirstOrDefaultAsync(p => p.UserId == userId);
                if (patient == null || appointment.PatientId != patient.Id)
                    return ApiResponse<AppointmentDto>.Failure("You are not authorized to cancel this appointment", 403);
            }
            else if (user.Role == UserRole.Doctor)
            {
                var doctor = await _unitOfWork.Doctors.Query().FirstOrDefaultAsync(d => d.UserId == userId);
                if (doctor == null || appointment.DoctorId != doctor.Id)
                    return ApiResponse<AppointmentDto>.Failure("You are not authorized to cancel this appointment", 403);
            }

            // Refund is only allowed while the appointment is still waiting
            if (appointment.Status != AppointmentStatus.Confirmed
                || appointment.QueueStatus != QueueStatus.Waiting)
            {
                return ApiResponse<AppointmentDto>.Failure(
                    "Cancellation and refund are only allowed while the appointment is waiting. Consultations that have started or completed cannot be cancelled.",
                    400);
            }

            // Perform cancellation and refund
            appointment.Status = AppointmentStatus.Cancelled;
            appointment.CancellationReason = dto.Reason;
            appointment.QueueStatus = QueueStatus.Refunded;
            appointment.RefundStatus = RefundStatus.Processed;
            appointment.PaymentStatus = PaymentStatus.Refunded;
            appointment.IsPaid = false;

            _unitOfWork.Appointments.Update(appointment);

            // Create notification for patient if registered
            if (appointment.PatientId.HasValue && appointment.Patient != null)
            {
                string title = "Booking cancelled";
                string message = "";

                if (user.Role == UserRole.Patient)
                {
                    message = $"Your booking with Dr. {appointment.Doctor.User.FullName} on {appointment.AppointmentDate:yyyy-MM-dd} at {appointment.StartTime:hh\\:mm} has been cancelled at your request.";
                }
                else
                {
                    title = "Booking cancelled by clinic";
                    string reasonText = string.IsNullOrWhiteSpace(dto.Reason) ? "Not specified" : dto.Reason;
                    message = $"We would like to inform you that your booking with Dr. {appointment.Doctor.User.FullName} on {appointment.AppointmentDate:yyyy-MM-dd} at {appointment.StartTime:hh\\:mm} has been cancelled by the clinic. Reason: {reasonText}.";
                }

                var notification = new Notification
                {
                    UserId = appointment.Patient.UserId,
                    Title = title,
                    Message = message
                };
                await _unitOfWork.Notifications.AddAsync(notification);
            }

            await _unitOfWork.CompleteAsync();

            _logger.LogInformation("Appointment {Id} has been Cancelled and Refunded", appointmentId);

            return ApiResponse<AppointmentDto>.Success(MapToDto(appointment), "Appointment cancelled and refunded successfully");
        }

        // ===== Reschedule Appointment =====
        public async Task<ApiResponse<AppointmentDto>> RescheduleAppointmentAsync(int appointmentId, int userId, RescheduleAppointmentDto dto)
        {
            var appointment = await _unitOfWork.Appointments.Query()
                .Include(a => a.Patient)
                    .ThenInclude(p => p!.User)
                .Include(a => a.Doctor)
                    .ThenInclude(d => d.User)
                .FirstOrDefaultAsync(a => a.Id == appointmentId);

            if (appointment == null)
                return ApiResponse<AppointmentDto>.Failure("Appointment not found", 404);

            var user = await _unitOfWork.Users.GetByIdAsync(userId);
            if (user == null)
                return ApiResponse<AppointmentDto>.Failure("User not found", 404);

            // Authorization check
            if (user.Role == UserRole.Patient)
            {
                var patient = await _unitOfWork.Patients.Query().FirstOrDefaultAsync(p => p.UserId == userId);
                if (patient == null || appointment.PatientId != patient.Id)
                    return ApiResponse<AppointmentDto>.Failure("You are not authorized to reschedule this booking", 403);
            }
            else if (user.Role == UserRole.Doctor)
            {
                var doctor = await _unitOfWork.Doctors.Query().FirstOrDefaultAsync(d => d.UserId == userId);
                if (doctor == null || appointment.DoctorId != doctor.Id)
                    return ApiResponse<AppointmentDto>.Failure("You are not authorized to reschedule this booking", 403);
            }

            // Validate schedule
            var dayOfWeek = dto.AppointmentDate.DayOfWeek;
            var schedule = await _unitOfWork.DoctorSchedules.Query()
                .FirstOrDefaultAsync(s => s.DoctorId == appointment.DoctorId && s.DayOfWeek == dayOfWeek && s.IsActive);

            if (schedule == null)
                return ApiResponse<AppointmentDto>.Failure("Doctor has no available working hours on this new day", 400);

            if (dto.StartTime < schedule.StartTime || dto.StartTime >= schedule.EndTime)
                return ApiResponse<AppointmentDto>.Failure("The new booking time is outside the doctor's official working hours", 400);

            using var transaction = await _unitOfWork.Context.Database.BeginTransactionAsync(IsolationLevel.Serializable);

            // Prevent double booking
            var doubleBookingExists = await _unitOfWork.Appointments.Query()
                .AnyAsync(a => a.DoctorId == appointment.DoctorId 
                    && a.AppointmentDate.Date == dto.AppointmentDate.Date 
                    && a.StartTime == dto.StartTime 
                    && a.Id != appointment.Id
                    && a.Status != AppointmentStatus.Cancelled);

            if (doubleBookingExists)
                return ApiResponse<AppointmentDto>.Failure("Sorry, this new slot is already booked! Please choose another time", 409);

            // Prevent overlapping appointments for the same patient or family member
            var overlapExists = await _unitOfWork.Appointments.Query()
                .AnyAsync(a => a.AppointmentDate.Date == dto.AppointmentDate.Date
                    && a.StartTime == dto.StartTime
                    && a.Id != appointment.Id
                    && a.Status != AppointmentStatus.Cancelled
                    && (
                        (appointment.PatientId.HasValue && a.PatientId == appointment.PatientId)
                        || (appointment.FamilyMemberId.HasValue && a.FamilyMemberId == appointment.FamilyMemberId)
                    ));

            if (overlapExists)
                return ApiResponse<AppointmentDto>.Failure("You cannot reschedule this appointment to a time that overlaps with another appointment for the same person", 409);

            var oldDateStr = appointment.AppointmentDate.ToString("yyyy-MM-dd");
            var oldTimeStr = appointment.StartTime.ToString(@"hh\:mm");
            var newDateStr = dto.AppointmentDate.ToString("yyyy-MM-dd");
            var newTimeStr = dto.StartTime.ToString(@"hh\:mm");

            // Calculate new Queue Number for the new day
            var newDayAppointmentsCount = await _unitOfWork.Appointments.Query()
                .CountAsync(a => a.DoctorId == appointment.DoctorId 
                    && a.AppointmentDate.Date == dto.AppointmentDate.Date 
                    && a.Status != AppointmentStatus.Cancelled);

            var queueNumber = newDayAppointmentsCount + 1;

            // Perform Rescheduling
            appointment.AppointmentDate = dto.AppointmentDate.Date;
            appointment.StartTime = dto.StartTime;
            appointment.EndTime = dto.StartTime.Add(TimeSpan.FromMinutes(schedule.SlotDurationMinutes));
            appointment.QueueNumber = queueNumber;
            appointment.QueueStatus = QueueStatus.Waiting;
            appointment.Status = AppointmentStatus.Confirmed;

            _unitOfWork.Appointments.Update(appointment);

            // Send notification to Patient
            if (appointment.PatientId.HasValue && appointment.Patient != null)
            {
                var patientNotification = new Notification
                {
                    UserId = appointment.Patient.UserId,
                    Title = "Booking rescheduled",
                    Message = $"Your booking with Dr. {appointment.Doctor.User.FullName} has been rescheduled to {newDateStr} at {newTimeStr} (previously {oldDateStr} at {oldTimeStr})."
                };
                await _unitOfWork.Notifications.AddAsync(patientNotification);
            }

            // Send notification to Doctor
            var doctorNotification = new Notification
            {
                UserId = appointment.Doctor.UserId,
                Title = "Patient booking rescheduled",
                Message = $"Patient {appointment.Patient?.User?.FullName ?? "walk-in"} has rescheduled their booking to {newDateStr} at {newTimeStr} (previously {oldDateStr} at {oldTimeStr})."
            };
            await _unitOfWork.Notifications.AddAsync(doctorNotification);

            // Send notification to Clinic Admins
            var clinicAdmins = await _unitOfWork.ClinicAdmins.Query()
                .Where(ca => ca.ClinicId == schedule.ClinicId)
                .Select(ca => ca.UserId)
                .ToListAsync();

            foreach (var adminUserId in clinicAdmins)
            {
                var adminNotification = new Notification
                {
                    UserId = adminUserId,
                    Title = "Patient booking rescheduled",
                    Message = $"Patient {appointment.Patient?.User?.FullName ?? "walk-in"} has rescheduled their booking with Dr. {appointment.Doctor.User.FullName} to {newDateStr} at {newTimeStr} (previously {oldDateStr} at {oldTimeStr})."
                };
                await _unitOfWork.Notifications.AddAsync(adminNotification);
            }

            await _unitOfWork.CompleteAsync();
            await transaction.CommitAsync();

            _logger.LogInformation("Appointment {Id} Rescheduled successfully to {Date} {Time}", appointmentId, newDateStr, newTimeStr);

            return ApiResponse<AppointmentDto>.Success(MapToDto(appointment), "Booking rescheduled successfully and notifications sent");
        }

        // ===== Update Appointment Status =====
        public async Task<ApiResponse<AppointmentDto>> UpdateStatusAsync(int appointmentId, int userId, UpdateAppointmentStatusDto dto)
        {
            var appointment = await _unitOfWork.Appointments.Query()
                .Include(a => a.Patient)
                    .ThenInclude(p => p!.User)
                .Include(a => a.Doctor)
                    .ThenInclude(d => d.User)
                .FirstOrDefaultAsync(a => a.Id == appointmentId);

            if (appointment == null)
                return ApiResponse<AppointmentDto>.Failure("Appointment not found", 404);

            var doctor = await _unitOfWork.Doctors.Query().FirstOrDefaultAsync(d => d.UserId == userId);
            if (doctor == null || appointment.DoctorId != doctor.Id)
                return ApiResponse<AppointmentDto>.Failure("You are not authorized to change this appointment's status", 403);

            // ===== Enforce valid status transitions =====
            var validTransitions = new Dictionary<AppointmentStatus, List<AppointmentStatus>>
            {
                { AppointmentStatus.Pending,     new() { AppointmentStatus.Confirmed, AppointmentStatus.Cancelled } },
                { AppointmentStatus.Confirmed,   new() { AppointmentStatus.InProgress, AppointmentStatus.Cancelled, AppointmentStatus.NoShow } },
                { AppointmentStatus.InProgress,  new() { AppointmentStatus.Completed, AppointmentStatus.Cancelled } },
                { AppointmentStatus.Completed,   new() { } }, // Terminal state
                { AppointmentStatus.Cancelled,   new() { } }, // Terminal state
                { AppointmentStatus.NoShow,      new() { AppointmentStatus.Confirmed } }  // Allow rebook
            };

            if (!validTransitions.ContainsKey(appointment.Status) 
                || !validTransitions[appointment.Status].Contains(dto.Status))
            {
                return ApiResponse<AppointmentDto>.Failure(
                    $"Cannot change appointment status from '{appointment.Status}' to '{dto.Status}'", 400);
            }

            if (dto.Status == AppointmentStatus.Completed)
            {
                return ApiResponse<AppointmentDto>.Failure(
                    "Use the complete consultation endpoint to finish a consultation", 400);
            }

            // Update statuses
            appointment.Status = dto.Status;
            appointment.QueueStatus = dto.Status switch
            {
                AppointmentStatus.Confirmed => QueueStatus.Waiting,
                AppointmentStatus.InProgress => QueueStatus.InConsultation,
                AppointmentStatus.Completed => QueueStatus.Completed,
                AppointmentStatus.Cancelled => QueueStatus.Refunded,
                AppointmentStatus.NoShow => null, // Remove from active queue
                _ => appointment.QueueStatus
            };

            if (dto.Status == AppointmentStatus.Cancelled)
            {
                if (appointment.QueueStatus != QueueStatus.Waiting)
                {
                    return ApiResponse<AppointmentDto>.Failure(
                        "Cancellation and refund are only allowed while the appointment is waiting", 400);
                }

                appointment.RefundStatus = RefundStatus.Processed;
                appointment.PaymentStatus = PaymentStatus.Refunded;
                appointment.IsPaid = false;
            }

            _unitOfWork.Appointments.Update(appointment);
            await _unitOfWork.CompleteAsync();

            _logger.LogInformation("Appointment status updated to {Status}", dto.Status);

            return ApiResponse<AppointmentDto>.Success(MapToDto(appointment), "Appointment status updated successfully");
        }

        // ===== Get Today's Queue =====
        public async Task<ApiResponse<List<AppointmentDto>>> GetTodayQueueAsync(int userId)
        {
            var doctor = await _unitOfWork.Doctors.Query().FirstOrDefaultAsync(d => d.UserId == userId);
            if (doctor == null)
                return ApiResponse<List<AppointmentDto>>.Failure("Doctor account not found", 404);

            var todayQueue = await _unitOfWork.Appointments.Query()
                .Include(a => a.Patient)
                    .ThenInclude(p => p!.User)
                .Where(a => a.DoctorId == doctor.Id 
                    && a.AppointmentDate.Date == DateTime.Today 
                    && a.Status != AppointmentStatus.Cancelled)
                .OrderBy(a => a.StartTime)
                .ToListAsync();

            var dtos = todayQueue.Select(MapToDto).ToList();
            return ApiResponse<List<AppointmentDto>>.Success(dtos, "Live queue retrieved for today");
        }

        // ===== Get Patient Live Queue Tracker =====
        public async Task<ApiResponse<LiveQueueTrackerDto>> GetLiveQueueTrackerAsync(int appointmentId, int userId)
        {
            var appointment = await _unitOfWork.Appointments.Query()
                .Include(a => a.Doctor)
                    .ThenInclude(d => d.User)
                .FirstOrDefaultAsync(a => a.Id == appointmentId);

            if (appointment == null)
                return ApiResponse<LiveQueueTrackerDto>.Failure("Appointment not found", 404);

            // Patient Auth check
            var patient = await _unitOfWork.Patients.Query().FirstOrDefaultAsync(p => p.UserId == userId);
            if (patient == null || appointment.PatientId != patient.Id)
                return ApiResponse<LiveQueueTrackerDto>.Failure("You are not authorized to track this appointment", 403);

            if (appointment.Status == AppointmentStatus.Cancelled)
                return ApiResponse<LiveQueueTrackerDto>.Failure("This appointment has already been cancelled", 400);

            // Fetch currently serving patient in consultation
            var activeServing = await _unitOfWork.Appointments.Query()
                .Where(a => a.DoctorId == appointment.DoctorId 
                    && a.AppointmentDate.Date == appointment.AppointmentDate.Date 
                    && a.QueueStatus == QueueStatus.InConsultation)
                .FirstOrDefaultAsync();

            int currentServingNumber = 0;
            if (activeServing != null)
            {
                currentServingNumber = activeServing.QueueNumber ?? 0;
            }
            else
            {
                // Find last completed patient for today
                var lastCompleted = await _unitOfWork.Appointments.Query()
                    .Where(a => a.DoctorId == appointment.DoctorId 
                        && a.AppointmentDate.Date == appointment.AppointmentDate.Date 
                        && a.QueueStatus == QueueStatus.Completed)
                    .OrderByDescending(a => a.QueueNumber)
                    .FirstOrDefaultAsync();

                currentServingNumber = lastCompleted?.QueueNumber ?? 0;
            }

            // Calculate patients ahead in the queue
            int myQueueNumber = appointment.QueueNumber ?? 0;
            int patientsAhead = await _unitOfWork.Appointments.Query()
                .CountAsync(a => a.DoctorId == appointment.DoctorId
                    && a.AppointmentDate.Date == appointment.AppointmentDate.Date
                    && a.QueueStatus == QueueStatus.Waiting
                    && a.StartTime < appointment.StartTime);

            // Estimating 15 minutes wait time per waiting patient
            int estimatedWaitTimeMinutes = patientsAhead * 15;

            var trackerDto = new LiveQueueTrackerDto
            {
                AppointmentId = appointment.Id,
                MyQueueNumber = myQueueNumber,
                CurrentServingNumber = currentServingNumber,
                PatientsAheadOfMe = patientsAhead,
                EstimatedWaitTimeMinutes = estimatedWaitTimeMinutes,
                MyQueueStatus = appointment.QueueStatus,
                DoctorName = appointment.Doctor.User.FullName
            };

            return ApiResponse<LiveQueueTrackerDto>.Success(trackerDto, "Live queue tracker updated");
        }

        // ===== Call Next Patient in Queue =====
        public async Task<ApiResponse<AppointmentDto>> CallNextPatientInQueueAsync(int doctorUserId)
        {
            var doctor = await _unitOfWork.Doctors.Query().FirstOrDefaultAsync(d => d.UserId == doctorUserId);
            if (doctor == null)
                return ApiResponse<AppointmentDto>.Failure("Doctor account not found", 404);

            var today = DateTime.Today;

            // 1. Mark currently serving patient (if any) as Completed
            var activeServing = await _unitOfWork.Appointments.Query()
                .FirstOrDefaultAsync(a => a.DoctorId == doctor.Id 
                    && a.AppointmentDate.Date == today 
                    && a.QueueStatus == QueueStatus.InConsultation);

            if (activeServing != null)
            {
                activeServing.Status = AppointmentStatus.Completed;
                activeServing.QueueStatus = QueueStatus.Completed;
                _unitOfWork.Appointments.Update(activeServing);
            }

            // 2. Find next patient in Waiting state
            var nextPatient = await _unitOfWork.Appointments.Query()
                .Include(a => a.Patient)
                    .ThenInclude(p => p!.User)
                .Where(a => a.DoctorId == doctor.Id 
                    && a.AppointmentDate.Date == today 
                    && a.QueueStatus == QueueStatus.Waiting 
                    && a.Status == AppointmentStatus.Confirmed)
                .OrderBy(a => a.StartTime)
                .FirstOrDefaultAsync();

            if (nextPatient == null)
            {
                await _unitOfWork.CompleteAsync(); // Save currently serving completion
                return ApiResponse<AppointmentDto>.Failure("No patients waiting today", 404);
            }

            // 3. Mark next patient as InConsultation / InProgress
            nextPatient.Status = AppointmentStatus.InProgress;
            nextPatient.QueueStatus = QueueStatus.InConsultation;

            _unitOfWork.Appointments.Update(nextPatient);
            await _unitOfWork.CompleteAsync();

            _logger.LogInformation("Doctor {Doc} called next patient Queue {Queue}", doctor.Id, nextPatient.QueueNumber);

            return ApiResponse<AppointmentDto>.Success(MapToDto(nextPatient), $"Patient number {nextPatient.QueueNumber} has been called in");
        }

        // ===== Map Entity to DTO Helper =====
        private AppointmentDto MapToDto(Appointment app)
        {
            string patientName = "Walk-in patient";
            string phone = string.Empty;

            if (app.Patient != null && app.Patient.User != null)
            {
                patientName = app.Patient.User.FullName;
                phone = app.Patient.User.PhoneNumber;
            }
            else if (!string.IsNullOrWhiteSpace(app.OfflinePatientName))
            {
                patientName = app.OfflinePatientName;
                phone = app.OfflinePatientPhone ?? string.Empty;
            }

            string? familyMemberName = null;
            if (app.FamilyMember != null)
            {
                familyMemberName = app.FamilyMember.Name;
            }
            else if (app.FamilyMemberId.HasValue)
            {
                // In case it wasn't loaded via Include, fetch it synchronously or just leave as null
                // To avoid sync-over-async in MapToDto, we should rely on Includes in the queries.
                // Assuming it's Included.
            }

            int? clinicId = null;
            string? clinicName = null;
            string? clinicAddress = null;

            var activeClinic = app.Doctor?.DoctorClinics?.FirstOrDefault(dc => dc.IsActive)?.Clinic;
            if (activeClinic != null)
            {
                clinicId = activeClinic.Id;
                clinicName = activeClinic.Name;
                clinicAddress = $"{activeClinic.Area}, {activeClinic.Government}";
            }

            int? currentServingNumber = null;
            if (app.AppointmentDate.Date == DateTime.Today 
                && app.Status == AppointmentStatus.Confirmed 
                && app.QueueStatus == QueueStatus.Waiting)
            {
                var activeServing = _unitOfWork.Appointments.Query()
                    .FirstOrDefault(a => a.DoctorId == app.DoctorId 
                        && a.AppointmentDate.Date == DateTime.Today 
                        && a.QueueStatus == QueueStatus.InConsultation);

                if (activeServing != null)
                {
                    currentServingNumber = activeServing.QueueNumber ?? 0;
                }
                else
                {
                    var lastCompleted = _unitOfWork.Appointments.Query()
                        .Where(a => a.DoctorId == app.DoctorId 
                            && a.AppointmentDate.Date == DateTime.Today 
                            && a.QueueStatus == QueueStatus.Completed)
                        .OrderByDescending(a => a.QueueNumber)
                        .FirstOrDefault();

                    currentServingNumber = lastCompleted?.QueueNumber ?? 0;
                }
            }

            return new AppointmentDto
            {
                Id = app.Id,
                PatientId = app.PatientId,
                PatientName = patientName,
                FamilyMemberId = app.FamilyMemberId,
                FamilyMemberName = familyMemberName,
                OfflinePatientPhone = phone,
                DoctorId = app.DoctorId,
                DoctorName = app.Doctor?.User?.FullName ?? string.Empty,
                Specialization = app.Doctor?.Specialization ?? string.Empty,
                DoctorProfileImageUrl = app.Doctor?.User?.ProfileImageUrl,
                ClinicId = clinicId,
                ClinicName = clinicName,
                ClinicAddress = clinicAddress,
                CurrentServingNumber = currentServingNumber,
                AppointmentDate = app.AppointmentDate,
                StartTime = app.StartTime,
                EndTime = app.EndTime,
                Status = app.Status,
                StatusText = app.Status.ToString(),
                QueueNumber = app.QueueNumber,
                QueueStatus = app.QueueStatus,
                RefundStatus = app.RefundStatus,
                RefundStatusText = app.RefundStatus == RefundStatus.Pending ? "Refund Pending" : 
                                   app.RefundStatus == RefundStatus.Processed ? "Refund Processed" : "",
                Notes = app.Notes,
                CancellationReason = app.CancellationReason,
                IsEmergency = app.IsEmergency,
                ChiefComplaint = app.ChiefComplaint,
                IsPaid = app.IsPaid,
                PaymentStatus = app.PaymentStatus,
                PaymentStatusText = app.PaymentStatus.ToString(),
                ConsultationFee = app.ConsultationFee,
                PaymentMethod = app.PaymentMethod,
                PaymentMethodText = app.PaymentMethod.ToString(),
                OfflinePatientAge = app.OfflinePatientAge,
                OfflinePatientGender = app.OfflinePatientGender,
                CreatedAt = app.CreatedAt
            };
        }

        // ===== Get Clinic Dashboard Overview =====
        public async Task<ApiResponse<ClinicDashboardOverviewDto>> GetClinicDashboardOverviewAsync(int clinicAdminUserId, int? doctorId)
        {
            var admin = await _unitOfWork.ClinicAdmins.Query()
                .FirstOrDefaultAsync(a => a.UserId == clinicAdminUserId);
            if (admin == null)
                return ApiResponse<ClinicDashboardOverviewDto>.Failure("You are not authorized to view the clinic overview as an admin", 403);

            int clinicId = admin.ClinicId;
            var today = DateTime.Today;

            var query = _unitOfWork.Appointments.Query()
                .Include(a => a.Doctor)
                    .ThenInclude(d => d.DoctorClinics)
                .Where(a => a.AppointmentDate.Date == today && a.Status != AppointmentStatus.Cancelled);

            // Filter doctors who belong to the admin's clinic
            query = query.Where(a => a.Doctor.DoctorClinics.Any(dc => dc.ClinicId == clinicId && dc.IsActive));

            if (doctorId.HasValue)
            {
                query = query.Where(a => a.DoctorId == doctorId.Value);
            }

            var appointments = await query.ToListAsync();

            int paidCount = appointments.Count(a => a.PaymentStatus == PaymentStatus.Paid);
            int walkInCount = appointments.Count(a => a.PatientId == null || !string.IsNullOrEmpty(a.OfflinePatientName));
            decimal totalRevenue = appointments
                .Where(a => a.PaymentStatus == PaymentStatus.Paid)
                .Sum(a => a.ConsultationFee);

            var overview = new ClinicDashboardOverviewDto
            {
                PaidCount = paidCount,
                WalkInCount = walkInCount,
                TodayRevenueAmount = totalRevenue
            };

            return ApiResponse<ClinicDashboardOverviewDto>.Success(overview, "Statistics retrieved successfully");
        }

        // ===== Get Clinic Today Queue =====
        public async Task<ApiResponse<List<AppointmentDto>>> GetClinicTodayQueueAsync(int clinicAdminUserId, int doctorId)
        {
            var admin = await _unitOfWork.ClinicAdmins.Query()
                .FirstOrDefaultAsync(a => a.UserId == clinicAdminUserId);
            if (admin == null)
                return ApiResponse<List<AppointmentDto>>.Failure("You are not authorized to fetch the clinic queue as an admin", 403);

            int clinicId = admin.ClinicId;

            var isLinked = await _unitOfWork.DoctorClinics.Query()
                .AnyAsync(dc => dc.ClinicId == clinicId && dc.DoctorId == doctorId && dc.IsActive);
            if (!isLinked)
                return ApiResponse<List<AppointmentDto>>.Failure("Selected doctor does not work in this clinic", 400);

            var today = DateTime.Today;

            var appointments = await _unitOfWork.Appointments.Query()
                .Include(a => a.Patient)
                    .ThenInclude(p => p!.User)
                .Include(a => a.Doctor)
                    .ThenInclude(d => d.User)
                .Include(a => a.Doctor.DoctorClinics)
                    .ThenInclude(dc => dc.Clinic)
                .Where(a => a.DoctorId == doctorId 
                    && a.AppointmentDate.Date == today 
                    && a.Status != AppointmentStatus.Cancelled)
                .ToListAsync();

            // Custom sorting:
            // 1. QueueStatus.InConsultation (order weight 0)
            // 2. IsEmergency && Waiting (order weight 1)
            // 3. !IsEmergency && Waiting (order weight 2)
            // 4. Completed/Others (order weight 3)
            var sortedAppointments = appointments
                .OrderBy(a => a.QueueStatus == QueueStatus.InConsultation ? 0 :
                              a.QueueStatus == QueueStatus.Waiting && a.IsEmergency ? 1 :
                              a.QueueStatus == QueueStatus.Waiting && !a.IsEmergency ? 2 : 3)
                .ThenBy(a => a.StartTime)
                .Select(MapToDto)
                .ToList();

            return ApiResponse<List<AppointmentDto>>.Success(sortedAppointments, "Queue retrieved successfully");
        }

        // ===== Start Checkup =====
        public async Task<ApiResponse<AppointmentDto>> StartCheckupAsync(int staffUserId, int appointmentId)
        {
            var user = await _unitOfWork.Users.GetByIdAsync(staffUserId);
            if (user == null || user.Role != UserRole.ClinicAdmin)
                return ApiResponse<AppointmentDto>.Failure("You are not authorized to start a patient checkup", 403);

            var admin = await _unitOfWork.ClinicAdmins.Query()
                .FirstOrDefaultAsync(a => a.UserId == staffUserId);
            if (admin == null)
                return ApiResponse<AppointmentDto>.Failure("You are not authorized to start a patient checkup", 403);

            int clinicId = admin.ClinicId;

            var appointment = await _unitOfWork.Appointments.Query()
                .Include(a => a.Patient)
                    .ThenInclude(p => p!.User)
                .Include(a => a.FamilyMember)
                .Include(a => a.Doctor)
                    .ThenInclude(d => d.User)
                .Include(a => a.Doctor.DoctorClinics)
                .FirstOrDefaultAsync(a => a.Id == appointmentId);

            if (appointment == null)
                return ApiResponse<AppointmentDto>.Failure("Appointment not found", 404);

            var isLinked = appointment.Doctor.DoctorClinics.Any(dc => dc.ClinicId == clinicId && dc.IsActive);
            if (!isLinked)
                return ApiResponse<AppointmentDto>.Failure("This clinic admin does not have permission to manage this appointment", 403);

            if (appointment.AppointmentDate.Date != DateTime.Today)
                return ApiResponse<AppointmentDto>.Failure("Checkup can only be started for today's appointments", 400);

            if (appointment.Status == AppointmentStatus.Cancelled)
                return ApiResponse<AppointmentDto>.Failure("This appointment is already cancelled and cannot be started", 400);

            if (appointment.Status == AppointmentStatus.Completed)
                return ApiResponse<AppointmentDto>.Failure("This appointment is already completed", 400);

            if (appointment.Status == AppointmentStatus.InProgress
                || appointment.QueueStatus == QueueStatus.InConsultation)
            {
                return ApiResponse<AppointmentDto>.Failure("Checkup has already been started for this appointment", 400);
            }

            if (appointment.QueueStatus != QueueStatus.Waiting)
                return ApiResponse<AppointmentDto>.Failure("Only waiting appointments can be started", 400);

            appointment.Status = AppointmentStatus.InProgress;
            appointment.QueueStatus = QueueStatus.InConsultation;

            _unitOfWork.Appointments.Update(appointment);
            await _unitOfWork.CompleteAsync();

            _logger.LogInformation("Clinic staff started checkup for appointment {Id}", appointmentId);

            return ApiResponse<AppointmentDto>.Success(MapToDto(appointment), "Checkup started successfully");
        }

        // ===== Get Payments Dashboard =====
        public async Task<ApiResponse<PaymentsDashboardDto>> GetPaymentsDashboardAsync(int clinicAdminUserId, int? doctorId, string timeframe)
        {
            var admin = await _unitOfWork.ClinicAdmins.Query()
                .FirstOrDefaultAsync(a => a.UserId == clinicAdminUserId);
            if (admin == null)
                return ApiResponse<PaymentsDashboardDto>.Failure("You are not authorized to view the payments dashboard as an admin", 403);

            int clinicId = admin.ClinicId;

            // Determine date filter based on timeframe
            var today = DateTime.Today;
            DateTime startDate = today;

            timeframe = timeframe.ToLower();
            if (timeframe == "week")
            {
                // Last 7 days
                startDate = today.AddDays(-7);
            }
            else if (timeframe == "month")
            {
                // Last 30 days
                startDate = today.AddDays(-30);
            }
            else
            {
                // Default to today
                startDate = today;
            }

            var query = _unitOfWork.Appointments.Query()
                .Include(a => a.Patient)
                    .ThenInclude(p => p!.User)
                .Include(a => a.Doctor)
                    .ThenInclude(d => d.User)
                .Include(a => a.Doctor.DoctorClinics)
                .Where(a => a.AppointmentDate.Date >= startDate.Date && a.AppointmentDate.Date <= today.Date);

            // Filter by clinic
            query = query.Where(a => a.Doctor.DoctorClinics.Any(dc => dc.ClinicId == clinicId && dc.IsActive));

            if (doctorId.HasValue)
            {
                query = query.Where(a => a.DoctorId == doctorId.Value);
            }

            var appointments = await query.ToListAsync();

            // Calculate stats
            // Revenue is calculated from active paid appointments
            var activePaid = appointments.Where(a => a.PaymentStatus == PaymentStatus.Paid).ToList();
            var refunded = appointments.Where(a => a.PaymentStatus == PaymentStatus.Refunded).ToList();

            decimal totalRevenue = activePaid.Sum(a => a.ConsultationFee);
            decimal cashAmount = activePaid.Where(a => a.PaymentMethod == PaymentMethod.Cash).Sum(a => a.ConsultationFee);
            decimal onlineAmount = activePaid.Where(a => a.PaymentMethod == PaymentMethod.Online).Sum(a => a.ConsultationFee);
            decimal refundsAmount = refunded.Sum(a => a.ConsultationFee);

            double cashPercentage = totalRevenue > 0 ? (double)(cashAmount / totalRevenue) * 100.0 : 0.0;
            double onlinePercentage = totalRevenue > 0 ? (double)(onlineAmount / totalRevenue) * 100.0 : 0.0;
            double refundsPercentage = totalRevenue > 0 ? (double)(refundsAmount / totalRevenue) * 100.0 : 0.0;

            // Map Recent Transactions
            var recentTransactions = appointments
                .OrderByDescending(a => a.AppointmentDate)
                .ThenByDescending(a => a.StartTime)
                .Select(a => {
                    string name = "Walk-in patient";
                    if (a.Patient?.User != null)
                        name = a.Patient.User.FullName;
                    else if (!string.IsNullOrEmpty(a.OfflinePatientName))
                        name = a.OfflinePatientName;

                    string status = a.PaymentStatus.ToString();

                    return new TransactionDto
                    {
                        AppointmentId = a.Id,
                        PatientName = name,
                        DateTime = a.AppointmentDate.Date.Add(a.StartTime),
                        Amount = a.ConsultationFee,
                        Status = status,
                        PaymentMethod = a.PaymentMethod,
                        PaymentMethodText = a.PaymentMethod.ToString()
                    };
                })
                .Take(50) // Cap recent transactions at 50
                .ToList();

            var dto = new PaymentsDashboardDto
            {
                TotalRevenue = totalRevenue,
                RevenueGrowthText = timeframe == "month" ? "+12% vs last month" : timeframe == "week" ? "+3% vs last week" : "+5% vs yesterday",
                CashAmount = cashAmount,
                CashPercentage = Math.Round(cashPercentage, 1),
                OnlineAmount = onlineAmount,
                OnlinePercentage = Math.Round(onlinePercentage, 1),
                RefundsAmount = refundsAmount,
                RefundsPercentage = Math.Round(refundsPercentage, 1),
                RecentTransactions = recentTransactions
            };

            return ApiResponse<PaymentsDashboardDto>.Success(dto, "Payments dashboard data retrieved successfully");
        }

        private async Task<decimal> ResolveConsultationFeeAsync(int doctorId)
        {
            var doctorClinic = await _unitOfWork.DoctorClinics.Query()
                .Where(dc => dc.DoctorId == doctorId && dc.IsActive)
                .OrderBy(dc => dc.Id)
                .FirstOrDefaultAsync();

            if (doctorClinic?.ConsultationFee.HasValue == true)
                return doctorClinic.ConsultationFee.Value;

            var doctor = await _unitOfWork.Doctors.GetByIdAsync(doctorId);
            return doctor?.ConsultationFee ?? 0m;
        }
    }
}

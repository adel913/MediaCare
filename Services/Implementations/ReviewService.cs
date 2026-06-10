using Microsoft.EntityFrameworkCore;
using MedicalApp.API.Data.Repositories;
using MedicalApp.API.DTOs.Review;
using MedicalApp.API.Helpers;
using MedicalApp.API.Services.Interfaces;

namespace MedicalApp.API.Services.Implementations
{
    public class ReviewService : IReviewService
    {
        private readonly IUnitOfWork _unitOfWork;

        public ReviewService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<ApiResponse<ReviewDto>> CreateReviewAsync(int userId, CreateReviewDto dto)
        {
            var patient = await _unitOfWork.Patients.Query().Include(p => p.User)
                .FirstOrDefaultAsync(p => p.UserId == userId);
            if (patient == null)
                return ApiResponse<ReviewDto>.Failure("Patient profile not found", 404);

            var doctor = await _unitOfWork.Doctors.Query().Include(d => d.User)
                .FirstOrDefaultAsync(d => d.Id == dto.DoctorId);
            if (doctor == null)
                return ApiResponse<ReviewDto>.Failure("Doctor not found", 404);

            // Check if already reviewed this appointment
            if (dto.AppointmentId.HasValue)
            {
                var existingReview = await _unitOfWork.Reviews.Query()
                    .AnyAsync(r => r.AppointmentId == dto.AppointmentId && r.PatientId == patient.Id);
                if (existingReview)
                    return ApiResponse<ReviewDto>.Failure("You have already reviewed this appointment", 409);
            }

            var review = new Models.Entities.Review
            {
                PatientId = patient.Id,
                DoctorId = dto.DoctorId,
                AppointmentId = dto.AppointmentId,
                Rating = dto.Rating,
                Comment = dto.Comment
            };

            await _unitOfWork.Reviews.AddAsync(review);

            // Update doctor's average rating
            var allRatings = await _unitOfWork.Reviews.Query()
                .Where(r => r.DoctorId == dto.DoctorId)
                .Select(r => r.Rating).ToListAsync();
            allRatings.Add(dto.Rating);

            doctor.AverageRating = allRatings.Average();
            doctor.TotalReviews = allRatings.Count;

            _unitOfWork.Doctors.Update(doctor);
            await _unitOfWork.CompleteAsync();

            var result = new ReviewDto
            {
                Id = review.Id,
                PatientId = review.PatientId,
                PatientName = patient.User.FullName,
                DoctorId = review.DoctorId,
                DoctorName = doctor.User.FullName,
                AppointmentId = review.AppointmentId,
                Rating = review.Rating,
                Comment = review.Comment,
                CreatedAt = review.CreatedAt
            };

            return ApiResponse<ReviewDto>.Success(result, "Review added successfully", 201);
        }

        public async Task<ApiResponse<List<ReviewDto>>> GetDoctorReviewsAsync(int doctorId)
        {
            var reviews = await _unitOfWork.Reviews.Query()
                .Include(r => r.Patient).ThenInclude(p => p!.User)
                .Include(r => r.Doctor).ThenInclude(d => d.User)
                .Where(r => r.DoctorId == doctorId)
                .OrderByDescending(r => r.CreatedAt)
                .Select(r => new ReviewDto
                {
                    Id = r.Id,
                    PatientId = r.PatientId,
                    PatientName = r.Patient != null ? r.Patient.User.FullName : string.Empty,
                    DoctorId = r.DoctorId,
                    DoctorName = r.Doctor.User.FullName,
                    AppointmentId = r.AppointmentId,
                    Rating = r.Rating,
                    Comment = r.Comment,
                    CreatedAt = r.CreatedAt
                }).ToListAsync();

            return ApiResponse<List<ReviewDto>>.Success(reviews);
        }
    }
}

using AutoMapper;
using MedicalApp.API.Models.Entities;
using MedicalApp.API.DTOs.Auth;
using MedicalApp.API.DTOs.Patient;
using MedicalApp.API.DTOs.Doctor;
using MedicalApp.API.DTOs.Appointment;
using MedicalApp.API.DTOs.Clinic;

namespace MedicalApp.API.Helpers
{
    public class MappingProfile : Profile
    {
        public MappingProfile()
        {
            // AutoMapper mappings
            CreateMap<Patient, PatientProfileDto>();

            CreateMap<Doctor, DoctorProfileDto>()
                .ForMember(dest => dest.FullName, opt => opt.MapFrom(src => src.User.FullName))
                .ForMember(dest => dest.PhoneNumber, opt => opt.MapFrom(src => src.User.PhoneNumber))
                .ForMember(dest => dest.Email, opt => opt.MapFrom(src => src.User.Email));

            CreateMap<Doctor, DoctorListItemDto>();
            CreateMap<Clinic, ClinicDto>();

            CreateMap<Appointment, AppointmentDto>()
                .ForMember(dest => dest.PatientName, opt => opt.MapFrom(src => src.Patient != null ? src.Patient.User.FullName : src.OfflinePatientName))
                .ForMember(dest => dest.DoctorName, opt => opt.MapFrom(src => src.Doctor.User.FullName));

            CreateMap<DoctorSchedule, MedicalApp.API.DTOs.Schedule.DoctorScheduleDto>();
        }
    }
}

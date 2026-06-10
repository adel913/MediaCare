using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace MedicalApp.API.DTOs.Community
{
    public class CreatePostDto
    {
        [Required(ErrorMessage = "Post content is required")]
        [MaxLength(2000, ErrorMessage = "Post content cannot exceed 2000 characters")]
        public string Content { get; set; } = string.Empty;

        [MaxLength(100, ErrorMessage = "Specialization name cannot exceed 100 characters")]
        public string? Specialization { get; set; } // The specialization chip selected e.g. Neurology, Dentistry, or null/empty for All/General
    }

    public class CreateCommentDto
    {
        [Required(ErrorMessage = "Comment content is required")]
        [MaxLength(1000, ErrorMessage = "Comment cannot exceed 1000 characters")]
        public string Content { get; set; } = string.Empty;
    }

    public class CommunityCommentDto
    {
        public int Id { get; set; }
        public int PostId { get; set; }
        public int UserId { get; set; }
        public string AuthorName { get; set; } = string.Empty;
        public string? AuthorProfileImageUrl { get; set; }
        public string AuthorRoleText { get; set; } = string.Empty; // "Patient" or "Doctor" or "Admin"
        public string Content { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }

    public class CommunityPostDto
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public string AuthorName { get; set; } = string.Empty;
        public string? AuthorProfileImageUrl { get; set; }
        public string AuthorRoleText { get; set; } = string.Empty; // "Patient" or "Doctor" or "Admin"
        public string? AuthorSpecialization { get; set; } // If the author is a doctor
        public string Content { get; set; } = string.Empty;
        public string? Specialization { get; set; } // Specialization category chip e.g. Neurology
        public DateTime CreatedAt { get; set; }
        public int CommentsCount { get; set; }
        public List<CommunityCommentDto> Comments { get; set; } = new List<CommunityCommentDto>();
    }
}

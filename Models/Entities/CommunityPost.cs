using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MedicalApp.API.Models.Entities
{
    public class CommunityPost : BaseEntity
    {
        [Required]
        public int UserId { get; set; }

        [ForeignKey(nameof(UserId))]
        public User User { get; set; } = null!;

        [Required]
        [MaxLength(2000)]
        public string Content { get; set; } = string.Empty;

        [MaxLength(100)]
        public string? Specialization { get; set; } // Category filter e.g. Neurology, Dentistry

        // Navigation properties
        public ICollection<CommunityComment> Comments { get; set; } = new List<CommunityComment>();
    }
}

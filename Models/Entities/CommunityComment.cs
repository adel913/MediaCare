using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MedicalApp.API.Models.Entities
{
    public class CommunityComment : BaseEntity
    {
        [Required]
        public int PostId { get; set; }

        [ForeignKey(nameof(PostId))]
        public CommunityPost Post { get; set; } = null!;

        [Required]
        public int UserId { get; set; }

        [ForeignKey(nameof(UserId))]
        public User User { get; set; } = null!;

        [Required]
        [MaxLength(1000)]
        public string Content { get; set; } = string.Empty;
    }
}

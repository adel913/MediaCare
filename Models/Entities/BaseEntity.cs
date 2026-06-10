using System.ComponentModel.DataAnnotations;

namespace MedicalApp.API.Models.Entities
{
    /// <summary>
    /// Base entity with common audit fields for all database tables.
    /// </summary>
    public abstract class BaseEntity
    {
        [Key]
        public int Id { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? UpdatedAt { get; set; }

        public bool IsDeleted { get; set; } = false;
    }
}

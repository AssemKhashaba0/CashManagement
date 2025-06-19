using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace CashManagement.Models
{
    public class FawryService
    {
        [Key]
        public int Id { get; set; }

        [Required(ErrorMessage = "اسم الخدمة مطلوب")]
        [StringLength(100)]
        public string ServiceName { get; set; }

        [StringLength(500)]
        public string Description { get; set; }

        [Range(0, 100, ErrorMessage = "نسبة الرسوم يجب أن تكون بين 0 و 100")]
        [Column(TypeName = "decimal(5,2)")]
        public decimal? FeesPercentage { get; set; }

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // العلاقات
        public virtual ICollection<FawryTransaction> FawryTransactions { get; set; } = new List<FawryTransaction>();
    }

}

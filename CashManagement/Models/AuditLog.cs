using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CashManagement.Models
{
    public class AuditLog
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(450)]
        public string UserId { get; set; }

        [Required]
        [StringLength(100)]
        public string ActionType { get; set; } // نوع الإجراء (مثل: Add, Update, Freeze, Delete)

        [Required]
        [StringLength(100)]
        public string EntityType { get; set; } // نوع الكيان (مثل: CashLine, CashTransaction)

        public int? EntityId { get; set; } // معرف الكيان (مثل: معرف الخط أو العملية)

        [StringLength(1000)]
        public string Details { get; set; } // تفاصيل الإجراء (مثل: "تم إضافة خط برقم 0123456789")

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // العلاقات
        [ForeignKey("UserId")]
        public virtual ApplicationUser User { get; set; }
    }
}
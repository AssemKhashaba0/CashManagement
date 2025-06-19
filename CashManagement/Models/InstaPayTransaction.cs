using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CashManagement.Models
{
    public class InstaPayTransaction
    {
        [Key]
        public int Id { get; set; }
        [Required]
        public int InstaPayId { get; set; }
        [Required(ErrorMessage = "المبلغ مطلوب")]
        [Range(0.01, double.MaxValue, ErrorMessage = "المبلغ يجب أن يكون أكبر من صفر")]
        [Column(TypeName = "decimal(18,2)")]
        public decimal Amount { get; set; }
        [Required(ErrorMessage = "مبلغ الرسوم مطلوب")]
        [Range(0, double.MaxValue, ErrorMessage = "مبلغ الرسوم يجب أن يكون قيمة موجبة")]
        [Column(TypeName = "decimal(18,2)")]
        public decimal FeesAmount { get; set; } // تغيير: مبلغ ثابت بالجنيه
        [Column(TypeName = "decimal(18,2)")]
        public decimal NetAmount { get; set; }
        [Required]
        public TransactionType TransactionType { get; set; }
        [StringLength(500)]
        public string Description { get; set; }
        public TransactionStatus Status { get; set; } = TransactionStatus.Completed;
        [Required]
        [StringLength(450)]
        public string UserId { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        // العلاقات
        [ForeignKey("InstaPayId")]
        public virtual InstaPay InstaPay { get; set; }
    }
}
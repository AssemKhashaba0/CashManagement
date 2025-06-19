using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace CashManagement.Models
{
    public class FawryTransaction
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int FawryServiceId { get; set; }

        [Required(ErrorMessage = "المبلغ مطلوب")]
        [Range(0.01, double.MaxValue, ErrorMessage = "المبلغ يجب أن يكون أكبر من صفر")]
        [Column(TypeName = "decimal(18,2)")]
        public decimal Amount { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal FeesAmount { get; set; } = 0;

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
        [ForeignKey("FawryServiceId")]
        public virtual FawryService FawryService { get; set; }
    }

}

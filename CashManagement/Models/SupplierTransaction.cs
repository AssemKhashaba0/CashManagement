using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace CashManagement.Models
{
    public class SupplierTransaction
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int SupplierId { get; set; }

        [Required(ErrorMessage = "المبلغ مطلوب")]
        [Range(0.01, double.MaxValue, ErrorMessage = "المبلغ يجب أن يكون أكبر من صفر")]
        [Column(TypeName = "decimal(18,2)")]
        public decimal Amount { get; set; }

        [Required]
        public DebitCreditType DebitCreditType { get; set; }
        public ApplicationUser User { get; set; }
        [StringLength(500)]
        public string Description { get; set; }

        [Required]
        [StringLength(450)]
        public string UserId { get; set; }

        public DateTime TransactionDate { get; set; } = DateTime.Now;
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // العلاقات
        [ForeignKey("SupplierId")]
        public virtual Supplier Supplier { get; set; }
    }
    public enum DebitCreditType
    {
        [Display(Name = "له")]
        Credit = 1,
        [Display(Name = "عليه")]
        Debit = 2
    }
}

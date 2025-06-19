using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CashManagement.Models
{
    public class CashTransaction
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int CashLineId { get; set; }

        [Required(ErrorMessage = "المبلغ مطلوب")]
        [Range(0.01, double.MaxValue, ErrorMessage = "المبلغ يجب أن يكون أكبر من صفر")]
        [Column(TypeName = "decimal(18,2)")]
        public decimal Amount { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal Fees { get; set; } = 0;

        [Column(TypeName = "decimal(18,2)")]
        public decimal NetAmount { get; set; }

        [Required(ErrorMessage = "نسبة العمولة مطلوبة")]
        [Range(0, 100, ErrorMessage = "نسبة العمولة يجب أن تكون بين 0 و 100")]
        [Column(TypeName = "decimal(5,2)")]
        public decimal CommissionRate { get; set; }

        [Required]
        public TransactionType TransactionType { get; set; }

        public DepositType? DepositType { get; set; }

        [StringLength(50)]
        public string RecipientNumber { get; set; }

        [StringLength(500)]
        public string Description { get; set; }

        public TransactionStatus Status { get; set; } = TransactionStatus.Completed;

        [Required]
        [StringLength(450)]
        public string UserId { get; set; }

        public PaymentType PaymentType { get; set; } = PaymentType.Cash;

        public int? SupplierId { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? FrozenUntil { get; set; } // تاريخ فك التجميد (للمبالغ المجمدة)
        [StringLength(50)]
        public string TransactionReference { get; set; } // رقم مرجعي فريد

        // العلاقات
        [ForeignKey("CashLineId")]
        public virtual CashLine CashLine { get; set; }

        [ForeignKey("UserId")]
        public virtual ApplicationUser User { get; set; }

        [ForeignKey("SupplierId")]
        public virtual Supplier Supplier { get; set; }
    }

    public enum PaymentType
    {
        [Display(Name = "نقدي")]
        Cash = 1,
        [Display(Name = "آجل")]
        Deferred = 2
    }

    public enum TransactionType
    {
        [Display(Name = "سحب")]
        Withdraw = 1,
        [Display(Name = "إيداع")]
        Deposit = 2
    }

    public enum TransactionStatus
    {
        [Display(Name = "قيد الانتظار")]
        Pending = 1,
        [Display(Name = "مكتملة")]
        Completed = 2,
        [Display(Name = "مرفوضة")]
        Rejected = 3,
        [Display(Name = "مجمدة")]
        Frozen = 4
    }

    public enum DepositType
    {
        [Display(Name = "أوتوماتيكي")]
        Automatic = 1,
        [Display(Name = "يدوي")]
        Manual = 2,
        [Display(Name = "بدون خصم")]
        NoDeduction = 3
    }
}
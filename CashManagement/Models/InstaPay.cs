using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace CashManagement.Models
{
    public class InstaPay
    {
        [Key]
        public int Id { get; set; }

        [Required(ErrorMessage = "رقم الهاتف مطلوب")]
        [StringLength(15)]
        [RegularExpression(@"^[0-9]+$", ErrorMessage = "رقم الهاتف يجب أن يحتوي على أرقام فقط")]
        public string PhoneNumber { get; set; }

        [Required(ErrorMessage = "رقم الحساب البنكي مطلوب")]
        [StringLength(50)]
        public string BankAccountNumber { get; set; }

        [Required(ErrorMessage = "اسم البنك مطلوب")]
        [StringLength(100)]
        public string BankName { get; set; }

        [Required(ErrorMessage = "الرصيد الحالي مطلوب")]
        [Range(0, double.MaxValue, ErrorMessage = "الرصيد يجب أن يكون قيمة موجبة")]
        [Column(TypeName = "decimal(18,2)")]
        public decimal CurrentBalance { get; set; }

        public AccountStatus Status { get; set; } = AccountStatus.Active;

        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime UpdatedAt { get; set; } = DateTime.Now;
        [ForeignKey("User")]
        public string? UserId { get; set; }
        public virtual ApplicationUser? User { get; set; }

        // العلاقات
        public virtual ICollection<InstaPayTransaction> InstaPayTransactions { get; set; } = new List<InstaPayTransaction>();
    }
   
    public enum AccountStatus
    {
        [Display(Name = "نشط")]
        Active = 1,
        [Display(Name = "مجمد")]
        Frozen = 2,
        [Display(Name = "محذوف")]
        Deleted = 3
    }
}

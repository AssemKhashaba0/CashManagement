using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace CashManagement.Models
{
    public class CashLine
    {
        [Key]
        public int Id { get; set; }

        [Required(ErrorMessage = "رقم الهاتف مطلوب")]
        [StringLength(15, ErrorMessage = "رقم الهاتف لا يجب أن يتجاوز 15 رقم")]
        [RegularExpression(@"^[0-9]+$", ErrorMessage = "رقم الهاتف يجب أن يحتوي على أرقام فقط")]
        public string PhoneNumber { get; set; }

        [Required(ErrorMessage = "اسم صاحب الخط مطلوب")]
        [StringLength(100, ErrorMessage = "الاسم لا يجب أن يتجاوز 100 حرف")]
        public string OwnerName { get; set; }

        [Required(ErrorMessage = "نوع الشبكة مطلوب")]
        public NetworkType NetworkType { get; set; }

        [Required(ErrorMessage = "الرصيد الحالي مطلوب")]
        [Range(0, double.MaxValue, ErrorMessage = "الرصيد يجب أن يكون قيمة موجبة")]
        [Column(TypeName = "decimal(18,2)")]
        public decimal CurrentBalance { get; set; }

        [Required(ErrorMessage = "الحد اليومي مطلوب")]
        [Range(0, double.MaxValue, ErrorMessage = "الحد اليومي يجب أن يكون قيمة موجبة")]
        [Column(TypeName = "decimal(18,2)")]
        public decimal DailyLimit { get; set; }

        [Required(ErrorMessage = "الحد الشهري مطلوب")]
        [Range(0, double.MaxValue, ErrorMessage = "الحد الشهري يجب أن يكون قيمة موجبة")]
        [Column(TypeName = "decimal(18,2)")]
        public decimal MonthlyLimit { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal DailyUsed { get; set; } = 0;

        [Column(TypeName = "decimal(18,2)")]
        public decimal MonthlyUsed { get; set; } = 0;

        public AccountStatus Status { get; set; } = AccountStatus.Active;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // **الحقول الجديدة**
        public DateTime? LastResetDate { get; set; } // آخر إعادة تعيين للحدود
       

        // العلاقات
        public virtual ICollection<CashTransaction> CashTransactions { get; set; } = new List<CashTransaction>();
    }
    public enum NetworkType
    {
        [Display(Name = "اتصالات")]
        Etisalat = 1,
        [Display(Name = "فودافون")]
        Vodafone = 2,
        [Display(Name = "أورانج")]
        Orange = 3,
        [Display(Name = "وي")]
        WE = 4
    }
}

using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace CashManagement.Models
{
    public class Supplier
    {
        [Key]
        public int Id { get; set; }

        [Required(ErrorMessage = "الاسم مطلوب")]
        [StringLength(100)]
        public string Name { get; set; }

        [Required]
        public SupplierType Type { get; set; }

        [StringLength(15)]
        [RegularExpression(@"^[0-9]+$", ErrorMessage = "رقم الهاتف يجب أن يحتوي على أرقام فقط")]
        public string PhoneNumber { get; set; }

        [StringLength(100)]
        [EmailAddress(ErrorMessage = "البريد الإلكتروني غير صحيح")]
        public string? Email { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal OpeningBalance { get; set; } = 0;

        [Column(TypeName = "decimal(18,2)")]
        public decimal CurrentBalance { get; set; } = 0;

        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime UpdatedAt { get; set; } = DateTime.Now;

        // العلاقات
        public virtual ICollection<SupplierTransaction> SupplierTransactions { get; set; } = new List<SupplierTransaction>();
    }
    public enum SupplierType
    {
        [Display(Name = "مورد")]
        Supplier = 1,
        [Display(Name = "عميل")]
        Customer = 2
    }
}

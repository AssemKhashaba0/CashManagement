using Microsoft.AspNetCore.Identity;
namespace CashManagement.Models
{
    public class ApplicationUser : IdentityUser
    {
        public string FullName { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }

        // العلاقات المرتبطة بالعمليات التي قام بها المستخدم
        public virtual ICollection<CashTransaction> CashTransactions { get; set; } = new List<CashTransaction>();
        public virtual ICollection<CashTransaction_Physical> PhysicalCashTransactions { get; set; } = new List<CashTransaction_Physical>();
        public virtual ICollection<InstaPayTransaction> InstaPayTransactions { get; set; } = new List<InstaPayTransaction>();
        public virtual ICollection<FawryTransaction> FawryTransactions { get; set; } = new List<FawryTransaction>();
        public virtual ICollection<SupplierTransaction> SupplierTransactions { get; set; } = new List<SupplierTransaction>();
    }
}
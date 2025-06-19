using CashManagement.Models;
using CashManagement.Models.CashManagement.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace CashManagement.Data
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        // تعريف DbSets لكل نموذج (Entity)
        public DbSet<CashLine> CashLines { get; set; }
        public DbSet<CashTransaction> CashTransactions { get; set; }
        public DbSet<CashTransaction_Physical> CashTransactionsPhysical { get; set; }
        public DbSet<InstaPay> InstaPays { get; set; }
        public DbSet<InstaPayTransaction> InstaPayTransactions { get; set; }
        public DbSet<FawryService> FawryServices { get; set; }
        public DbSet<FawryTransaction> FawryTransactions { get; set; }
        public DbSet<Supplier> Suppliers { get; set; }
        public DbSet<SupplierTransaction> SupplierTransactions { get; set; }
        public DbSet<DailyProfit> DailyProfits { get; set; }
        public DbSet<SystemBalance> SystemBalances { get; set; }
        public DbSet<AuditLog> AuditLogs { get; set; }
        public DbSet<FrozenAmount> FrozenAmount { get; set; }


        // يمكن إضافة إعدادات إضافية للنماذج إذا لزم الأمر
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // إعدادات إضافية للعلاقات أو القيود إذا لزم الأمر
            // على سبيل المثال، يمكن إضافة قيود على الحقول أو العلاقات هنا
        }
    }
}
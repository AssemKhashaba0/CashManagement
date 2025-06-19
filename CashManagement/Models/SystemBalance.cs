using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace CashManagement.Models
{
    public class SystemBalance
    {
        [Key]
        public int Id { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalCashLineBalance { get; set; } = 0;

        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalPhysicalCash { get; set; } = 0; // يتأثر بعمليات إنستا باي (إيداع ينقص، سحب يزيد)

        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalInstaPayBalance { get; set; } = 0; // إجمالي رصيد حسابات إنستا باي

        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalSystemBalance { get; set; } = 0; // إجمالي النظام = TotalCashLineBalance + TotalPhysicalCash + TotalInstaPayBalance

        public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
    }
}
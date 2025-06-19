using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace CashManagement.Models
{
    namespace CashManagement.Models
    {
        public class DailyProfit
        {
            [Key]
            public int Id { get; set; }

            [Required]
            public DateTime Date { get; set; }

            [Column(TypeName = "decimal(18,2)")]
            public decimal CashLineProfit { get; set; } = 0;

            [Column(TypeName = "decimal(18,2)")]
            public decimal InstaPayProfit { get; set; } = 0; // أرباح عمليات إنستا باي (من العمولات)

            [Column(TypeName = "decimal(18,2)")]
            public decimal FawryProfit { get; set; } = 0;

            [Column(TypeName = "decimal(18,2)")]
            public decimal TotalProfit { get; set; } = 0; // إجمالي الأرباح = CashLineProfit + InstaPayProfit + FawryProfit

            public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
            public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        }
    }
}

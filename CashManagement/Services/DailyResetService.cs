using CashManagement.Data;
using CashManagement.Models;
using CashManagement.Models.CashManagement.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace CashManagement.Services
{
    public class DailyResetService
    {
        private readonly ApplicationDbContext _context;

        public DailyResetService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task ResetDailyLimitsAndUnfreezeAsync()
        {
            // الحصول على جميع الخطوط التي لم تصل إلى الحد الشهري
            var eligibleLines = await _context.CashLines
                .Where(cl => cl.MonthlyUsed < cl.MonthlyLimit && cl.Status != AccountStatus.Deleted)
                .ToListAsync();

            foreach (var cashLine in eligibleLines)
            {
                // إعادة تعيين الحد اليومي
                cashLine.DailyUsed = 0;
                // فك التجميد إذا كان الخط مجمدًا
                if (cashLine.Status == AccountStatus.Frozen)
                {
                    cashLine.Status = AccountStatus.Active;
                }
                cashLine.UpdatedAt = DateTime.UtcNow;
            }

            // الحصول على المعاملات المجمدة للخطوط المؤهلة
            var frozenTransactions = await _context.CashTransactions
                .Include(t => t.CashLine)
                .Where(t => t.Status == TransactionStatus.Frozen &&
                            t.CashLine.MonthlyUsed < t.CashLine.MonthlyLimit &&
                            t.CashLine.Status != AccountStatus.Deleted)
                .ToListAsync();

            var systemBalance = await _context.SystemBalances.FirstOrDefaultAsync();

            foreach (var transaction in frozenTransactions)
            {
                var cashLine = transaction.CashLine;

                if (transaction.TransactionType == TransactionType.Withdraw)
                {
                    // تحديث رصيد الخط
                    cashLine.CurrentBalance -= transaction.Amount;
                    cashLine.DailyUsed += transaction.Amount;
                    cashLine.MonthlyUsed += transaction.Amount;

                    // تحديث رصيد النظام
                    if (systemBalance != null)
                    {
                        systemBalance.TotalPhysicalCash += transaction.NetAmount;
                        systemBalance.TotalSystemBalance += transaction.NetAmount;
                        systemBalance.LastUpdated = DateTime.UtcNow;
                    }
                }
                else if (transaction.TransactionType == TransactionType.Deposit)
                {
                    // تحديث رصيد الخط
                    cashLine.CurrentBalance += transaction.Amount;
                    cashLine.DailyUsed += transaction.Amount;
                    cashLine.MonthlyUsed += transaction.Amount;

                    // تحديث رصيد النظام
                    if (systemBalance != null)
                    {
                        systemBalance.TotalPhysicalCash -= transaction.Amount;
                        systemBalance.TotalSystemBalance -= transaction.Amount;
                        systemBalance.LastUpdated = DateTime.UtcNow;
                    }
                }

                // تحديث الأرباح
                var dailyProfit = await _context.DailyProfits
                    .FirstOrDefaultAsync(dp => dp.Date.Date == DateTime.UtcNow.Date);
                if (dailyProfit == null)
                {
                    dailyProfit = new DailyProfit { Date = DateTime.UtcNow.Date };
                    _context.DailyProfits.Add(dailyProfit);
                }
                dailyProfit.CashLineProfit += transaction.Fees;
                dailyProfit.TotalProfit += transaction.Fees;
                dailyProfit.UpdatedAt = DateTime.UtcNow;

                // فك تجميد المعاملة
                transaction.Status = TransactionStatus.Completed;
                transaction.CreatedAt = DateTime.UtcNow; // تحديث تاريخ الإتمام
            }

            await _context.SaveChangesAsync();
        }
    }
}
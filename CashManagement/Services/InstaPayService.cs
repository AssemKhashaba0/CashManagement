using CashManagement.Data;
using CashManagement.Models;
using CashManagement.Models.CashManagement.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace CashManagement.Services
{
    public class InstaPayService
    {
        private readonly ApplicationDbContext _context;

        public InstaPayService(ApplicationDbContext context)
        {
            _context = context;
        }

        // تسجيل حساب إنستا باي جديد
        public async Task<(bool Success, string Message)> AddInstaPayAccountAsync(InstaPay instaPay)
        {
            // التحقق من أن رقم الهاتف أو الحساب البنكي غير مسجل مسبقًا
            var existingAccount = await _context.InstaPays
                .FirstOrDefaultAsync(x => x.PhoneNumber == instaPay.PhoneNumber || x.BankAccountNumber == instaPay.BankAccountNumber);

            if (existingAccount != null)
            {
                return (false, "رقم الهاتف أو الحساب البنكي مسجل مسبقًا، الرجاء إدخال بيانات مختلفة.");
            }

            // إضافة الحساب
            _context.InstaPays.Add(instaPay);
            await _context.SaveChangesAsync();

            // تحديث رصيد النظام
            var systemBalance = await _context.SystemBalances.FirstOrDefaultAsync();
            if (systemBalance != null)
            {
                systemBalance.TotalInstaPayBalance += instaPay.CurrentBalance;
                systemBalance.TotalSystemBalance = systemBalance.TotalCashLineBalance + systemBalance.TotalPhysicalCash + systemBalance.TotalInstaPayBalance;
                systemBalance.LastUpdated = DateTime.UtcNow;
                await _context.SaveChangesAsync();
            }

            // تسجيل الإجراء في AuditLog
            var auditLog = new AuditLog
            {
                UserId = "ManagerId", // يجب استبداله بمعرف المدير الحقيقي من السياق
                ActionType = "Add",
                EntityType = "InstaPay",
                EntityId = instaPay.Id,
                Details = $"تم إضافة حساب إنستا باي برقم هاتف {instaPay.PhoneNumber} ورصيد {instaPay.CurrentBalance}",
                CreatedAt = DateTime.UtcNow
            };
            _context.AuditLogs.Add(auditLog);
            await _context.SaveChangesAsync();

            return (true, "تم تسجيل حساب إنستا باي بنجاح.");
        }
        public List<InstaPay> GetInstaPayAccounts()
        {
            return _context.InstaPays
                .Where(x => x.Status == AccountStatus.Active) // فقط الحسابات النشطة
                .ToList();
        }
        // تنفيذ عملية سحب/إيداع
        public async Task<(bool Success, string Message, InstaPayTransaction Transaction)> ProcessInstaPayTransactionAsync(InstaPayTransaction transaction)
        {
            var instaPayAccount = await _context.InstaPays.FindAsync(transaction.InstaPayId);
            if (instaPayAccount == null)
            {
                return (false, "حساب إنستا باي غير موجود.", null);
            }

            // التحقق من حالة الحساب
            if (instaPayAccount.Status != AccountStatus.Active)
            {
                return (false, "الحساب غير نشط.", null);
            }

            // التحقق من نسبة الرسوم (يجب أن تكون ضمن القيم المسموحة، مثل 1% أو 2%)
            if (transaction.FeesPercentage != 1 && transaction.FeesPercentage != 2)
            {
                return (false, "نسبة الرسوم غير متاحة، الرجاء اختيار نسبة أخرى (1% أو 2%).", null);
            }

            // حساب الرسوم والمبلغ النهائي
            transaction.FeesAmount = transaction.Amount * (transaction.FeesPercentage / 100);
            transaction.NetAmount = transaction.TransactionType == TransactionType.Withdraw
                ? transaction.Amount + transaction.FeesAmount // السحب: المبلغ + الرسوم
                : transaction.Amount - transaction.FeesAmount; // الإيداع: المبلغ - الرسوم

            // التحقق من الرصيد في حالة السحب
            if (transaction.TransactionType == TransactionType.Withdraw && instaPayAccount.CurrentBalance < transaction.NetAmount)
            {
                return (false, "الرصيد غير كافٍ لإتمام عملية السحب.", null);
            }

            // تحديث رصيد حساب إنستا باي
            if (transaction.TransactionType == TransactionType.Withdraw)
            {
                instaPayAccount.CurrentBalance -= transaction.NetAmount;
            }
            else // Deposit
            {
                instaPayAccount.CurrentBalance += transaction.NetAmount;
            }
            instaPayAccount.UpdatedAt = DateTime.UtcNow;

            // تحديث النقدية في النظام
            var systemBalance = await _context.SystemBalances.FirstOrDefaultAsync();
            if (systemBalance != null)
            {
                if (transaction.TransactionType == TransactionType.Withdraw)
                {
                    systemBalance.TotalPhysicalCash += transaction.NetAmount; // السحب يزيد النقدية
                }
                else // Deposit
                {
                    if (systemBalance.TotalPhysicalCash < transaction.NetAmount)
                    {
                        return (false, "النقدية غير كافية لإتمام عملية الإيداع.", null);
                    }
                    systemBalance.TotalPhysicalCash -= transaction.NetAmount; // الإيداع ينقص النقدية
                }
                systemBalance.TotalInstaPayBalance = await _context.InstaPays.SumAsync(x => x.CurrentBalance);
                systemBalance.TotalSystemBalance = systemBalance.TotalCashLineBalance + systemBalance.TotalPhysicalCash + systemBalance.TotalInstaPayBalance;
                systemBalance.LastUpdated = DateTime.UtcNow;
            }

            // تسجيل الأرباح في DailyProfit
            var dailyProfit = await _context.DailyProfits.FirstOrDefaultAsync(x => x.Date.Date == DateTime.Today);
            if (dailyProfit == null)
            {
                dailyProfit = new DailyProfit { Date = DateTime.Today };
                _context.DailyProfits.Add(dailyProfit);
            }
            dailyProfit.InstaPayProfit += transaction.FeesAmount;
            dailyProfit.TotalProfit = dailyProfit.CashLineProfit + dailyProfit.InstaPayProfit + dailyProfit.FawryProfit;
            dailyProfit.UpdatedAt = DateTime.UtcNow;

            // إضافة العملية
            transaction.Status = TransactionStatus.Completed;
            transaction.CreatedAt = DateTime.UtcNow;
            _context.InstaPayTransactions.Add(transaction);

            // تسجيل الإجراء في AuditLog
            var auditLog = new AuditLog
            {
                UserId = transaction.UserId,
                ActionType = transaction.TransactionType.ToString(),
                EntityType = "InstaPayTransaction",
                EntityId = transaction.Id,
                Details = $"تم تنفيذ عملية {transaction.TransactionType} بمبلغ {transaction.Amount} ورسوم {transaction.FeesAmount}",
                CreatedAt = DateTime.UtcNow
            };
            _context.AuditLogs.Add(auditLog);

            await _context.SaveChangesAsync();

            return (true, "تم تنفيذ العملية بنجاح.", transaction);
        }
    }
}
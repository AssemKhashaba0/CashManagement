using CashManagement.Data;
using CashManagement.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CashManagement.Controllers
{
    [Authorize]
    public class EmployeeDashboardController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public EmployeeDashboardController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // GET: EmployeeDashboard/Index
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);
            var isManager = await _userManager.IsInRoleAsync(user, "Manager");
            var today = DateTime.UtcNow.Date;

            // إنشاء نموذج عرض الداشبورد
            var model = new EmployeeDashboardViewModel
            {
                UserFullName = user.FullName,
                IsManager = isManager,
                TodayDate = today
            };

            // 1. ملخص العمليات النقدية اليوم
            var cashTransactionsQuery = _context.CashTransactions
                .Where(t => t.CreatedAt.Date == today && t.Status == TransactionStatus.Completed);
            if (!isManager)
            {
                cashTransactionsQuery = cashTransactionsQuery.Where(t => t.UserId == user.Id);
            }

            model.TotalCashTransactionsToday = await cashTransactionsQuery.CountAsync();
            model.TotalCashWithdrawalsToday = await cashTransactionsQuery
                .Where(t => t.TransactionType == TransactionType.Withdraw)
                .SumAsync(t => t.Amount);
            model.TotalCashDepositsToday = await cashTransactionsQuery
                .Where(t => t.TransactionType == TransactionType.Deposit)
                .SumAsync(t => t.Amount);
            model.TotalCashFeesToday = await cashTransactionsQuery.SumAsync(t => t.Fees);

            // 2. ملخص عمليات إنستا باي اليوم
            var instaPayTransactionsQuery = _context.InstaPayTransactions
                .Where(t => t.CreatedAt.Date == today && t.Status == TransactionStatus.Completed);
            if (!isManager)
            {
                instaPayTransactionsQuery = instaPayTransactionsQuery.Where(t => t.UserId == user.Id);
            }

            model.TotalInstaPayTransactionsToday = await instaPayTransactionsQuery.CountAsync();
            model.TotalInstaPayWithdrawalsToday = await instaPayTransactionsQuery
                .Where(t => t.TransactionType == TransactionType.Withdraw)
                .SumAsync(t => t.Amount);
            model.TotalInstaPayDepositsToday = await instaPayTransactionsQuery
                .Where(t => t.TransactionType == TransactionType.Deposit)
                .SumAsync(t => t.Amount);
            model.TotalInstaPayFeesToday = await instaPayTransactionsQuery.SumAsync(t => t.FeesAmount);

            // 3. ملخص الأرصدة
            var systemBalance = await _context.SystemBalances.FirstOrDefaultAsync();
            if (systemBalance != null)
            {
                model.TotalPhysicalCashBalance = systemBalance.TotalPhysicalCash;
                model.TotalInstaPayBalance = systemBalance.TotalInstaPayBalance;
                model.TotalSystemBalance = systemBalance.TotalSystemBalance;
            }

            // 4. أحدث 5 عمليات نقدية
            model.RecentCashTransactions = await _context.CashTransactions
                .Include(t => t.CashLine)
                .Where(t => isManager || t.UserId == user.Id)
                .OrderByDescending(t => t.CreatedAt)
                .Take(5)
                .Select(t => new CashTransactionViewModel
                {
                    Id = t.Id,
                    CashLinePhoneNumber = t.CashLine.PhoneNumber,
                    Amount = t.Amount,
                    Fees = t.Fees,
                    NetAmount = t.NetAmount,
                    TransactionType = t.TransactionType,
                    Status = t.Status,
                    CreatedAt = t.CreatedAt
                })
                .ToListAsync();

            // 5. أحدث 5 عمليات إنستا باي
            model.RecentInstaPayTransactions = await _context.InstaPayTransactions
                .Include(t => t.InstaPay)
                .Where(t => isManager || t.UserId == user.Id)
                .OrderByDescending(t => t.CreatedAt)
                .Take(5)
                .Select(t => new InstaPayTransactionViewModel
                {
                    InstaPayId = t.Id,
                    PhoneNumber = t.InstaPay.PhoneNumber,
                    Amount = t.Amount,
                    FeesAmount = t.FeesAmount,
                    TransactionType = t.TransactionType,
                    Status = t.Status,
                    CreatedAt = t.CreatedAt
                })
                .ToListAsync();

            // 6. ملخص الخطوط النقدية
            var cashLinesQuery = _context.CashLines
                .Where(cl => cl.Status == AccountStatus.Active);
            if (!isManager)
            {
                cashLinesQuery = cashLinesQuery.Where(cl => cl.CashTransactions.Any(t => t.UserId == user.Id));
            }

            model.ActiveCashLinesCount = await cashLinesQuery.CountAsync();
            model.TotalCashLineBalance = await cashLinesQuery.SumAsync(cl => cl.CurrentBalance);
            model.CashLines = await cashLinesQuery
                .Select(cl => new CashLineViewModel
                {
                    Id = cl.Id,
                    PhoneNumber = cl.PhoneNumber,
                    OwnerName = cl.OwnerName,
                    CurrentBalance = cl.CurrentBalance,
                    DailyLimit = cl.DailyLimit,
                    MonthlyLimit = cl.MonthlyLimit,
                    DailyUsed = cl.DailyUsed,
                    MonthlyUsed = cl.MonthlyUsed,
                    Status = cl.Status,
                    DailyRemainingPercentage = (cl.DailyLimit > 0) ? (1 - (cl.DailyUsed / cl.DailyLimit)) * 100 : 0,
                    MonthlyRemainingPercentage = (cl.MonthlyLimit > 0) ? (1 - (cl.MonthlyUsed / cl.MonthlyLimit)) * 100 : 0
                })
                .Take(5)
                .ToListAsync();

            return View(model);
        }
    }

    // ViewModel للداشبورد
    public class EmployeeDashboardViewModel
    {
        public string UserFullName { get; set; }
        public bool IsManager { get; set; }
        public DateTime TodayDate { get; set; }

        // ملخص العمليات النقدية اليوم
        public int TotalCashTransactionsToday { get; set; }
        public decimal TotalCashWithdrawalsToday { get; set; }
        public decimal TotalCashDepositsToday { get; set; }
        public decimal TotalCashFeesToday { get; set; }

        // ملخص عمليات إنستا باي اليوم
        public int TotalInstaPayTransactionsToday { get; set; }
        public decimal TotalInstaPayWithdrawalsToday { get; set; }
        public decimal TotalInstaPayDepositsToday { get; set; }
        public decimal TotalInstaPayFeesToday { get; set; }

        // ملخص الأرصدة
        public decimal TotalPhysicalCashBalance { get; set; }
        public decimal TotalInstaPayBalance { get; set; }
        public decimal TotalSystemBalance { get; set; }

        // أحدث العمليات
        public List<CashTransactionViewModel> RecentCashTransactions { get; set; }
        public List<InstaPayTransactionViewModel> RecentInstaPayTransactions { get; set; }

        // ملخص الخطوط النقدية
        public int ActiveCashLinesCount { get; set; }
        public decimal TotalCashLineBalance { get; set; }
        public List<CashLineViewModel> CashLines { get; set; }
    }
}
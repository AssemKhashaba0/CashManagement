using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CashManagement.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using OfficeOpenXml;
using System.IO;
using CashManagement.Data;
using CashManagement.Models.CashManagement.Models;

namespace CashManagement.Controllers
{
    [Authorize]
    public class CashTransactionsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public CashTransactionsController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // GET: CashTransactions/Index
        [HttpGet]
        public async Task<IActionResult> Index(string searchString, DateTime? startDate, DateTime? endDate, TransactionType? type, TransactionStatus? status, int? cashLineId)
        {
            var query = _context.CashTransactions
                .Include(t => t.CashLine)
                .Include(t => t.User)
                .AsQueryable();

            if (!string.IsNullOrEmpty(searchString))
            {
                query = query.Where(t => t.RecipientNumber.Contains(searchString) || t.Description.Contains(searchString));
            }
            if (startDate.HasValue)
            {
                query = query.Where(t => t.CreatedAt >= startDate.Value);
            }
            if (endDate.HasValue)
            {
                query = query.Where(t => t.CreatedAt <= endDate.Value.AddDays(1));
            }
            if (type.HasValue)
            {
                query = query.Where(t => t.TransactionType == type.Value);
            }
            if (status.HasValue)
            {
                query = query.Where(t => t.Status == status.Value);
            }
            if (cashLineId.HasValue)
            {
                query = query.Where(t => t.CashLineId == cashLineId.Value);
            }

            var transactions = await query.Select(t => new CashTransactionViewModel
            {
                Id = t.Id,
                CashLineId = t.CashLineId,
                CashLinePhoneNumber = t.CashLine.PhoneNumber,
                Amount = t.Amount,
                Fees = t.Fees,
                NetAmount = t.NetAmount,
                CommissionRate = t.CommissionRate,
                TransactionType = t.TransactionType,
                DepositType = t.DepositType,
                RecipientNumber = t.RecipientNumber,
                Description = t.Description,
                Status = t.Status,
                UserId = t.UserId,
                UserName = t.User.FullName,
                CreatedAt = t.CreatedAt
            }).ToListAsync();

            ViewBag.CashLines = await _context.CashLines.Select(cl => new { cl.Id, cl.PhoneNumber }).ToListAsync();
            return View(transactions);
        }
        [HttpGet]
        public async Task<IActionResult> Withdraw(int cashLineId)
        {
            var cashLine = await _context.CashLines.FindAsync(cashLineId);
            if (cashLine == null || cashLine.Status != AccountStatus.Active)
            {
                TempData["ErrorMessage"] = "الخط غير موجود أو غير نشط.";
                return RedirectToAction(nameof(Index));
            }

            var model = new CashTransaction
            {
                CashLineId = cashLineId,
                TransactionType = TransactionType.Withdraw
            };
            ViewBag.CashLine = cashLine;
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Withdraw(CashTransaction transaction)
        {
            if (ModelState.IsValid)
            {
                ViewBag.CashLine = await _context.CashLines.FindAsync(transaction.CashLineId);
                return View(transaction);
            }

            var cashLine = await _context.CashLines.FindAsync(transaction.CashLineId);
            if (cashLine == null || cashLine.Status != AccountStatus.Active)
            {
                TempData["ErrorMessage"] = "الخط غير موجود أو غير نشط.";
                return RedirectToAction(nameof(Index));
            }

            // التحقق من الرصيد والحدود
            if (cashLine.CurrentBalance < transaction.Amount)
            {
                ModelState.AddModelError("Amount", "الرصيد غير كافٍ.");
                ViewBag.CashLine = cashLine;
                return View(transaction);
            }

            var dailyRemaining = cashLine.DailyLimit - cashLine.DailyUsed;
            var monthlyRemaining = cashLine.MonthlyLimit - cashLine.MonthlyUsed;
            bool willFreeze = dailyRemaining < transaction.Amount || monthlyRemaining < transaction.Amount;

            // حساب الرسوم
            transaction.Fees = transaction.Amount * (transaction.CommissionRate / 100);
            transaction.NetAmount = transaction.Amount - transaction.Fees;
            transaction.UserId = _userManager.GetUserId(User);
            transaction.CreatedAt = DateTime.UtcNow;
            transaction.Status = willFreeze ? TransactionStatus.Frozen : TransactionStatus.Completed;
            transaction.TransactionReference = Guid.NewGuid().ToString();

            if (!willFreeze)
            {
                // تحديث رصيد الخط والنقدي
                cashLine.CurrentBalance -= transaction.Amount;
                cashLine.DailyUsed += transaction.Amount;
                cashLine.MonthlyUsed += transaction.Amount;
                cashLine.UpdatedAt = DateTime.UtcNow;

                var systemBalance = await _context.SystemBalances.FirstOrDefaultAsync();
                if (systemBalance != null)
                {
                    systemBalance.TotalPhysicalCash += transaction.NetAmount;
                    systemBalance.TotalSystemBalance += transaction.NetAmount;
                    systemBalance.LastUpdated = DateTime.UtcNow;
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
            }
            else
            {
                // تجميد الخط إذا تجاوز الحد
                cashLine.Status = AccountStatus.Frozen;
                cashLine.UpdatedAt = DateTime.UtcNow;
            }

            _context.CashTransactions.Add(transaction);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = willFreeze
                ? "تم تسجيل عملية السحب كمجمدة بسبب تجاوز الحد، وسيتم فك تجميدها تلقائيًا عند إعادة تعيين الحدود."
                : "تم إجراء عملية السحب بنجاح.";
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> Deposit(int cashLineId)
        {
            var cashLine = await _context.CashLines.FindAsync(cashLineId);
            if (cashLine == null || cashLine.Status != AccountStatus.Active)
            {
                TempData["ErrorMessage"] = "الخط غير موجود أو غير نشط.";
                return RedirectToAction(nameof(Index));
            }

            var model = new CashTransaction
            {
                CashLineId = cashLineId,
                TransactionType = TransactionType.Deposit
            };
            ViewBag.CashLine = cashLine;
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Deposit(CashTransaction transaction)
        {
            if (ModelState.IsValid)
            {
                ViewBag.CashLine = await _context.CashLines.FindAsync(transaction.CashLineId);
                return View(transaction);
            }

            var cashLine = await _context.CashLines.FindAsync(transaction.CashLineId);
            if (cashLine == null || cashLine.Status != AccountStatus.Active)
            {
                TempData["ErrorMessage"] = "الخط غير موجود أو غير نشط.";
                return RedirectToAction(nameof(Index));
            }

            // التحقق من رصيد النقدي
            var systemBalance = await _context.SystemBalances.FirstOrDefaultAsync();
            if (systemBalance == null || systemBalance.TotalPhysicalCash < transaction.Amount)
            {
                ModelState.AddModelError("Amount", "رصيد النقدي غير كافٍ.");
                ViewBag.CashLine = cashLine;
                return View(transaction);
            }

            // التحقق من الحدود اليومية والشهرية
            var dailyRemaining = cashLine.DailyLimit - cashLine.DailyUsed;
            var monthlyRemaining = cashLine.MonthlyLimit - cashLine.MonthlyUsed;
            bool willFreeze = dailyRemaining < transaction.Amount || monthlyRemaining < transaction.Amount;

            // التحقق من صحة نسبة الرسوم
            if (transaction.DepositType != DepositType.NoDeduction && transaction.CommissionRate <= 0)
            {
                ModelState.AddModelError("CommissionRate", "نسبة الرسوم يجب أن تكون أكبر من صفر.");
                ViewBag.CashLine = cashLine;
                return View(transaction);
            }

            // حساب الرسوم
            transaction.Fees = (transaction.DepositType == DepositType.NoDeduction) ? 0 : transaction.Amount * (transaction.CommissionRate / 100);
            transaction.NetAmount = transaction.Amount + transaction.Fees;
            transaction.UserId = _userManager.GetUserId(User);
            transaction.CreatedAt = DateTime.UtcNow;
            transaction.Status = willFreeze ? TransactionStatus.Frozen : TransactionStatus.Completed;
            transaction.TransactionReference = Guid.NewGuid().ToString();

            if (!willFreeze)
            {
                // تحديث رصيد الخط والنقدي
                cashLine.CurrentBalance += transaction.Amount;
                cashLine.DailyUsed += transaction.Amount;
                cashLine.MonthlyUsed += transaction.Amount;
                cashLine.UpdatedAt = DateTime.UtcNow;

                if (systemBalance != null)
                {
                    systemBalance.TotalPhysicalCash -= transaction.Amount;
                    systemBalance.TotalSystemBalance -= transaction.Amount;
                    systemBalance.LastUpdated = DateTime.UtcNow;
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
            }
            else
            {
                // تجميد الخط إذا تجاوز الحد
                cashLine.Status = AccountStatus.Frozen;
                cashLine.UpdatedAt = DateTime.UtcNow;
            }

            _context.CashTransactions.Add(transaction);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = willFreeze
                ? "تم تسجيل عملية الإيداع كمجمدة بسبب تجاوز الحد، وسيتم فك تجميدها تلقائيًا عند إعادة تعيين الحدود."
                : "تم إجراء عملية الإيداع بنجاح.";
            return RedirectToAction(nameof(Index));
        }
        // GET: CashTransactions/FrozenAmounts
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ConfirmFreeze(CashTransaction transaction)
        {
            var cashLine = await _context.CashLines.FindAsync(transaction.CashLineId);
            if (cashLine == null)
            {
                return NotFound("الخط غير موجود.");
            }

            // إضافة المبلغ المجمد إلى جدول FrozenAmounts (مقترح)
            // ملاحظة: يحتاج إلى نموذج FrozenAmounts جديد
            transaction.Status = TransactionStatus.Frozen;
            transaction.UserId = _userManager.GetUserId(User);
            transaction.CreatedAt = DateTime.UtcNow;
            // transaction.FrozenUntil = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month + 1, 1); // فك التجميد الشهر القادم
            transaction.Fees = transaction.Amount * (transaction.CommissionRate / 100);
            transaction.NetAmount = transaction.Amount - transaction.Fees;

            _context.CashTransactions.Add(transaction);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "تم تجميد المبلغ للشهر التالي.";
            return RedirectToAction(nameof(Index));
        }
        [HttpGet]
        public async Task<IActionResult> FrozenAmounts()
        {
            var frozenTransactions = await _context.CashTransactions
                .Where(t => t.Status == TransactionStatus.Frozen)
                .Include(t => t.CashLine)
                .Select(t => new CashTransactionViewModel
                {
                    Id = t.Id,
                    CashLineId = t.CashLineId,
                    CashLinePhoneNumber = t.CashLine.PhoneNumber,
                    Amount = t.Amount,
                    Fees = t.Fees,
                    NetAmount = t.NetAmount,
                    TransactionType = t.TransactionType,
                    Status = t.Status,
                    CreatedAt = t.CreatedAt
                    // ملاحظة: يمكن إضافة FrozenUntil لو تم تحديث النموذج
                })
                .ToListAsync();

            return View(frozenTransactions);
        }

        // GET: CashTransactions/Details/5
        [HttpGet]
        public async Task<IActionResult> Details(int id)
        {
            var transaction = await _context.CashTransactions
                .Include(t => t.CashLine)
                .Include(t => t.User)
                .Select(t => new CashTransactionViewModel
                {
                    Id = t.Id,
                    CashLineId = t.CashLineId,
                    CashLinePhoneNumber = t.CashLine.PhoneNumber,
                    Amount = t.Amount,
                    Fees = t.Fees,
                    NetAmount = t.NetAmount,
                    CommissionRate = t.CommissionRate,
                    TransactionType = t.TransactionType,
                    DepositType = t.DepositType,
                    RecipientNumber = t.RecipientNumber,
                    Description = t.Description,
                    Status = t.Status,
                    UserId = t.UserId,
                    UserName = t.User.FullName,
                    CreatedAt = t.CreatedAt
                })
                .FirstOrDefaultAsync(t => t.Id == id);

            if (transaction == null)
            {
                return NotFound("العملية غير موجودة.");
            }

            return View(transaction);
        }

        // POST: CashTransactions/ExportTransactions

        [HttpPost]
        public async Task<IActionResult> ExportTransactions(DateTime? startDate, DateTime? endDate, int? cashLineId)
        {
            // **هذا هو السطر الجديد الذي يجب إضافته هنا**
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

            var query = _context.CashTransactions
                .Include(t => t.CashLine)
                .Include(t => t.User)
                .AsQueryable();

            if (startDate.HasValue)
            {
                query = query.Where(t => t.CreatedAt >= startDate.Value);
            }
            if (endDate.HasValue)
            {
                query = query.Where(t => t.CreatedAt <= endDate.Value.AddDays(1));
            }
            if (cashLineId.HasValue)
            {
                query = query.Where(t => t.CashLineId == cashLineId.Value);
            }

            var transactions = await query.ToListAsync();

            using (var package = new ExcelPackage())
            {
                var worksheet = package.Workbook.Worksheets.Add("Transactions");
                worksheet.Cells[1, 1].Value = "رقم العملية";
                worksheet.Cells[1, 2].Value = "رقم الخط";
                worksheet.Cells[1, 3].Value = "المبلغ";
                worksheet.Cells[1, 4].Value = "الرسوم";
                worksheet.Cells[1, 5].Value = "المبلغ الصافي";
                worksheet.Cells[1, 6].Value = "نوع العملية";
                worksheet.Cells[1, 7].Value = "الحالة";
                worksheet.Cells[1, 8].Value = "التاريخ";

                for (int i = 0; i < transactions.Count; i++)
                {
                    worksheet.Cells[i + 2, 1].Value = transactions[i].Id;
                    worksheet.Cells[i + 2, 2].Value = transactions[i].CashLine.PhoneNumber;
                    worksheet.Cells[i + 2, 3].Value = transactions[i].Amount;
                    worksheet.Cells[i + 2, 4].Value = transactions[i].Fees;
                    worksheet.Cells[i + 2, 5].Value = transactions[i].NetAmount;
                    worksheet.Cells[i + 2, 6].Value = transactions[i].TransactionType.ToString();
                    worksheet.Cells[i + 2, 7].Value = transactions[i].Status.ToString();
                    worksheet.Cells[i + 2, 8].Value = transactions[i].CreatedAt.ToString("yyyy-MM-dd HH:mm:ss");
                }

                var stream = new MemoryStream();
                package.SaveAs(stream);
                stream.Position = 0;
                string excelName = $"Transactions_{DateTime.UtcNow:yyyyMMdd}.xlsx";
                return File(stream, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", excelName);
            }
        }
            // وظيفة إعادة تعيين الحدود الشهرية (ممكن تكون مجدولة)

            [HttpPost]
        public async Task<IActionResult> ResetMonthlyLimits()
        {
            var cashLines = await _context.CashLines.ToListAsync();
            foreach (var cashLine in cashLines)
            {
                cashLine.MonthlyUsed = 0;
                cashLine.UpdatedAt = DateTime.UtcNow;
                // فك تجميد المبالغ (يتطلب جدول FrozenAmounts)
            }

            var frozenTransactions = await _context.CashTransactions
                .Where(t => t.Status == TransactionStatus.Frozen)
                .ToListAsync();
            foreach (var transaction in frozenTransactions)
            {
                transaction.Status = TransactionStatus.Completed;
                // تحديث الأرصدة هنا لو تم فك التجميد
            }

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "تم إعادة تعيين الحدود الشهرية بنجاح.";
            return RedirectToAction(nameof(Index));
        }

        // GET: CashTransactions/Dashboard
        [HttpGet]
        public async Task<IActionResult> TransactionsDashboard()
        {
            var today = DateTime.UtcNow.Date;
            var transactions = await _context.CashTransactions
                .Where(t => t.CreatedAt.Date == today)
                .Include(t => t.CashLine)
                .GroupBy(t => t.CashLineId)
                .Select(g => new
                {
                    CashLineId = g.Key,
                    PhoneNumber = g.First().CashLine.PhoneNumber,
                    TotalWithdrawals = g.Where(t => t.TransactionType == TransactionType.Withdraw).Sum(t => t.Amount),
                    TotalDeposits = g.Where(t => t.TransactionType == TransactionType.Deposit).Sum(t => t.Amount),
                    TotalFees = g.Sum(t => t.Fees)
                })
                .ToListAsync();

            var cashLines = await _context.CashLines
                .Select(cl => new CashLineViewModel
                {
                    Id = cl.Id,
                    PhoneNumber = cl.PhoneNumber,
                    CurrentBalance = cl.CurrentBalance,
                    DailyLimit = cl.DailyLimit,
                    MonthlyLimit = cl.MonthlyLimit,
                    DailyUsed = cl.DailyUsed,
                    MonthlyUsed = cl.MonthlyUsed,
                    Status = cl.Status,
                    DailyRemainingPercentage = (cl.DailyLimit > 0) ? (1 - (cl.DailyUsed / cl.DailyLimit)) * 100 : 0,
                    MonthlyRemainingPercentage = (cl.MonthlyLimit > 0) ? (1 - (cl.MonthlyUsed / cl.MonthlyLimit)) * 100 : 0
                })
                .ToListAsync();

            var model = new TransactionsDashboardViewModel
            {
                TotalTransactions = transactions.Count,
                TotalWithdrawals = transactions.Sum(t => t.TotalWithdrawals),
                TotalDeposits = transactions.Sum(t => t.TotalDeposits),
                TotalFees = transactions.Sum(t => t.TotalFees),
                CashLines = cashLines,
                ActiveLinesCount = cashLines.Count(cl => cl.Status == AccountStatus.Active),
                FrozenLinesCount = cashLines.Count(cl => cl.Status == AccountStatus.Frozen)
            };

            return View(model);
        }
    }

    // ViewModel لعرض العمليات
    public class CashTransactionViewModel
    {
        public int Id { get; set; }
        public int CashLineId { get; set; }
        public string CashLinePhoneNumber { get; set; }
        public decimal Amount { get; set; }
        public decimal Fees { get; set; }
        public decimal NetAmount { get; set; }
        public decimal CommissionRate { get; set; }
        public TransactionType TransactionType { get; set; }
        public DepositType? DepositType { get; set; }
        public string RecipientNumber { get; set; }
        public string Description { get; set; }
        public TransactionStatus Status { get; set; }
        public string UserId { get; set; }
        public string UserName { get; set; }
        public DateTime CreatedAt { get; set; }
    }   

    // ViewModel للوحة التحكم
    public class TransactionsDashboardViewModel
    {
        public int TotalTransactions { get; set; }
        public decimal TotalWithdrawals { get; set; }
        public decimal TotalDeposits { get; set; }
        public decimal TotalFees { get; set; }
        public int ActiveLinesCount { get; set; }
        public int FrozenLinesCount { get; set; }
        public List<CashLineViewModel> CashLines { get; set; }
    }
}
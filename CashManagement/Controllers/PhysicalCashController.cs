using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CashManagement.Models;
using Microsoft.AspNetCore.Identity;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using CashManagement.Data;

namespace CashManagement.Controllers
{
    public class PhysicalCashController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public PhysicalCashController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // GET: PhysicalCash/Index
        [HttpGet]
        public async Task<IActionResult> Index(string searchString, DateTime? startDate, DateTime? endDate, TransactionType? type, TransactionStatus? status)
        {
            var user = await _userManager.GetUserAsync(User);
            var isManager = await _userManager.IsInRoleAsync(user, "Admin");

            var query = _context.CashTransactionsPhysical.AsQueryable();

            // تصفية حسب البحث
            if (!string.IsNullOrEmpty(searchString))
            {
                query = query.Where(t => t.Description.Contains(searchString));
            }

            // تصفية حسب التاريخ
            if (startDate.HasValue)
            {
                query = query.Where(t => t.CreatedAt >= startDate.Value);
            }
            if (endDate.HasValue)
            {
                query = query.Where(t => t.CreatedAt <= endDate.Value.AddDays(1));
            }

            // تصفية حسب نوع العملية
            if (type.HasValue)
            {
                query = query.Where(t => t.TransactionType == type.Value);
            }

            // إذا لم يكن مديرًا، عرض العمليات الخاصة بالمستخدم فقط
            if (!isManager)
            {
                query = query.Where(t => t.UserId == user.Id);
            }

            var transactions = await query
                .Select(t => new CashTransactionPhysicalViewModel
                {
                    Id = t.Id,
                    Amount = t.Amount,
                    TransactionType = t.TransactionType,
                    Description = t.Description,
                    UserName = t.UserId,
                    CreatedAt = t.CreatedAt
                })
                .ToListAsync();

            // تمرير بيانات الفلترة إلى العرض
            ViewBag.SearchString = searchString;
            ViewBag.StartDate = startDate;
            ViewBag.EndDate = endDate;
            ViewBag.TransactionType = type;
            ViewBag.Status = status;

            return View(transactions);
        }

        // GET: PhysicalCash/Deposit
        [HttpGet]
        public IActionResult Deposit()
        {
            return View();
        }

        // POST: PhysicalCash/Deposit
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Deposit(CashTransactionPhysicalViewModel model)
        {
            if (ModelState.IsValid)
            {
                return View(model);
            }

            // التحقق من أن المبلغ صالح
            if (model.Amount <= 0)
            {
                TempData["ErrorMessage"] = "المبلغ يجب أن يكون أكبر من صفر.";
                return View(model);
            }

            var user = await _userManager.GetUserAsync(User);
            var transaction = new CashTransaction_Physical
            {
                Amount = model.Amount,
                TransactionType = TransactionType.Deposit,
                Description = model.Description,
                UserId = user.Id,
                CreatedAt = DateTime.UtcNow,
            };

            // تحديث الرصيد النقدي في SystemBalance
            var systemBalance = await _context.SystemBalances.FirstOrDefaultAsync();
            if (systemBalance == null)
            {
                systemBalance = new SystemBalance
                {
                    TotalPhysicalCash = 0,
                    LastUpdated = DateTime.UtcNow
                };
                _context.SystemBalances.Add(systemBalance);
            }

            systemBalance.TotalPhysicalCash += model.Amount;
            systemBalance.TotalSystemBalance = systemBalance.TotalPhysicalCash + systemBalance.TotalCashLineBalance + systemBalance.TotalInstaPayBalance;
            systemBalance.LastUpdated = DateTime.UtcNow;

            _context.CashTransactionsPhysical.Add(transaction);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "تم تسجيل عملية الإيداع بنجاح.";
            return RedirectToAction(nameof(Index));
        }

        // GET: PhysicalCash/Withdraw
        [HttpGet]
        public IActionResult Withdraw()
        {
            return View();
        }

        // POST: PhysicalCash/Withdraw
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Withdraw(CashTransactionPhysicalViewModel model)
        {
            if (ModelState.IsValid)
            {
                return View(model);
            }

            // التحقق من أن المبلغ صالح
            if (model.Amount <= 0)
            {
                TempData["ErrorMessage"] = "المبلغ يجب أن يكون أكبر من صفر.";
                return View(model);
            }

            // التحقق من كفاية الرصيد النقدي
            var systemBalance = await _context.SystemBalances.FirstOrDefaultAsync();
            if (systemBalance == null || systemBalance.TotalPhysicalCash < model.Amount)
            {
                TempData["ErrorMessage"] = "الرصيد النقدي غير كافٍ لإتمام عملية السحب.";
                return View(model);
            }

            var user = await _userManager.GetUserAsync(User);
            var transaction = new CashTransaction_Physical
            {
                Amount = model.Amount,
                TransactionType = TransactionType.Withdraw,
                Description = model.Description,
                UserId = user.Id,
                CreatedAt = DateTime.UtcNow
            };

            // تحديث الرصيد النقدي
            systemBalance.TotalPhysicalCash -= model.Amount;
            systemBalance.TotalSystemBalance = systemBalance.TotalPhysicalCash + systemBalance.TotalCashLineBalance + systemBalance.TotalInstaPayBalance;
            systemBalance.LastUpdated = DateTime.UtcNow;

            _context.CashTransactionsPhysical.Add(transaction);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "تم تسجيل عملية السحب بنجاح.";
            return RedirectToAction(nameof(Index));
        }

        // GET: PhysicalCash/Details/5
        [HttpGet]
        public async Task<IActionResult> Details(int id)
        {
            var transaction = await _context.CashTransactionsPhysical
                .Include(t => t.User)
                .Select(t => new CashTransactionPhysicalViewModel
                {
                    Id = t.Id,
                    Amount = t.Amount,
                    TransactionType = t.TransactionType,
                    Description = t.Description,
                    UserName = t.User.FullName,
                    CreatedAt = t.CreatedAt
                })
                .FirstOrDefaultAsync(t => t.Id == id);

            if (transaction == null)
            {
                return NotFound();
            }

            return View(transaction);
        }

        // GET: PhysicalCash/Edit/5
        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var transaction = await _context.CashTransactionsPhysical
                .Select(t => new CashTransactionPhysicalViewModel
                {
                    Id = t.Id,
                    Amount = t.Amount,
                    TransactionType = t.TransactionType,
                    Description = t.Description,
                    UserName = t.UserId,
                    CreatedAt = t.CreatedAt
                })
                .FirstOrDefaultAsync(t => t.Id == id);

            if (transaction == null)
            {
                return NotFound();
            }

            return View(transaction);
        }

        // POST: PhysicalCash/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, CashTransactionPhysicalViewModel model)
        {
            if (id != model.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                return View(model);
            }

            // التحقق من أن المبلغ صالح
            if (model.Amount <= 0)
            {
                TempData["ErrorMessage"] = "المبلغ يجب أن يكون أكبر من صفر.";
                return View(model);
            }

            var transaction = await _context.CashTransactionsPhysical.FindAsync(id);
            if (transaction == null)
            {
                return NotFound();
            }

            // التحقق من كفاية الرصيد إذا كانت عملية سحب
            var systemBalance = await _context.SystemBalances.FirstOrDefaultAsync();
            if (transaction.TransactionType == TransactionType.Withdraw && (systemBalance == null || systemBalance.TotalPhysicalCash + transaction.Amount < model.Amount))
            {
                TempData["ErrorMessage"] = "الرصيد النقدي غير كافٍ لإتمام عملية السحب.";
                return View(model);
            }

            // حساب الفرق في المبلغ لتحديث الرصيد
            var amountDifference = model.Amount - transaction.Amount;

            // تحديث بيانات العملية
            transaction.Amount = model.Amount;
            transaction.Description = model.Description;

            // تحديث الرصيد النقدي في SystemBalance
            if (systemBalance != null)
            {
                if (transaction.TransactionType == TransactionType.Deposit)
                {
                    systemBalance.TotalPhysicalCash += amountDifference;
                }
                else if (transaction.TransactionType == TransactionType.Withdraw)
                {
                    systemBalance.TotalPhysicalCash -= amountDifference;
                }
                systemBalance.TotalSystemBalance = systemBalance.TotalPhysicalCash + systemBalance.TotalCashLineBalance + systemBalance.TotalInstaPayBalance;
                systemBalance.LastUpdated = DateTime.UtcNow;
            }

            try
            {
                _context.Update(transaction);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "تم تعديل العملية النقدية بنجاح.";
                return RedirectToAction(nameof(Index));
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!await _context.CashTransactionsPhysical.AnyAsync(t => t.Id == id))
                {
                    return NotFound();
                }
                throw;
            }
        }
    }

    // ViewModel للعمليات النقدية
    public class CashTransactionPhysicalViewModel
    {
        public int Id { get; set; }
        public decimal Amount { get; set; }
        public TransactionType TransactionType { get; set; }
        public string Description { get; set; }
        public TransactionStatus Status { get; set; }
        public string UserName { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CashManagement.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using CashManagement.Data;

namespace CashManagement.Controllers
{
    [Authorize]
    public class CashLinesController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public CashLinesController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // GET: CashLines/Index
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);
            var isManager = await _userManager.IsInRoleAsync(user, "Manager");

            var query = _context.CashLines.AsQueryable();
            if (!isManager)
            {
                query = query.Where(cl => cl.Status == AccountStatus.Active &&
                                         cl.DailyUsed < cl.DailyLimit &&
                                         cl.MonthlyUsed < cl.MonthlyLimit);
            }

            var cashLines = await query.Select(cl => new CashLineViewModel
            {
                Id = cl.Id,
                PhoneNumber = cl.PhoneNumber,
                OwnerName = cl.OwnerName,
                NetworkType = cl.NetworkType,
                CurrentBalance = cl.CurrentBalance,
                DailyLimit = cl.DailyLimit,
                MonthlyLimit = cl.MonthlyLimit,
                DailyUsed = cl.DailyUsed,
                MonthlyUsed = cl.MonthlyUsed,
                Status = cl.Status,
                DailyRemainingPercentage = (cl.DailyLimit > 0) ? (1 - (cl.DailyUsed / cl.DailyLimit)) * 100 : 0,
                MonthlyRemainingPercentage = (cl.MonthlyLimit > 0) ? (1 - (cl.MonthlyUsed / cl.MonthlyLimit)) * 100 : 0
            }).ToListAsync();

            return View(cashLines);
        }

        // GET: CashLines/Create
        //
        [HttpGet]
        public IActionResult Create()
        {
            return View();
        }

        // POST: CashLines/Create
        
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CashLine cashLine)
        {
            if (!ModelState.IsValid)
            {
                return View(cashLine);
            }

            // التحقق من أن رقم الهاتف غير مسجل
            if (await _context.CashLines.AnyAsync(cl => cl.PhoneNumber == cashLine.PhoneNumber))
            {
                ModelState.AddModelError("PhoneNumber", "رقم الهاتف مسجل مسبقًا.");
                return View(cashLine);
            }

            // التحقق من أن الحد الشهري أكبر من أو يساوي الحد اليومي
            if (cashLine.MonthlyLimit < cashLine.DailyLimit)
            {
                ModelState.AddModelError("MonthlyLimit", "الحد الشهري يجب أن يكون أكبر من أو يساوي الحد اليومي.");
                return View(cashLine);
            }

            cashLine.CreatedAt = DateTime.UtcNow;
            cashLine.UpdatedAt = DateTime.UtcNow;

            // تحديث رصيد النقدي العام
            var systemBalance = await _context.SystemBalances.FirstOrDefaultAsync();
            if (systemBalance != null && cashLine.CurrentBalance > 0)
            {
                systemBalance.TotalPhysicalCash -= cashLine.CurrentBalance;
                systemBalance.TotalSystemBalance -= cashLine.CurrentBalance;
                systemBalance.LastUpdated = DateTime.UtcNow;
            }

            _context.CashLines.Add(cashLine);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "تم إضافة الخط بنجاح.";
            return RedirectToAction(nameof(Index));
        }

        // GET: CashLines/Edit/5
        
        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var cashLine = await _context.CashLines.FindAsync(id);
            if (cashLine == null)
            {
                return NotFound("الخط غير موجود.");
            }
            return View(cashLine);
        }

        // POST: CashLines/Edit/5
        
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, CashLine cashLine)
        {
            if (id != cashLine.Id)
            {
                return BadRequest("معرف الخط غير متطابق.");
            }

            if (!ModelState.IsValid)
            {
                return View(cashLine);
            }

            var existingCashLine = await _context.CashLines.FindAsync(id);
            if (existingCashLine == null)
            {
                return NotFound("الخط غير موجود.");
            }

            // التحقق من رقم الهاتف
            if (cashLine.PhoneNumber != existingCashLine.PhoneNumber &&
                await _context.CashLines.AnyAsync(cl => cl.PhoneNumber == cashLine.PhoneNumber))
            {
                ModelState.AddModelError("PhoneNumber", "رقم الهاتف مسجل لخط آخر.");
                return View(cashLine);
            }

            // التحقق من الحدود
            if (cashLine.MonthlyLimit < cashLine.DailyLimit)
            {
                ModelState.AddModelError("MonthlyLimit", "الحد الشهري يجب أن يكون أكبر من أو يساوي الحد اليومي.");
                return View(cashLine);
            }

            // تحديث رصيد النقدي لو تغير الرصيد
            var systemBalance = await _context.SystemBalances.FirstOrDefaultAsync();
            if (systemBalance != null)
            {
                systemBalance.TotalPhysicalCash -= (cashLine.CurrentBalance - existingCashLine.CurrentBalance);
                systemBalance.TotalSystemBalance -= (cashLine.CurrentBalance - existingCashLine.CurrentBalance);
                systemBalance.LastUpdated = DateTime.UtcNow;
            }

            existingCashLine.PhoneNumber = cashLine.PhoneNumber;
            existingCashLine.OwnerName = cashLine.OwnerName;
            existingCashLine.NetworkType = cashLine.NetworkType;
            existingCashLine.CurrentBalance = cashLine.CurrentBalance;
            existingCashLine.DailyLimit = cashLine.DailyLimit;
            existingCashLine.MonthlyLimit = cashLine.MonthlyLimit;
            existingCashLine.Status = cashLine.Status;
            existingCashLine.UpdatedAt = DateTime.UtcNow;

            _context.Entry(existingCashLine).State = EntityState.Modified;
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "تم تعديل الخط بنجاح.";
            return RedirectToAction(nameof(Index));
        }

        // POST: CashLines/Delete/5
        
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var cashLine = await _context.CashLines.FindAsync(id);
            if (cashLine == null)
            {
                return NotFound("الخط غير موجود.");
            }

            // تحديث رصيد النقدي
            var systemBalance = await _context.SystemBalances.FirstOrDefaultAsync();
            if (systemBalance != null && cashLine.CurrentBalance > 0)
            {
                systemBalance.TotalPhysicalCash += cashLine.CurrentBalance;
                systemBalance.TotalSystemBalance += cashLine.CurrentBalance;
                systemBalance.LastUpdated = DateTime.UtcNow;
            }

            cashLine.Status = AccountStatus.Deleted;
            cashLine.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "تم حذف الخط بنجاح.";
            return RedirectToAction(nameof(Index));
        }

        // POST: CashLines/Freeze/5
        
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Freeze(int id)
        {
            var cashLine = await _context.CashLines.FindAsync(id);
            if (cashLine == null)
            {
                return NotFoundcourses(cashLine);
            }

            if (cashLine.Status == AccountStatus.Frozen)
            {
                TempData["ErrorMessage"] = "الخط مجمد بالفعل.";
                return RedirectToAction(nameof(Index));
            }

            cashLine.Status = AccountStatus.Frozen;
            cashLine.UpdatedAt = DateTime.UtcNow;
            // ملاحظة: يمكن إضافة حقل FreezeReason هنا لو تم تحديث النموذج
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "تم تجميد الخط بنجاح.";
            return RedirectToAction(nameof(Index));
        }

        private IActionResult NotFoundcourses(CashLine? cashLine)
        {
            throw new NotImplementedException();
        }

        // POST: CashLines/Unfreeze/5
        
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Unfreeze(int id)
        {
            var cashLine = await _context.CashLines.FindAsync(id);
            if (cashLine == null)
            {
                return NotFound("الخط غير موجود.");
            }

            if (cashLine.Status == AccountStatus.Active)
            {
                TempData["ErrorMessage"] = "الخط نشط بالفعل.";
                return RedirectToAction(nameof(Index));
            }

            cashLine.Status = AccountStatus.Active;
            cashLine.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "تم تفعيل الخط بنجاح.";
            return RedirectToAction(nameof(Index));
        }

        // GET: CashLines/Details/5
        [HttpGet]
        public async Task<IActionResult> Details(int id)
        {
            var cashLine = await _context.CashLines
                .Include(cl => cl.CashTransactions)
                .Select(cl => new CashLineViewModel
                {
                    Id = cl.Id,
                    PhoneNumber = cl.PhoneNumber,
                    OwnerName = cl.OwnerName,
                    NetworkType = cl.NetworkType,
                    CurrentBalance = cl.CurrentBalance,
                    DailyLimit = cl.DailyLimit,
                    MonthlyLimit = cl.MonthlyLimit,
                    DailyUsed = cl.DailyUsed,
                    MonthlyUsed = cl.MonthlyUsed,
                    Status = cl.Status,
                    DailyRemainingPercentage = (cl.DailyLimit > 0) ? (1 - (cl.DailyUsed / cl.DailyLimit)) * 100 : 0,
                    MonthlyRemainingPercentage = (cl.MonthlyLimit > 0) ? (1 - (cl.MonthlyUsed / cl.MonthlyLimit)) * 100 : 0,
                    Transactions = cl.CashTransactions.Select(t => new CashTransactionViewModel
                    {
                        Id = t.Id,
                        Amount = t.Amount,
                        Fees = t.Fees,
                        NetAmount = t.NetAmount,
                        TransactionType = t.TransactionType,
                        Status = t.Status,
                        CreatedAt = t.CreatedAt
                    }).ToList()
                })
                .FirstOrDefaultAsync(cl => cl.Id == id);

            if (cashLine == null)
            {
                return NotFound("الخط غير موجود.");
            }

            return View(cashLine);
        }

        // POST: CashLines/SearchAvailableLines
        [HttpPost]
        public async Task<IActionResult> SearchAvailableLines(decimal amount)
        {
            var cashLines = await _context.CashLines
                .Where(cl => cl.Status == AccountStatus.Active &&
                             cl.CurrentBalance >= amount &&
                             (cl.DailyLimit - cl.DailyUsed) >= amount &&
                             (cl.MonthlyLimit - cl.MonthlyUsed) >= amount)
                .Select(cl => new CashLineViewModel
                {
                    Id = cl.Id,
                    PhoneNumber = cl.PhoneNumber,
                    OwnerName = cl.OwnerName,
                    CurrentBalance = cl.CurrentBalance,
                    DailyRemainingPercentage = (cl.DailyLimit > 0) ? (1 - (cl.DailyUsed / cl.DailyLimit)) * 100 : 0,
                    MonthlyRemainingPercentage = (cl.MonthlyLimit > 0) ? (1 - (cl.MonthlyUsed / cl.MonthlyLimit)) * 100 : 0
                })
                .ToListAsync();

            return View(cashLines);
        }

        // وظيفة إعادة تعيين الحدود اليومية (ممكن تكون مجدولة)

        [HttpPost]
        public async Task<IActionResult> ResetDailyLimits()
        {
            var eligibleLines = await _context.CashLines
                .Where(cl => cl.MonthlyUsed < cl.MonthlyLimit && cl.Status != AccountStatus.Deleted)
                .ToListAsync();

            foreach (var cashLine in eligibleLines)
            {
                cashLine.DailyUsed = 0;
                if (cashLine.Status == AccountStatus.Frozen)
                {
                    cashLine.Status = AccountStatus.Active;
                }
                cashLine.UpdatedAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "تم إعادة تعيين الحدود اليومية بنجاح للخطوط المؤهلة.";
            return RedirectToAction("Index");
        }
    }

    // ViewModel لعرض الخطوط
    public class CashLineViewModel
    {
        public int Id { get; set; }
        public string PhoneNumber { get; set; }
        public string OwnerName { get; set; }
        public NetworkType NetworkType { get; set; }
        public decimal CurrentBalance { get; set; }
        public decimal DailyLimit { get; set; }
        public decimal MonthlyLimit { get; set; }
        public decimal DailyUsed { get; set; }
        public decimal MonthlyUsed { get; set; }
        public AccountStatus Status { get; set; }
        public decimal DailyRemainingPercentage { get; set; }
        public decimal MonthlyRemainingPercentage { get; set; }
        public List<CashTransactionViewModel> Transactions { get; set; }
    }
}
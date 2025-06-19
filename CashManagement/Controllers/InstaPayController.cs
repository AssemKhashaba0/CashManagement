using CashManagement.Data;
using CashManagement.Models;
using CashManagement.Models.CashManagement.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace CashManagement.Controllers
{
    [Authorize]
    public class InstaPayController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public InstaPayController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // عرض قائمة حسابات إنستا باي
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var instaPayAccounts = await _context.InstaPays
                .Where(ip => ip.Status != AccountStatus.Deleted)
                .ToListAsync();
            return View(instaPayAccounts);
        }

        // عرض نموذج إضافة حساب إنستا باي جديد (للمدير فقط)
        [HttpGet]
        //[Authorize(Roles = "Manager")]
        public IActionResult Create()
        {
            return View();
        }

        // إضافة حساب إنستا باي جديد
        [HttpPost]
        //[Authorize(Roles = "Manager")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(InstaPay model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            // التحقق من عدم تكرار رقم الهاتف أو الحساب البنكي
            if (await _context.InstaPays.AnyAsync(ip => ip.PhoneNumber == model.PhoneNumber || ip.BankAccountNumber == model.BankAccountNumber))
            {
                ModelState.AddModelError("", "رقم الهاتف أو الحساب البنكي مسجل مسبقًا.");
                return View(model);
            }

            var user = await _userManager.GetUserAsync(User);
            model.UserId = user.Id;
            model.CreatedAt = DateTime.UtcNow;
            model.UpdatedAt = DateTime.UtcNow;

            _context.InstaPays.Add(model);
            await _context.SaveChangesAsync();

            // تسجيل نشاط في AuditLog
            await LogAudit(user.Id, "Add", "InstaPay", model.Id, $"تم إضافة حساب إنستا باي برقم هاتف {model.PhoneNumber}");

            // تحديث رصيد النظام
            await UpdateSystemBalance();

            TempData["Success"] = "تم إضافة حساب إنستا باي بنجاح.";
            return RedirectToAction(nameof(Index));
        }

        // عرض نموذج إجراء عملية سحب أو إيداع
        [HttpGet]
        public async Task<IActionResult> Transaction(int id)
        {
            var instaPay = await _context.InstaPays.FindAsync(id);
            if (instaPay == null || instaPay.Status == AccountStatus.Deleted)
            {
                return NotFound();
            }

            var model = new InstaPayTransactionViewModel
            {
                InstaPayId = id,
                PhoneNumber = instaPay.PhoneNumber
            };

            return View(model);
        }

        // تنفيذ عملية سحب أو إيداع
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Transaction(InstaPayTransactionViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var instaPay = await _context.InstaPays.FindAsync(model.InstaPayId);
            if (instaPay == null || instaPay.Status != AccountStatus.Active)
            {
                ModelState.AddModelError("", "حساب إنستا باي غير متاح أو مجمد.");
                return View(model);
            }

            var user = await _userManager.GetUserAsync(User);
            var transaction = new InstaPayTransaction
            {
                InstaPayId = model.InstaPayId,
                Amount = model.Amount,
                FeesAmount = model.FeesAmount, // استخدام مبلغ الرسوم المدخل مباشرة
                TransactionType = model.TransactionType,
                Description = model.Description,
                UserId = user.Id,
                CreatedAt = DateTime.UtcNow,
                Status = TransactionStatus.Pending
            };

            // حساب المبلغ النهائي
            transaction.NetAmount = model.TransactionType == TransactionType.Withdraw
                ? model.Amount + model.FeesAmount // السحب: المبلغ + الرسوم
                : model.Amount - model.FeesAmount; // الإيداع: المبلغ - الرسوم

            // التحقق من أن المبلغ النهائي صالح (غير سالب)
            if (transaction.NetAmount < 0)
            {
                ModelState.AddModelError("", "مبلغ الرسوم أكبر من المبلغ الأساسي في عملية الإيداع.");
                return View(model);
            }

            // التحقق من الرصيد للسحب
            if (model.TransactionType == TransactionType.Withdraw && instaPay.CurrentBalance < transaction.NetAmount)
            {
                ModelState.AddModelError("", "الرصيد غير كافٍ لإجراء عملية السحب.");
                return View(model);
            }

            // تحديث رصيد حساب إنستا باي
            if (model.TransactionType == TransactionType.Withdraw)
            {
                instaPay.CurrentBalance -= transaction.NetAmount;
            }
            else
            {
                instaPay.CurrentBalance += transaction.NetAmount;
            }
            instaPay.UpdatedAt = DateTime.UtcNow;

            transaction.Status = TransactionStatus.Completed;
            _context.InstaPayTransactions.Add(transaction);
            _context.InstaPays.Update(instaPay);
            await _context.SaveChangesAsync();

            // تحديث الأرباح اليومية
            await UpdateDailyProfit(transaction.FeesAmount);

            // تحديث رصيد النظام
            await UpdateSystemBalance();

            // تسجيل نشاط في AuditLog
            await LogAudit(user.Id, model.TransactionType.ToString(), "InstaPayTransaction", transaction.Id,
                $"تم تنفيذ عملية {model.TransactionType} بمبلغ {model.Amount} ورسوم {model.FeesAmount} على حساب إنستا باي {instaPay.PhoneNumber}");

            TempData["Success"] = "تم تنفيذ العملية بنجاح.";
            return RedirectToAction(nameof(Index));
        }

        // عرض تفاصيل حساب إنستا باي
        [HttpGet]
        public async Task<IActionResult> Details(int id)
        {
            var instaPay = await _context.InstaPays
                .Include(ip => ip.InstaPayTransactions)
                .FirstOrDefaultAsync(ip => ip.Id == id);

            if (instaPay == null)
            {
                return NotFound();
            }

            return View(instaPay);
        }

        // البحث عن حسابات إنستا باي قادرة على استقبال مبلغ معين
        [HttpGet]
        public async Task<IActionResult> SearchAccounts(decimal amount)
        {
            var accounts = await _context.InstaPays
                .Where(ip => ip.Status == AccountStatus.Active && ip.CurrentBalance >= amount)
                .ToListAsync();

            return View(accounts);
        }
        // عرض جميع العمليات مع التصفح
        [HttpGet]
        public async Task<IActionResult> Transactions(int page = 1, int pageSize = 10, TransactionType? transactionType = null, DateTime? filterDate = null)
        {
            var query = _context.InstaPayTransactions
                .Include(t => t.InstaPay)
                .AsQueryable();

            if (transactionType.HasValue)
            {
                query = query.Where(t => t.TransactionType == transactionType);
            }

            if (filterDate.HasValue)
            {
                var startDate = filterDate.Value.Date;
                var endDate = startDate.AddDays(1);
                query = query.Where(t => t.CreatedAt >= startDate && t.CreatedAt < endDate);
            }

            var totalItems = await query.CountAsync();
            var totalPages = (int)Math.Ceiling((double)totalItems / pageSize);

            var transactions = await query
                .OrderByDescending(t => t.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var model = new PagedTransactionsViewModel
            {
                Transactions = transactions,
                CurrentPage = page,
                TotalPages = totalPages,
                PageSize = pageSize,
                TotalItems = totalItems,
                FilterTransactionType = transactionType,
                FilterDate = filterDate
            };

            return View(model);
        }

        // مساعدة: تسجيل نشاط في AuditLog
        private async Task LogAudit(string userId, string actionType, string entityType, int? entityId, string details)
        {
            var auditLog = new AuditLog
            {
                UserId = userId,
                ActionType = actionType,
                EntityType = entityType,
                EntityId = entityId,
                Details = details,
                CreatedAt = DateTime.UtcNow
            };
            _context.AuditLogs.Add(auditLog);
            await _context.SaveChangesAsync();
        }

        // مساعدة: تحديث الأرباح اليومية
        private async Task UpdateDailyProfit(decimal fees)
        {
            var today = DateTime.UtcNow.Date;
            var dailyProfit = await _context.DailyProfits
                .FirstOrDefaultAsync(dp => dp.Date == today);

            if (dailyProfit == null)
            {
                dailyProfit = new DailyProfit
                {
                    Date = today,
                    CreatedAt = DateTime.UtcNow
                };
                _context.DailyProfits.Add(dailyProfit);
                await _context.SaveChangesAsync(); // حفظ الكيان الجديد للحصول على قيمة Id
            }

            dailyProfit.InstaPayProfit += fees;
            dailyProfit.TotalProfit = dailyProfit.CashLineProfit + dailyProfit.InstaPayProfit + dailyProfit.FawryProfit;
            dailyProfit.UpdatedAt = DateTime.UtcNow;

            _context.DailyProfits.Update(dailyProfit);
            await _context.SaveChangesAsync();
        }
        // مساعدة: تحديث رصيد النظام
        private async Task UpdateSystemBalance()
        {
            var systemBalance = await _context.SystemBalances.FirstOrDefaultAsync();
            if (systemBalance == null)
            {
                systemBalance = new SystemBalance();
                _context.SystemBalances.Add(systemBalance);
            }

            systemBalance.TotalInstaPayBalance = await _context.InstaPays
                .Where(ip => ip.Status == AccountStatus.Active)
                .SumAsync(ip => ip.CurrentBalance);

            systemBalance.TotalSystemBalance = systemBalance.TotalCashLineBalance + systemBalance.TotalPhysicalCash + systemBalance.TotalInstaPayBalance;
            systemBalance.LastUpdated = DateTime.UtcNow;

            _context.SystemBalances.Update(systemBalance);
            await _context.SaveChangesAsync();
        }

    }
    public class InstaPayTransactionViewModel
    {
        public int InstaPayId { get; set; }
        public string PhoneNumber { get; set; }
        [Required(ErrorMessage = "المبلغ مطلوب")]
        [Range(0.01, double.MaxValue, ErrorMessage = "المبلغ يجب أن يكون أكبر من صفر")]
        public decimal Amount { get; set; }
        [Required(ErrorMessage = "مبلغ الرسوم مطلوب")]
        [Range(0, double.MaxValue, ErrorMessage = "مبلغ الرسوم يجب أن يكون قيمة موجبة")]
        public decimal FeesAmount { get; set; } // تغيير: مبلغ ثابت بالجنيه
        [Required(ErrorMessage = "نوع العملية مطلوب")]
        public TransactionType TransactionType { get; set; }
        [StringLength(500)]
        public string Description { get; set; }
                public TransactionStatus Status { get; set; } = TransactionStatus.Completed;
        public DateTime CreatedAt { get; set; } = DateTime.Now;

    }
    public class PagedTransactionsViewModel
    {
        public List<InstaPayTransaction> Transactions { get; set; }
        public int CurrentPage { get; set; }
        public int TotalPages { get; set; }
        public int PageSize { get; set; }
        public int TotalItems { get; set; }
        public TransactionType? FilterTransactionType { get; set; }
        public DateTime? FilterDate { get; set; }
    }
}
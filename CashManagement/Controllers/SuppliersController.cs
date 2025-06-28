using CashManagement.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.IO;
using System.ComponentModel.DataAnnotations;
using CashManagement.Data;
using System.Drawing.Printing;

namespace CashManagement.Controllers
{
    //[Authorize(Roles = "Admin")] // يقتصر الوصول على المدير
    public class SuppliersController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public SuppliersController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }
        // **عرض داش بورد احترافي للموردين/العملاء**
        public async Task<IActionResult> Dashboard(SupplierType? type)
        {
            var suppliersQuery = _context.Suppliers
                .Include(s => s.SupplierTransactions)
                .AsQueryable();

            // تصفية حسب النوع (مورد/عميل) إذا تم تحديده
            if (type.HasValue)
            {
                suppliersQuery = suppliersQuery.Where(s => s.Type == type.Value);
            }

            // جمع البيانات للإحصائيات
            var suppliers = await suppliersQuery.ToListAsync();
            var totalSuppliers = suppliers.Count;
            var totalCredit = suppliers.Where(s => s.CurrentBalance > 0).Sum(s => s.CurrentBalance);
            var totalDebit = suppliers.Where(s => s.CurrentBalance < 0).Sum(s => Math.Abs(s.CurrentBalance));
            var latestTransactions = await _context.SupplierTransactions
                .Include(t => t.Supplier)
                .Include(t => t.User)
                .OrderByDescending(t => t.TransactionDate)
                .Take(5)
                .Select(t => new SupplierTransactionViewModel
                {
                    Id = t.Id,
                    SupplierName = t.Supplier.Name,
                    Amount = t.Amount,
                    DebitCreditType = t.DebitCreditType,
                    TransactionDate = t.TransactionDate,
                    Description = t.Description,
                    UserName = t.User.UserName
                })
                .ToListAsync();

            // إعداد البيانات للمخططات
            var creditSuppliers = suppliers.Count(s => s.CurrentBalance > 0);
            var debitSuppliers = suppliers.Count(s => s.CurrentBalance < 0);
            var zeroBalanceSuppliers = suppliers.Count(s => s.CurrentBalance == 0);

            // إعداد ViewModel
            var model = new SupplierDashboardViewModel
            {
                TotalSuppliers = totalSuppliers,
                TotalCredit = totalCredit,
                TotalDebit = totalDebit,
                CreditSuppliersCount = creditSuppliers,
                DebitSuppliersCount = debitSuppliers,
                ZeroBalanceSuppliersCount = zeroBalanceSuppliers,
                LatestTransactions = latestTransactions,
                SupplierType = type
            };

            ViewBag.SupplierType = type;
            return View(model);
        }
        // **عرض قائمة الموردين/العملاء مع ترقيم الصفحات**
        public async Task<IActionResult> Index(string searchString, SupplierType? type, int page = 1, int pageSize = 10)
        {
            var suppliers = _context.Suppliers
                .Include(s => s.SupplierTransactions)
                .AsQueryable();

            // تصفية حسب الاسم أو رقم الهاتف
            if (!string.IsNullOrEmpty(searchString))
            {
                suppliers = suppliers.Where(s => s.Name.Contains(searchString) || (s.PhoneNumber != null && s.PhoneNumber.Contains(searchString)));
            }

            // تصفية حسب النوع (مورد/عميل)
            if (type.HasValue)
            {
                suppliers = suppliers.Where(s => s.Type == type.Value);
            }

            // حساب صافي الرصيد لكل مورد/عميل
            var supplierList = await suppliers
                .Select(s => new SupplierViewModel
                {
                    Id = s.Id,
                    Name = s.Name,
                    Type = s.Type,
                    PhoneNumber = s.PhoneNumber,
                    CurrentBalance = s.CurrentBalance,
                    CreditTotal = s.SupplierTransactions
                        .Where(t => t.DebitCreditType == DebitCreditType.Credit)
                        .Sum(t => t.Amount),
                    DebitTotal = s.SupplierTransactions
                        .Where(t => t.DebitCreditType == DebitCreditType.Debit)
                        .Sum(t => t.Amount),
                    NetBalance = s.CurrentBalance >= 0 ? $"له {s.CurrentBalance:N2}" : $"عليه {Math.Abs(s.CurrentBalance):N2}"
                })
                .ToListAsync();

            // حساب إجمالي "لي بره" و"عليا"
            var totalCredit = supplierList.Where(s => s.CurrentBalance > 0).Sum(s => s.CurrentBalance);
            var totalDebit = supplierList.Where(s => s.CurrentBalance < 0).Sum(s => Math.Abs(s.CurrentBalance));

            // ترقيم الصفحات
            int totalItems = supplierList.Count;
            var pagedSuppliers = supplierList
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            ViewBag.SearchString = searchString;
            ViewBag.SupplierType = type;
            ViewBag.Page = page;
            ViewBag.PageSize = pageSize;
            ViewBag.TotalPages = (int)Math.Ceiling((double)totalItems / pageSize);
            ViewBag.TotalCredit = totalCredit;
            ViewBag.TotalDebit = totalDebit;

            return View(pagedSuppliers);
        }
        // **إضافة مورد/عميل جديد - عرض النموذج**
        [HttpGet]
        public IActionResult Create()
        {
            return View();
        }

        // **إضافة مورد/عميل جديد - معالجة البيانات**
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Supplier supplier)
        {
            if (ModelState.IsValid)
            {
                // التحقق من عدم تكرار الاسم أو رقم الهاتف
                if (await _context.Suppliers.AnyAsync(s => s.Name == supplier.Name || (s.PhoneNumber != null && s.PhoneNumber == supplier.PhoneNumber)))
                {
                    ModelState.AddModelError("", "الاسم أو رقم الهاتف مسجل مسبقًا.");
                    return View(supplier);
                }

                supplier.CreatedAt = DateTime.UtcNow;
                supplier.UpdatedAt = DateTime.UtcNow;
                supplier.CurrentBalance = supplier.OpeningBalance;

                _context.Suppliers.Add(supplier);
                await _context.SaveChangesAsync();

                // تسجيل النشاط في AuditLog
                var user = await _userManager.GetUserAsync(User);
                await LogActivity(user.Id, "Add", nameof(Supplier), supplier.Id, $"تم إضافة {supplier.Type}: {supplier.Name} برصيد افتتاحي {supplier.OpeningBalance:N2}");

                TempData["Success"] = "تم إضافة المورد/العميل بنجاح.";
                return RedirectToAction(nameof(Index));
            }

            return View(supplier);
        }

        // **عرض تفاصيل مورد/عميل مع المعاملات**
        public async Task<IActionResult> Details(int id, DateTime? startDate, DateTime? endDate, DebitCreditType? debitCreditType)
        {
            var supplier = await _context.Suppliers
                .Include(s => s.SupplierTransactions)
                .ThenInclude(t => t.User)
                .FirstOrDefaultAsync(s => s.Id == id);

            if (supplier == null)
            {
                return NotFound();
            }

            var transactionsQuery = supplier.SupplierTransactions.AsQueryable();

            // تصفية المعاملات حسب التاريخ
            if (startDate.HasValue)
                transactionsQuery = transactionsQuery.Where(t => t.TransactionDate >= startDate.Value);
            if (endDate.HasValue)
                transactionsQuery = transactionsQuery.Where(t => t.TransactionDate <= endDate.Value);

            // تصفية حسب نوع المعاملة (له/عليه)
            if (debitCreditType.HasValue)
                transactionsQuery = transactionsQuery.Where(t => t.DebitCreditType == debitCreditType.Value);

            var model = new SupplierDetailsViewModel
            {
                Supplier = supplier,
                Transactions = transactionsQuery.OrderByDescending(t => t.TransactionDate).ToList(),
                CreditTotal = supplier.SupplierTransactions
                    .Where(t => t.DebitCreditType == DebitCreditType.Credit)
                    .Sum(t => t.Amount),
                DebitTotal = supplier.SupplierTransactions
                    .Where(t => t.DebitCreditType == DebitCreditType.Debit)
                    .Sum(t => t.Amount),
                NetBalance = supplier.CurrentBalance >= 0 ? $"له {supplier.CurrentBalance:N2}" : $"عليه {Math.Abs(supplier.CurrentBalance):N2}",
                StartDate = startDate,
                EndDate = endDate,
                DebitCreditType = debitCreditType
            };

            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> CreateTransaction(int supplierId)
        {
            var supplier = await _context.Suppliers.FindAsync(supplierId);
            if (supplier == null)
            {
                return NotFound();
            }

            var model = new SupplierTransaction
            {
                SupplierId = supplierId,
                TransactionDate = DateTime.UtcNow // استخدام UtcNow للتوافق مع التوقيت العالمي
            };

            ViewBag.SupplierName = supplier.Name;
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateTransaction(SupplierTransaction transaction)
        {
            if (ModelState.IsValid)
            {
                var invalidSupplier = await _context.Suppliers.FindAsync(transaction.SupplierId);
                ViewBag.SupplierName = invalidSupplier?.Name ?? "غير معروف";
                return View(transaction);
            }

            var supplier = await _context.Suppliers.FindAsync(transaction.SupplierId);
            if (supplier == null)
            {
                return NotFound();
            }

            // التحقق من صحة المبلغ
            if (transaction.Amount <= 0)
            {
                ModelState.AddModelError("Amount", "المبلغ يجب أن يكون أكبر من صفر.");
                ViewBag.SupplierName = supplier.Name;
                return View(transaction);
            }

            // التحقق من وجود المستخدم
            var userId = _userManager.GetUserId(User);
            if (string.IsNullOrEmpty(userId))
            {
                ModelState.AddModelError("", "تعذر العثور على المستخدم الحالي.");
                ViewBag.SupplierName = supplier.Name;
                return View(transaction);
            }

            transaction.CreatedAt = DateTime.UtcNow;
            transaction.UserId = userId;

            // تحديث رصيد المورد/العميل
            if (transaction.DebitCreditType == DebitCreditType.Credit)
            {
                supplier.CurrentBalance += transaction.Amount; // له
            }
            else
            {
                supplier.CurrentBalance -= transaction.Amount; // عليه
            }

            supplier.UpdatedAt = DateTime.UtcNow;

            // حفظ التغييرات
            try
            {
                _context.SupplierTransactions.Add(transaction);
                _context.Suppliers.Update(supplier);
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                ModelState.AddModelError("", "حدث خطأ أثناء حفظ المعاملة. حاول مرة أخرى.");
                ViewBag.SupplierName = supplier.Name;
                return View(transaction);
            }

            // تسجيل النشاط في AuditLog
            await LogActivity(userId, "Add", nameof(SupplierTransaction), transaction.Id,
                $"تم تسجيل معاملة {transaction.DebitCreditType} بمبلغ {transaction.Amount:N2} لـ{supplier.Name}");

            TempData["Success"] = "تم تسجيل المعاملة بنجاح.";
            return RedirectToAction(nameof(Details), new { id = transaction.SupplierId });
        }


        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var supplier = await _context.Suppliers.FindAsync(id);
            if (supplier == null)
            {
                return NotFound();
            }

            return View(supplier);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(Supplier supplier)
        {
            if (ModelState.IsValid)
            {
                return View(supplier);
            }

            var existingSupplier = await _context.Suppliers.FindAsync(supplier.Id);
            if (existingSupplier == null)
            {
                return NotFound();
            }

            // التحقق من عدم تكرار الاسم أو رقم الهاتف (باستثناء المورد الحالي)
            if (await _context.Suppliers.AnyAsync(s => (s.Name == supplier.Name || (s.PhoneNumber != null && s.PhoneNumber == supplier.PhoneNumber)) && s.Id != supplier.Id))
            {
                ModelState.AddModelError("", "الاسم أو رقم الهاتف مسجل مسبقًا.");
                return View(supplier);
            }

            // تحديث البيانات
            existingSupplier.Name = supplier.Name;
            existingSupplier.PhoneNumber = supplier.PhoneNumber;
            existingSupplier.Type = supplier.Type;
            existingSupplier.OpeningBalance = supplier.OpeningBalance;
            existingSupplier.UpdatedAt = DateTime.UtcNow;

            try
            {
                _context.Suppliers.Update(existingSupplier);
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                ModelState.AddModelError("", "حدث خطأ أثناء حفظ التعديلات. حاول مرة أخرى.");
                return View(supplier);
            }

            // تسجيل النشاط في AuditLog
            var userId = _userManager.GetUserId(User);
            await LogActivity(userId, "Edit", nameof(Supplier), supplier.Id,
                $"تم تعديل بيانات {supplier.Type}: {supplier.Name}");

            TempData["Success"] = "تم تعديل بيانات المورد/العميل بنجاح.";
            return RedirectToAction(nameof(Index));
        }
        // **عرض تقرير دفتر الحسابات**
        public async Task<IActionResult> Report(DateTime? startDate, DateTime? endDate, SupplierType? type)
        {
            var query = _context.Suppliers
                .Include(s => s.SupplierTransactions)
                .AsQueryable();

            // تصفية حسب النوع
            if (type.HasValue)
            {
                query = query.Where(s => s.Type == type.Value);
            }

            // تصفية حسب الفترة الزمنية
            if (startDate.HasValue && endDate.HasValue)
            {
                query = query.Where(s => s.SupplierTransactions.Any(t => t.TransactionDate >= startDate && t.TransactionDate <= endDate));
            }

            var suppliers = await query
                .Select(s => new SupplierViewModel
                {
                    Id = s.Id,
                    Name = s.Name,
                    Type = s.Type,
                    PhoneNumber = s.PhoneNumber,
                    CurrentBalance = s.CurrentBalance,
                    CreditTotal = s.SupplierTransactions
                        .Where(t => t.DebitCreditType == DebitCreditType.Credit)
                        .Sum(t => t.Amount),
                    DebitTotal = s.SupplierTransactions
                        .Where(t => t.DebitCreditType == DebitCreditType.Debit)
                        .Sum(t => t.Amount),
                    NetBalance = s.CurrentBalance >= 0 ? $"له {s.CurrentBalance:N2}" : $"عليه {Math.Abs(s.CurrentBalance):N2}"
                })
                .ToListAsync();

            // إذا لم يتم العثور على بيانات
            if (!suppliers.Any() && (startDate.HasValue || endDate.HasValue))
            {
                TempData["Error"] = "لا توجد معاملات في الفترة المحددة.";
            }

            ViewBag.StartDate = startDate;
            ViewBag.EndDate = endDate;
            ViewBag.SupplierType = type;

            return View(suppliers);
        }
        [HttpGet]
        public async Task<IActionResult> EditTransaction(int transactionId)
        {
            var transaction = await _context.SupplierTransactions
                .Include(t => t.Supplier)
                .FirstOrDefaultAsync(t => t.Id == transactionId);

            if (transaction == null)
            {
                return NotFound();
            }

            ViewBag.SupplierName = transaction.Supplier.Name;
            return View(transaction);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditTransaction(SupplierTransaction transaction)
        {
            if (ModelState.IsValid)
            {
                var invalidSupplier = await _context.Suppliers.FindAsync(transaction.SupplierId);
                ViewBag.SupplierName = invalidSupplier?.Name ?? "غير معروف";
                return View(transaction);
            }

            var existingTransaction = await _context.SupplierTransactions
                .Include(t => t.Supplier)
                .FirstOrDefaultAsync(t => t.Id == transaction.Id);

            if (existingTransaction == null)
            {
                return NotFound();
            }

            var supplier = await _context.Suppliers.FindAsync(transaction.SupplierId);
            if (supplier == null)
            {
                return NotFound();
            }

            // التحقق من صحة المبلغ
            if (transaction.Amount <= 0)
            {
                ModelState.AddModelError("Amount", "المبلغ يجب أن يكون أكبر من صفر.");
                ViewBag.SupplierName = supplier.Name;
                return View(transaction);
            }

            // التحقق من وجود المستخدم
            var userId = _userManager.GetUserId(User);
            if (string.IsNullOrEmpty(userId))
            {
                ModelState.AddModelError("", "تعذر العثور على المستخدم الحالي.");
                ViewBag.SupplierName = supplier.Name;
                return View(transaction);
            }

            // إعادة ضبط رصيد المورد بناءً على المعاملة القديمة
            if (existingTransaction.DebitCreditType == DebitCreditType.Credit)
            {
                supplier.CurrentBalance -= existingTransaction.Amount; // إلغاء تأثير "له"
            }
            else
            {
                supplier.CurrentBalance += existingTransaction.Amount; // إلغاء تأثير "عليه"
            }

            // تطبيق التعديلات الجديدة
            existingTransaction.Amount = transaction.Amount;
            existingTransaction.DebitCreditType = transaction.DebitCreditType;
            existingTransaction.TransactionDate = transaction.TransactionDate;
            existingTransaction.Description = transaction.Description;
            existingTransaction.UserId = userId;

            if (transaction.DebitCreditType == DebitCreditType.Credit)
            {
                supplier.CurrentBalance += transaction.Amount; // إضافة "له"
            }
            else
            {
                supplier.CurrentBalance -= transaction.Amount; // إضافة "عليه"
            }

            supplier.UpdatedAt = DateTime.UtcNow;

            // حفظ التغييرات
            try
            {
                _context.SupplierTransactions.Update(existingTransaction);
                _context.Suppliers.Update(supplier);
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                ModelState.AddModelError("", "حدث خطأ أثناء حفظ التعديلات. حاول مرة أخرى.");
                ViewBag.SupplierName = supplier.Name;
                return View(transaction);
            }

            // تسجيل النشاط في AuditLog
            await LogActivity(userId, "Edit", nameof(SupplierTransaction), transaction.Id,
                $"تم تعديل معاملة {transaction.DebitCreditType} بمبلغ {transaction.Amount:N2} لـ{supplier.Name}");

            TempData["Success"] = "تم تعديل المعاملة بنجاح.";
            return RedirectToAction(nameof(Details), new { id = transaction.SupplierId });
        }
       
        // **دالة مساعدة لتسجيل الأنشطة في AuditLog**
        private async Task LogActivity(string userId, string actionType, string entityType, int entityId, string details)
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
    }

    // **ViewModel لعرض قائمة الموردين/العملاء**
   
}
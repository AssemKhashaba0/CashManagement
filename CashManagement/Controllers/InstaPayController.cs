using CashManagement.Models;
using CashManagement.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.Threading.Tasks;

namespace CashManagement.Controllers
{
    [Authorize] // التأكد من أن المستخدم مسجل الدخول
    public class InstaPayController : Controller
    {
        private readonly InstaPayService _instaPayService;

        public InstaPayController(InstaPayService instaPayService)
        {
            _instaPayService = instaPayService;
        }

        // عرض صفحة إدارة إنستا باي
        //[Authorize(Roles = "Manager")] // فقط المدير يمكنه الوصول
        public IActionResult Manage()
        {
            var accounts = _instaPayService.GetInstaPayAccounts();
            return View(accounts);
        }

        // عرض صفحة إضافة حساب إنستا باي
        //[Authorize(Roles = "Manager")]
        public IActionResult AddInstaPayAccount()
        {
            return View();
        }

        // معالجة إضافة حساب إنستا باي
        [HttpPost]
        //[Authorize(Roles = "Manager")]
        public async Task<IActionResult> AddInstaPayAccount(InstaPay model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            // ✨ تأكد من ربط الحساب بالمستخدم الحالي
            model.UserId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var (success, message) = await _instaPayService.AddInstaPayAccountAsync(model);

            if (success)
            {
                TempData["SuccessMessage"] = message;
                return RedirectToAction("Manage");
            }

            ModelState.AddModelError("", message);
            return View(model);
        }


        // عرض صفحة تنفيذ عملية إنستا باي
        //[Authorize(Roles = "Employee,Manager")] // الموظف أو المدير
        public IActionResult ProcessTransaction()
        {
            if (!User.Identity.IsAuthenticated)
            {
                return RedirectToAction("Login", "Account");
            }

            var accounts = _instaPayService.GetInstaPayAccounts()?.ToList();
            if (accounts == null || accounts.Count == 0)
            {
                TempData["ErrorMessage"] = "لا توجد حسابات إنستا باي متاحة.";
                return RedirectToAction("Manage");
            }
            ViewBag.InstaPayAccounts = accounts;
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> ProcessTransaction(InstaPayTransaction model)
        {
            if (!User.Identity.IsAuthenticated)
            {
                ModelState.AddModelError("", "يرجى تسجيل الدخول أولاً.");
                ViewBag.InstaPayAccounts = _instaPayService.GetInstaPayAccounts()?.ToList();
                return View(model);
            }

            model.UserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(model.UserId))
            {
                ModelState.AddModelError("", "تعذر العثور على معرف المستخدم.");
                ViewBag.InstaPayAccounts = _instaPayService.GetInstaPayAccounts()?.ToList();
                return View(model);
            }

            if (model.InstaPayId == 0)
            {
                ModelState.AddModelError("InstaPayId", "يرجى اختيار حساب إنستا باي.");
            }

            if (!Enum.IsDefined(typeof(TransactionType), model.TransactionType))
            {
                ModelState.AddModelError("TransactionType", "نوع العملية غير صالح.");
            }

            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage);
                System.Diagnostics.Debug.WriteLine("ModelState Errors: " + string.Join(", ", errors));
                ViewBag.InstaPayAccounts = _instaPayService.GetInstaPayAccounts()?.ToList();
                return View(model);
            }

            var (success, message, transaction) = await _instaPayService.ProcessInstaPayTransactionAsync(model);
            if (success)
            {
                TempData["SuccessMessage"] = $"{message} المبلغ النهائي: {transaction.NetAmount}";
                return RedirectToAction("ProcessTransaction");
            }

            ModelState.AddModelError("", message);
            ViewBag.InstaPayAccounts = _instaPayService.GetInstaPayAccounts()?.ToList();
            return View(model);
        }
    }
}
using CashManagement.Data;
using CashManagement.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace CashManagement.Controllers
{
    // [Authorize(Roles = "Admin")] // المدير فقط يمكنه الوصول إلى هذا الكونترولر
    public class UserController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly ApplicationDbContext _context;
        private readonly SignInManager<ApplicationUser> _signInManager;

        public UserController(UserManager<ApplicationUser> userManager, RoleManager<IdentityRole> roleManager, ApplicationDbContext context, SignInManager<ApplicationUser> signInManager)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _context = context;
            _signInManager = signInManager;
        }

        // عرض قائمة جميع المستخدمين
        // GET: /User/Index
        public async Task<IActionResult> Index()
        {
            // تحميل جميع المستخدمين أولاً
            var users = await _userManager.Users.AsNoTracking().ToListAsync();

            // إنشاء قائمة UserViewModel مع جلب الأدوار بشكل منفصل
            var userViewModels = new List<UserViewModel>();
            foreach (var user in users)
            {
                var roles = await _userManager.GetRolesAsync(user);
                userViewModels.Add(new UserViewModel
                {
                    Id = user.Id,
                    FullName = user.FullName,
                    Email = user.Email,
                    UserName = user.UserName,
                    IsActive = user.IsActive,
                    CreatedAt = user.CreatedAt,
                    Role = roles.FirstOrDefault()
                });
            }

            return View(userViewModels);
        }

        // عرض نموذج إضافة مستخدم جديد
        public async Task<IActionResult> Create()
        {
            var model = new CreateUserViewModel
            {
                AvailableRoles = await _roleManager.Roles.Select(r => r.Name).ToListAsync()
            };
            return View(model);
        }

        // حفظ مستخدم جديد
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Store(CreateUserViewModel model)
        {
            model.AvailableRoles = await _roleManager.Roles.Select(r => r.Name).ToListAsync();

            if (!ModelState.IsValid)
            {
                return View("Create", model);
            }

            // التحقق من عدم تكرار البريد الإلكتروني
            var emailExists = await _userManager.FindByEmailAsync(model.Email);
            if (emailExists != null)
            {
                ModelState.AddModelError("Email", "البريد الإلكتروني مسجل مسبقًا.");
                return View("Create", model);
            }

            // التحقق من عدم تكرار اسم المستخدم
            var userNameExists = await _userManager.FindByNameAsync(model.UserName);
            if (userNameExists != null)
            {
                ModelState.AddModelError("UserName", "اسم المستخدم مسجل مسبقًا.");
                return View("Create", model);
            }

            var user = new ApplicationUser
            {
                UserName = model.UserName,
                Email = model.Email,
                FullName = model.FullName,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                EmailConfirmed = true
            };
            var result = await _userManager.CreateAsync(user, model.Password);
            if (!result.Succeeded)
            {
                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }
                return View("Create", model);
            }

            // تعيين الدور للمستخدم
            if (!string.IsNullOrEmpty(model.Role))
            {
                var roleExists = await _roleManager.RoleExistsAsync(model.Role);
                if (roleExists)
                {
                    await _userManager.AddToRoleAsync(user, model.Role);
                }
                else
                {
                    ModelState.AddModelError("Role", "الدور المحدد غير موجود.");
                    return View("Create", model);
                }
            }

            TempData["Success"] = "تم إضافة المستخدم بنجاح.";
            return RedirectToAction("Index");
        }

        // عرض نموذج تعديل مستخدم
        public async Task<IActionResult> Edit(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null)
            {
                return NotFound("المستخدم غير موجود.");
            }

            var model = new UpdateUserViewModel
            {
                Id = user.Id,
                FullName = user.FullName,
                UserName = user.UserName,
                Email = user.Email,
                IsActive = user.IsActive,
                Role = (await _userManager.GetRolesAsync(user)).FirstOrDefault(),
                AvailableRoles = await _roleManager.Roles.Select(r => r.Name).ToListAsync()
            };

            return View(model);
        }

        // تحديث بيانات المستخدم
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Update(UpdateUserViewModel model)
        {
            model.AvailableRoles = await _roleManager.Roles.Select(r => r.Name).ToListAsync();

            if (!ModelState.IsValid)
            {
                return View("Edit", model);
            }

            var user = await _userManager.FindByIdAsync(model.Id);
            if (user == null)
            {
                return NotFound("المستخدم غير موجود.");
            }

            // التحقق من عدم تكرار البريد الإلكتروني
            var emailExists = await _userManager.FindByEmailAsync(model.Email);
            if (emailExists != null && emailExists.Id != user.Id)
            {
                ModelState.AddModelError("Email", "البريد الإلكتروني مسجل مسبقًا.");
                return View("Edit", model);
            }

            // التحقق من عدم تكرار اسم المستخدم
            var userNameExists = await _userManager.FindByNameAsync(model.UserName);
            if (userNameExists != null && userNameExists.Id != user.Id)
            {
                ModelState.AddModelError("UserName", "اسم المستخدم مسجل مسبقًا.");
                return View("Edit", model);
            }

            // تحديث بيانات المستخدم
            user.FullName = model.FullName;
            user.UserName = model.UserName;
            user.Email = model.Email;
            user.IsActive = model.IsActive;
            user.UpdatedAt = DateTime.UtcNow;

            var result = await _userManager.UpdateAsync(user);
            if (!result.Succeeded)
            {
                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }
                return View("Edit", model);
            }

            // تحديث الدور
            var currentRoles = await _userManager.GetRolesAsync(user);
            if (currentRoles.Any() && currentRoles.FirstOrDefault() != model.Role)
            {
                await _userManager.RemoveFromRolesAsync(user, currentRoles);
                if (!string.IsNullOrEmpty(model.Role))
                {
                    var roleExists = await _roleManager.RoleExistsAsync(model.Role);
                    if (roleExists)
                    {
                        await _userManager.AddToRoleAsync(user, model.Role);
                    }
                    else
                    {
                        ModelState.AddModelError("Role", "الدور المحدد غير موجود.");
                        return View("Edit", model);
                    }
                }
            }
            else if (!currentRoles.Any() && !string.IsNullOrEmpty(model.Role))
            {
                var roleExists = await _roleManager.RoleExistsAsync(model.Role);
                if (roleExists)
                {
                    await _userManager.AddToRoleAsync(user, model.Role);
                }
                else
                {
                    ModelState.AddModelError("Role", "الدور المحدد غير موجود.");
                    return View("Edit", model);
                }
            }

            TempData["Success"] = "تم تحديث المستخدم بنجاح.";
            return RedirectToAction("Index");
        }

        // عرض تفاصيل المستخدم
        [HttpGet]
        public async Task<IActionResult> Show(string id)
        {
            var user = await _userManager.Users
                .AsNoTracking()
                .Include(u => u.CashTransactions).ThenInclude(ct => ct.CashLine)
                .Include(u => u.PhysicalCashTransactions)
                .Include(u => u.InstaPayTransactions).ThenInclude(it => it.InstaPay)
                .Include(u => u.FawryTransactions).ThenInclude(ft => ft.FawryService)
                .Include(u => u.SupplierTransactions).ThenInclude(st => st.Supplier)
                .FirstOrDefaultAsync(u => u.Id == id);

            if (user == null)
            {
                TempData["Error"] = "المستخدم غير موجود.";
                return RedirectToAction("Index");
            }

            var model = new UserDetailsViewModel
            {
                Id = user.Id,
                FullName = user.FullName ?? "غير متوفر",
                Email = user.Email ?? "غير متوفر",
                UserName = user.UserName ?? "غير متوفر",
                IsActive = user.IsActive,
                CreatedAt = user.CreatedAt,
                UpdatedAt = user.UpdatedAt,
                Roles = await _userManager.GetRolesAsync(user),
                CashTransactions = user.CashTransactions?.ToList() ?? new List<CashTransaction>(),
                PhysicalCashTransactions = user.PhysicalCashTransactions?.ToList() ?? new List<CashTransaction_Physical>(),
                InstaPayTransactions = user.InstaPayTransactions?.ToList() ?? new List<InstaPayTransaction>(),
                FawryTransactions = user.FawryTransactions?.ToList() ?? new List<FawryTransaction>(),
                SupplierTransactions = user.SupplierTransactions?.ToList() ?? new List<SupplierTransaction>()
            };

            return View(model);
        }

        // تجميد حساب موظف
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Freeze(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null)
            {
                return NotFound("المستخدم غير موجود.");
            }

            // منع تجميد حساب المدير
            if (await _userManager.IsInRoleAsync(user, "Admin"))
            {
                TempData["Error"] = "لا يمكن تجميد حساب المدير.";
                return RedirectToAction("Show", new { id });
            }

            user.IsActive = false;
            user.UpdatedAt = DateTime.UtcNow;
            await _userManager.UpdateAsync(user);

            TempData["Success"] = "تم تجميد الحساب بنجاح.";
            return RedirectToAction("Show", new { id });
        }

        // إلغاء تجميد حساب موظف
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Unfreeze(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null)
            {
                return NotFound("المستخدم غير موجود.");
            }

            user.IsActive = true;
            user.UpdatedAt = DateTime.UtcNow;
            await _userManager.UpdateAsync(user);

            TempData["Success"] = "تم إلغاء تجميد الحساب بنجاح.";
            return RedirectToAction("Show", new { id });
        }

        // حذف مستخدم
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(string id)
        {
            var userToDelete = await _userManager.FindByIdAsync(id);

            if (userToDelete == null)
            {
                TempData["Error"] = "المستخدم غير موجود.";
                return RedirectToAction("Index");
            }

            // التحقق من أن المستخدم لا يحذف حسابه الخاص
            var currentUserId = _userManager.GetUserId(User);
            if (userToDelete.Id == currentUserId)
            {
                TempData["Error"] = "لا يمكنك حذف حسابك الخاص أثناء تسجيل الدخول.";
                return RedirectToAction("Index");
            }

            var result = await _userManager.DeleteAsync(userToDelete);
            if (result.Succeeded)
            {
                TempData["Success"] = "تم حذف المستخدم بنجاح.";
                return RedirectToAction("Index");
            }

            TempData["Error"] = "حدث خطأ أثناء حذف المستخدم.";
            return RedirectToAction("Index");
        }
    }

    // نماذج العرض (ViewModels)
    public class UserListViewModel
    {
        public string Id { get; set; }
        public string FullName { get; set; }
        public string Email { get; set; }
        public string UserName { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class CreateUserViewModel
    {
        [Required(ErrorMessage = "الاسم الكامل مطلوب")]
        [StringLength(100, ErrorMessage = "الاسم الكامل يجب ألا يتجاوز 100 حرف")]
        public string FullName { get; set; }

        [Required(ErrorMessage = "اسم المستخدم مطلوب")]
        [StringLength(50, ErrorMessage = "اسم المستخدم يجب ألا يتجاوز 50 حرف")]
        public string UserName { get; set; }

        [Required(ErrorMessage = "البريد الإلكتروني مطلوب")]
        [EmailAddress(ErrorMessage = "البريد الإلكتروني غير صالح")]
        public string Email { get; set; }

        [Required(ErrorMessage = "كلمة المرور مطلوبة")]
        [DataType(DataType.Password)]
        [StringLength(100, MinimumLength = 6, ErrorMessage = "كلمة المرور يجب أن تكون بين 6 و100 حرف")]
        public string Password { get; set; }

        [Required(ErrorMessage = "يجب اختيار دور")]
        public string Role { get; set; }

        public List<string> AvailableRoles { get; set; } = new List<string>();
    }

    public class UpdateUserViewModel
    {
        public string Id { get; set; }

        [Required(ErrorMessage = "الاسم الكامل مطلوب")]
        [StringLength(100, ErrorMessage = "الاسم الكامل يجب ألا يتجاوز 100 حرف")]
        public string FullName { get; set; }

        [Required(ErrorMessage = "اسم المستخدم مطلوب")]
        [StringLength(50, ErrorMessage = "اسم المستخدم يجب ألا يتجاوز 50 حرف")]
        public string UserName { get; set; }

        [Required(ErrorMessage = "البريد الإلكتروني مطلوب")]
        [EmailAddress(ErrorMessage = "البريد الإلكتروني غير صالح")]
        public string Email { get; set; }

        public bool IsActive { get; set; }

        [Required(ErrorMessage = "يجب اختيار دور")]
        public string Role { get; set; }

        public List<string> AvailableRoles { get; set; } = new List<string>();
    }

    public class UserDetailsViewModel
    {
        public string Id { get; set; }
        public string FullName { get; set; }
        public string Email { get; set; }
        public string UserName { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public IList<string> Roles { get; set; }
        public List<CashTransaction> CashTransactions { get; set; }
        public List<CashTransaction_Physical> PhysicalCashTransactions { get; set; }
        public List<InstaPayTransaction> InstaPayTransactions { get; set; }
        public List<FawryTransaction> FawryTransactions { get; set; }
        public List<SupplierTransaction> SupplierTransactions { get; set; }
        public List<CashLine> CashLines { get; set; }
    }

    public class UserViewModel
    {
        public string Id { get; set; }
        public string FullName { get; set; }
        public string Email { get; set; }
        public string UserName { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public string Role { get; set; }
    }
}
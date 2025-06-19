// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
#nullable disable

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;
using CashManagement.Models; // تأكد من استيراد ApplicationUser من هنا

namespace CashManagement.Areas.Identity.Pages.Account
{
    public class LoginModel : PageModel
    {
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly ILogger<LoginModel> _logger;

        public LoginModel(SignInManager<ApplicationUser> signInManager, ILogger<LoginModel> logger)
        {
            _signInManager = signInManager;
            _logger = logger;
        }

        [BindProperty]
        public InputModel Input { get; set; }

        public IList<AuthenticationScheme> ExternalLogins { get; set; }

        public string ReturnUrl { get; set; }

        [TempData]
        public string ErrorMessage { get; set; }

        public class InputModel
        {
            [Required(ErrorMessage = "البريد الإلكتروني مطلوب.")]
            [EmailAddress(ErrorMessage = "البريد الإلكتروني غير صالح.")]
            public string Email { get; set; }

            [Required(ErrorMessage = "كلمة المرور مطلوبة.")]
            [DataType(DataType.Password)]
            public string Password { get; set; }

            [Display(Name = "تذكرني؟")]
            public bool RememberMe { get; set; }
        }

        public async Task OnGetAsync(string returnUrl = null)
        {
            if (!string.IsNullOrEmpty(ErrorMessage))
            {
                ModelState.AddModelError(string.Empty, ErrorMessage);
            }

            returnUrl ??= Url.Content("~/");

            // Clear the existing external cookie to ensure a clean login process
            await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);

            ExternalLogins = (await _signInManager.GetExternalAuthenticationSchemesAsync()).ToList();

            ReturnUrl = returnUrl;
        }

        public async Task<IActionResult> OnPostAsync(string returnUrl = null)
        {
            returnUrl ??= Url.Content("~/");

            ExternalLogins = (await _signInManager.GetExternalAuthenticationSchemesAsync()).ToList();

            if (ModelState.IsValid)
            {
                // الخطوة 1: البحث عن المستخدم باستخدام البريد الإلكتروني
                // هذا هو UserManager المتاح من خلال SignInManager
                var user = await _signInManager.UserManager.FindByEmailAsync(Input.Email);

                // الخطوة 2: التحقق إذا كان المستخدم موجوداً
                if (user == null)
                {
                    ModelState.AddModelError(string.Empty, "محاولة تسجيل دخول غير صالحة.");
                    _logger.LogWarning($"Login failed: User not found for email {Input.Email}");
                    return Page();
                }

                // الخطوة 3: التحقق من خاصية IsActive المخصصة بك
                if (!user.IsActive)
                {
                    ModelState.AddModelError(string.Empty, "هذا الحساب غير نشط أو تم تجميده.");
                    _logger.LogWarning($"Login failed: User {user.UserName} (ID: {user.Id}) is inactive.");
                    return Page();
                }

                // الخطوة 4: محاولة تسجيل الدخول باستخدام اسم المستخدم (UserName)
                // (PasswordSignInAsync يتوقع UserName كمعامل أول افتراضيًا)
                var result = await _signInManager.PasswordSignInAsync(user.UserName, Input.Password, Input.RememberMe, lockoutOnFailure: false);

                if (result.Succeeded)
                {
                    _logger.LogInformation("تم تسجيل دخول المستخدم.");
                    return LocalRedirect(returnUrl);
                }
                if (result.RequiresTwoFactor)
                {
                    _logger.LogWarning($"User {user.UserName} requires two-factor authentication.");
                    return RedirectToPage("./LoginWith2fa", new { ReturnUrl = returnUrl, RememberMe = Input.RememberMe });
                }
                if (result.IsLockedOut)
                {
                    _logger.LogWarning($"تم قفل حساب المستخدم: {user.UserName}.");
                    return RedirectToPage("./Lockout");
                }
                else
                {
                    // إذا لم تنجح أي من الشروط أعلاه، يعني أن كلمة المرور غير صحيحة أو هناك مشكلة أخرى
                    ModelState.AddModelError(string.Empty, "محاولة تسجيل دخول غير صالحة.");
                    _logger.LogWarning($"Invalid login attempt for user {user.UserName} (email: {Input.Email}).");
                    return Page();
                }
            }

            // إذا لم يكن ModelState صالحًا، أعد عرض النموذج مع أخطاء التحقق من الصحة
            return Page();
        }
    }
}
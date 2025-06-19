using CashManagement.Data;
using CashManagement.Models;
using CashManagement.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Hangfire;
using Hangfire.SqlServer;
using System.Threading.Tasks;
using Hangfire.Dashboard;


namespace CashManagement
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // إعداد الاتصال بقاعدة البيانات
            var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
            builder.Services.AddDbContext<ApplicationDbContext>(options =>
                options.UseSqlServer(connectionString));

            // إضافة خدمات Hangfire
            builder.Services.AddHangfire(configuration => configuration
                .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
                .UseSimpleAssemblyNameTypeSerializer()
                .UseRecommendedSerializerSettings()
                .UseSqlServerStorage(connectionString)); // استخدام نفس قاعدة البيانات

            builder.Services.AddHangfireServer();
            builder.Services.AddScoped<InstaPayService>();
            // إضافة خدمة DailyResetService
            builder.Services.AddScoped<DailyResetService>();

            // إضافة خدمات Identity مع دعم الأدوار
            builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
            {
                options.SignIn.RequireConfirmedAccount = false;
                options.Password.RequireDigit = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireNonAlphanumeric = true;
                options.Password.RequiredLength = 6;
            })
                .AddEntityFrameworkStores<ApplicationDbContext>()
                .AddDefaultTokenProviders()
                .AddRoles<IdentityRole>();

            // إضافة خدمات MVC
            builder.Services.AddControllersWithViews();

            // إضافة دعم Razor Pages
            builder.Services.AddRazorPages();

            // إضافة دعم لعرض أخطاء قاعدة البيانات في بيئة التطوير
            builder.Services.AddDatabaseDeveloperPageExceptionFilter();

            var app = builder.Build();

            // إعداد خط أنابيب HTTP
            if (app.Environment.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
                app.UseMigrationsEndPoint();
            }
            else
            {
                app.UseExceptionHandler("/Home/Error");
                app.UseHsts();
            }

            app.UseHttpsRedirection();
            app.UseStaticFiles();

            app.UseRouting();

            // إضافة Authentication قبل Authorization
            app.UseAuthentication();
            app.UseAuthorization();

            // إعداد Hangfire Dashboard
            app.UseHangfireDashboard("/hangfire", new DashboardOptions
            {
                Authorization = new[] { new HangfireAuthorizationFilter() }
            });

            // إعداد مسارات MVC
            app.MapControllerRoute(
                name: "default",
                pattern: "{controller=Home}/{action=Index}/{id?}");

            // إعداد مسارات Razor Pages
            app.MapRazorPages();

            // جدولة المهمة اليومية لإعادة التعيين وفك التجميد
            RecurringJob.AddOrUpdate<DailyResetService>(
                "ResetDailyLimitsAndUnfreeze",
                service => service.ResetDailyLimitsAndUnfreezeAsync(),
                "5 0 * * *"); // كل يوم الساعة 12:05 صباحًا (بالتوقيت المحلي للخادم)

            // إنشاء الأدوار ومستخدم المدير الافتراضي
            using (var scope = app.Services.CreateScope())
            {
                var services = scope.ServiceProvider;
                await SeedData(services);
            }

            app.Run();
        }

        // دالة لإنشاء الأدوار ومستخدم المدير الافتراضي
        private static async Task SeedData(IServiceProvider serviceProvider)
        {
            var roleManager = serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            var userManager = serviceProvider.GetRequiredService<UserManager<ApplicationUser>>();

            // إنشاء الأدوار إذا لم تكن موجودة
            string[] roleNames = { "Admin", "Employee" };
            foreach (var roleName in roleNames)
            {
                if (!await roleManager.RoleExistsAsync(roleName))
                {
                    await roleManager.CreateAsync(new IdentityRole(roleName));
                }
            }

            // إنشاء مستخدم مدير افتراضي إذا لم يكن موجودًا
            string adminEmail = "admin@cashmanagement.com";
            string adminPassword = "Admin@123";

            if (await userManager.FindByEmailAsync(adminEmail) == null)
            {
                var adminUser = new ApplicationUser
                {
                    UserName = adminEmail,
                    Email = adminEmail,
                    FullName = "Admin User",
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                };

                var result = await userManager.CreateAsync(adminUser, adminPassword);
                if (result.Succeeded)
                {
                    await userManager.AddToRoleAsync(adminUser, "Admin");
                }
                else
                {
                    throw new InvalidOperationException("Failed to create default admin user.");
                }
            }
        }

        // مرشح أذونات Hangfire
        private class HangfireAuthorizationFilter : IDashboardAuthorizationFilter
        {
            public bool Authorize(DashboardContext context)
            {
                var httpContext = context.GetHttpContext();
                return httpContext.User.Identity.IsAuthenticated &&
                       httpContext.User.IsInRole("Admin");
            }
        }
    }
}
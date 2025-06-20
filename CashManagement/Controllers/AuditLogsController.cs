using CashManagement.Data;
using CashManagement.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace CashManagement.Controllers
{
    //[Authorize(Roles = "Admin")]
    public class AuditLogsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public AuditLogsController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // GET: AuditLogs/Index
        public async Task<IActionResult> Index(
            string searchString,
            string actionType,
            string entityType,
            DateTime? startDate,
            DateTime? endDate,
            int page = 1,
            int pageSize = 10)
        {
            var logsQuery = _context.AuditLogs
                .Include(l => l.User)
                .AsQueryable();

            // Filter by search string (UserId, Username, or Details)
            if (!string.IsNullOrEmpty(searchString))
            {
                logsQuery = logsQuery.Where(l =>
                    l.UserId.Contains(searchString) ||
                    (l.User != null && l.User.UserName.Contains(searchString)) ||
                    l.Details.Contains(searchString));
            }

            // Filter by ActionType
            if (!string.IsNullOrEmpty(actionType))
            {
                logsQuery = logsQuery.Where(l => l.ActionType == actionType);
            }

            // Filter by EntityType
            if (!string.IsNullOrEmpty(entityType))
            {
                logsQuery = logsQuery.Where(l => l.EntityType == entityType);
            }

            // Filter by date range
            if (startDate.HasValue)
            {
                logsQuery = logsQuery.Where(l => l.CreatedAt >= startDate.Value.ToUniversalTime());
            }
            if (endDate.HasValue)
            {
                logsQuery = logsQuery.Where(l => l.CreatedAt <= endDate.Value.ToUniversalTime());
            }

            // Get distinct action types and entity types for dropdowns
            ViewBag.ActionTypes = await _context.AuditLogs
                .Select(l => l.ActionType)
                .Distinct()
                .ToListAsync();
            ViewBag.EntityTypes = await _context.AuditLogs
                .Select(l => l.EntityType)
                .Distinct()
                .ToListAsync();

            // Pagination
            var totalItems = await logsQuery.CountAsync();
            var pagedLogs = await logsQuery
                .OrderByDescending(l => l.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            // Pass filter values to view
            ViewBag.SearchString = searchString;
            ViewBag.ActionType = actionType;
            ViewBag.EntityType = entityType;
            ViewBag.StartDate = startDate?.ToString("yyyy-MM-dd");
            ViewBag.EndDate = endDate?.ToString("yyyy-MM-dd");
            ViewBag.Page = page;
            ViewBag.PageSize = pageSize;
            ViewBag.TotalPages = (int)Math.Ceiling((double)totalItems / pageSize);

            return View(pagedLogs);
        }
    }
}
using Dapper;
using DormWeb.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Data;
using static AspNetCoreGeneratedDocument.Views_Guard_TrafficReport;

namespace DormWeb.Controllers
{
    public class GuardController : Controller
    {
        private readonly string _connStr;

        public GuardController(IConfiguration config)
        {
            _connStr = config.GetConnectionString("Default")!;
        }

        private IActionResult CheckRole()
        {
            if (HttpContext.Session.GetString("UserRole") != "نگهبان")
                return RedirectToAction("Login", "Auth");
            return null!;
        }

        public async Task<IActionResult> Index(string? search, int page = 1, int pageSize = 5)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            using var conn = new SqlConnection(_connStr);
            var guardInfo = await conn.QueryFirstOrDefaultAsync<dynamic>(
                "SELECT FirstName, LastName, PersonnelID, UserRole FROM Staff WHERE PersonnelID = @Id",
                new { Id = userId });

            ViewBag.GuardFirstName = guardInfo?.FirstName ?? "نگهبان";
            ViewBag.GuardLastName = guardInfo?.LastName ?? "";
            ViewBag.GuardPersonnelID = guardInfo?.PersonnelID ?? 0;
            ViewBag.GuardRole = guardInfo?.UserRole ?? "نگهبان";
            var roleCheck = CheckRole();
            if (roleCheck != null) return roleCheck;


            string whereClause = "";
            if (!string.IsNullOrEmpty(search))
            {
                whereClause = @"WHERE CAST(StudentID AS NVARCHAR) LIKE @Search 
                         OR FirstName LIKE @Search 
                         OR LastName LIKE @Search";
            }

            string countSql = $"SELECT COUNT(*) FROM vw_GuardStudentSearch {whereClause}";
            int totalCount = await conn.ExecuteScalarAsync<int>(countSql, new { Search = $"%{search}%" });

            int offset = (page - 1) * pageSize;
            string dataSql = $@"
        SELECT * FROM vw_GuardStudentSearch 
        {whereClause}
        ORDER BY StudentID
        OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY";

            var students = await conn.QueryAsync<StudentGuardViewModel>(
                dataSql,
                new { Search = $"%{search}%", Offset = offset, PageSize = pageSize });

            int totalPages = (int)Math.Ceiling((double)totalCount / pageSize);

            ViewBag.Search = search;
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPages;
            ViewBag.PageSize = pageSize;
            ViewBag.TotalCount = totalCount;

            return View(students);
        }

        public async Task<IActionResult> TrafficReport(int? studentId, DateTime? startDate, DateTime? endDate,string? startTime, string? endTime)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            using var conn = new SqlConnection(_connStr);
            var guardInfo = await conn.QueryFirstOrDefaultAsync<dynamic>(
                "SELECT FirstName, LastName, PersonnelID, UserRole FROM Staff WHERE PersonnelID = @Id",
                new { Id = userId });

            ViewBag.GuardFirstName = guardInfo?.FirstName ?? "نگهبان";
            ViewBag.GuardLastName = guardInfo?.LastName ?? "";
            ViewBag.GuardPersonnelID = guardInfo?.PersonnelID ?? 0;
            ViewBag.GuardRole = guardInfo?.UserRole ?? "نگهبان";
            var roleCheck = CheckRole();
            if (roleCheck != null) return roleCheck;

            IEnumerable<TrafficRecord> records;

            bool noFilter = !studentId.HasValue && !startDate.HasValue && !endDate.HasValue
                            && string.IsNullOrEmpty(startTime) && string.IsNullOrEmpty(endTime);

            if (noFilter)
            {
                string sql = @"
            SELECT TOP 100 al.AID, al.StudentID, s.FirstName, s.LastName,
                   al.AccessType, al.Date AS AccessDate, al.Time AS AccessTime
            FROM AccessLog al
            INNER JOIN Student s ON al.StudentID = s.StudentID
            ORDER BY al.Date DESC, al.Time DESC";
                records = await conn.QueryAsync<TrafficRecord>(sql);
                ViewBag.InfoMessage = "نمایش ۱۰۰ تردد آخر. برای جستجوی دقیق‌تر، فیلترها را پر کنید.";
            }
            else
            {
                var parameters = new DynamicParameters();
                parameters.Add("@StudentID", studentId);
                parameters.Add("@StartDate", startDate);
                parameters.Add("@EndDate", endDate);
                TimeSpan? st = null, et = null;
                if (!string.IsNullOrEmpty(startTime) && TimeSpan.TryParse(startTime, out var pst)) st = pst;
                if (!string.IsNullOrEmpty(endTime) && TimeSpan.TryParse(endTime, out var pet)) et = pet;
                parameters.Add("@StartTime", st);
                parameters.Add("@EndTime", et);

                var allRecords = await conn.QueryAsync<TrafficRecord>(
                    "usp_GetGuardTrafficReport", parameters,
                    commandType: CommandType.StoredProcedure);

                records = allRecords.Take(100);   // ⬅️ حداکثر ۱۰۰ تا
                ViewBag.InfoMessage = $"نتایج جستجو: {records.Count()} رکورد (حداکثر ۱۰۰ رکورد)";
            }

            ViewBag.StudentIdFilter = studentId;
            ViewBag.StartDateFilter = startDate?.ToString("yyyy-MM-dd");
            ViewBag.EndDateFilter = endDate?.ToString("yyyy-MM-dd");
            ViewBag.StartTimeFilter = startTime;
            ViewBag.EndTimeFilter = endTime;

            return View(records);
        }


        public async Task<IActionResult> PersonalInfo()
        {
            var roleCheck = CheckRole();
            if (roleCheck != null) return roleCheck;

            var userId = HttpContext.Session.GetInt32("UserId");
            using var conn = new SqlConnection(_connStr);
            var info = await conn.QueryFirstOrDefaultAsync<GuardInfoViewModel>(
                "SELECT * FROM Staff WHERE PersonnelID = @Id",
                new { Id = userId });

            return View(info);
        }

        [HttpPost]
        public async Task<IActionResult> PersonalInfo(string MobilePhoneNumber, string OrganizationalEmail, string WorkShift, string Workplace)
        {
            var roleCheck = CheckRole();
            if (roleCheck != null) return roleCheck;

            var userId = HttpContext.Session.GetInt32("UserId");
            using var conn = new SqlConnection(_connStr);

            await conn.ExecuteAsync(
                @"UPDATE Staff SET 
            MobilePhoneNumber = @Mobile,
            OrganizationalEmail = @Email,
            WorkShift = @Shift,
            Workplace = @Work
          WHERE PersonnelID = @Id",
                new
                {
                    Mobile = MobilePhoneNumber,
                    Email = OrganizationalEmail,
                    Shift = WorkShift,
                    Work = Workplace,
                    Id = userId
                });

            ViewBag.SuccessMessage = "اطلاعات با موفقیت به‌روزرسانی شد.";

            var info = await conn.QueryFirstOrDefaultAsync<GuardInfoViewModel>(
                "SELECT * FROM Staff WHERE PersonnelID = @Id",
                new { Id = userId });

            return View(info);
        }
    }
}
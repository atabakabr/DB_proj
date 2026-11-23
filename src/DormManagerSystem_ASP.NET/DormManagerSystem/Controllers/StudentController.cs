using Dapper;
using DormWeb.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Data;

namespace DormWeb.Controllers
{
    public class StudentController : Controller
    {
        private readonly string _connStr;
        public StudentController(IConfiguration config) => _connStr = config.GetConnectionString("Default")!;

        private IActionResult CheckRole()
        {
            if (HttpContext.Session.GetString("UserRole") != "Student")
                return RedirectToAction("Login", "Auth");
            return null!;
        }

        private int GetStudentId() => HttpContext.Session.GetInt32("UserId") ?? 0;

        private async Task SetInfo()
        {
            var userId = GetStudentId();
            using var conn = new SqlConnection(_connStr);
            var info = await conn.QueryFirstOrDefaultAsync<dynamic>(
                "SELECT StudentID, FirstName, LastName FROM Student WHERE StudentID = @Id", new { Id = userId });
            ViewBag.StudentFirstName = info?.FirstName ?? "دانشجو";
            ViewBag.StudentLastName = info?.LastName ?? "";
            ViewBag.StudentID = info?.StudentID ?? 0;
        }

        public async Task<IActionResult> Index()
        {
            var roleCheck = CheckRole();
            if (roleCheck != null) return roleCheck;
            await SetInfo();

            int studentId = GetStudentId();
            using var conn = new SqlConnection(_connStr);

            var announcements = await conn.QueryAsync<AnnouncementItem>(@"
                SELECT TOP 5 a.*
                FROM Announcement a
                LEFT JOIN AnnToSTU ast ON a.AnnouncementID = ast.AnnouncementID AND ast.STUID = @Sid
                LEFT JOIN AnnToBlock ab ON a.AnnouncementID = ab.AnnouncementID
                LEFT JOIN AnnToDorm ad ON a.AnnouncementID = ad.AnnouncementID
                LEFT JOIN [Group] g ON g.GroupID = (SELECT GroupNumber FROM Student WHERE StudentID = @Sid)
                LEFT JOIN Room r ON g.RoomID = r.RoomID
                LEFT JOIN Block b ON r.BlockID = b.BlockID
                WHERE a.Status = 1 AND a.EXPDate > GETDATE()
                  AND (ast.STUID = @Sid OR ab.BlockID = b.BlockID OR ad.DormID = b.DormID)
                ORDER BY a.Date DESC",
                new { Sid = studentId });

            return View(announcements);
        }

        [HttpGet]
        public async Task<IActionResult> CreateGroup()
        {
            var roleCheck = CheckRole();
            if (roleCheck != null) return roleCheck;
            await SetInfo();

            int studentId = GetStudentId();
            using var conn = new SqlConnection(_connStr);

            var student = await conn.QueryFirstOrDefaultAsync<dynamic>(
                "SELECT GroupNumber, IsGroupLeader FROM Student WHERE StudentID = @Id", new { Id = studentId });

            if (student?.GroupNumber != null && student?.IsGroupLeader != true)
            {
                ViewBag.ErrorMessage = "شما عضو یک گروه هستید. فقط سرگروه می‌تواند گروه را مدیریت کند.";
                return View();
            }

            if (student?.GroupNumber != null && student?.IsGroupLeader == true)
            {
                int groupId = (int)student.GroupNumber;
                var members = await conn.QueryAsync<int>(
                    "SELECT StudentID FROM Student WHERE GroupNumber = @Gid", new { Gid = groupId });
                ViewBag.GroupId = groupId;
                ViewBag.Members = members.ToList();
                ViewBag.IsEditing = true;
                return View();
            }

            var now = DateTime.Now.Date;
            var slot = await conn.QueryFirstOrDefaultAsync<dynamic>(
                "SELECT TOP 1 BlockID FROM TimeSlot WHERE IsActive = 1 AND @Now BETWEEN GroupingStartDate AND SelectionStartDate",
                new { Now = now });
            if (slot == null)
            {
                ViewBag.ErrorMessage = "در حال حاضر بازهٔ تشکیل گروه فعال نیست.";
                return View();
            }

            ViewBag.IsEditing = false;
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> CreateGroup(string dummy)
        {
            var roleCheck = CheckRole();
            if (roleCheck != null) return roleCheck;
            await SetInfo();

            int myId = GetStudentId();

            var formValues = Request.Form["studentIds"];
            var studentIds = new List<int>();

            foreach (var val in formValues)
            {
                if (string.IsNullOrWhiteSpace(val)) continue;
                if (val.Contains(","))
                {
                    studentIds.AddRange(val.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(int.Parse));
                }
                else
                {
                    if (int.TryParse(val, out int id)) studentIds.Add(id);
                }
            }

            studentIds = studentIds.Distinct().ToList();
            if (!studentIds.Contains(myId)) studentIds.Add(myId);

            if (studentIds.Count < 3)
            {
                TempData["Err"] = "حداقل ۳ نفر (شامل خودتان) برای تشکیل گروه لازم است.";
                return RedirectToAction("CreateGroup");
            }

            using var conn = new SqlConnection(_connStr);
            await conn.OpenAsync();

            var existingGroups = await conn.QueryAsync<int?>(
                "SELECT GroupNumber FROM Student WHERE StudentID IN @Ids AND GroupNumber IS NOT NULL",
                new { Ids = studentIds });

            if (existingGroups.Any())
            {
                TempData["Err"] = "یک یا چند نفر از دانشجویان انتخاب شده، در حال حاضر عضو گروه دیگری هستند.";
                return RedirectToAction("CreateGroup");
            }

            var genders = await conn.QueryAsync<string>(
                "SELECT Gender FROM Student WHERE StudentID IN @Ids", new { Ids = studentIds });
            var distinctGenders = genders.Distinct();
            if (distinctGenders.Count() != 1)
            {
                TempData["Err"] = "همهٔ اعضا باید هم‌جنسیت باشند.";
                return RedirectToAction("CreateGroup");
            }
            string groupType = distinctGenders.First() == "M" ? "M" : "F";

            var now = DateTime.Now.Date;
            var slot = await conn.QueryFirstOrDefaultAsync<dynamic>(
                "SELECT TOP 1 BlockID FROM TimeSlot WHERE IsActive = 1 AND @Now BETWEEN GroupingStartDate AND SelectionStartDate",
                new { Now = now });
            if (slot == null)
            {
                TempData["Err"] = "بازهٔ تشکیل گروه فعال نیست.";
                return RedirectToAction("CreateGroup");
            }

            using var tran = conn.BeginTransaction();
            try
            {
                int groupId = await conn.ExecuteScalarAsync<int>(
                    "INSERT INTO [Group] (GroupType) OUTPUT INSERTED.GroupID VALUES (@Type)",
                    new { Type = groupType }, tran);

                await conn.ExecuteAsync(
                    "UPDATE Student SET GroupNumber = @Gid, IsGroupLeader = 0 WHERE StudentID IN @Ids",
                    new { Gid = groupId, Ids = studentIds }, tran);

                await conn.ExecuteAsync(
                    "UPDATE Student SET IsGroupLeader = 1 WHERE StudentID = @Id AND GroupNumber = @Gid",
                    new { Id = myId, Gid = groupId }, tran);

                tran.Commit();
                TempData["Msg"] = "گروه با موفقیت ایجاد شد.";
            }
            catch (Exception ex)
            {
                tran.Rollback();
                TempData["Err"] = "خطا در ثبت پایگاه داده: " + ex.Message;
            }
            return RedirectToAction("CreateGroup");
        }

        [HttpPost]
        public async Task<IActionResult> EditGroup(List<int> studentIds)
        {
            var roleCheck = CheckRole();
            if (roleCheck != null) return roleCheck;

            int myId = GetStudentId();
            using var conn = new SqlConnection(_connStr);

            var leader = await conn.QueryFirstOrDefaultAsync<dynamic>(
                "SELECT GroupNumber, IsGroupLeader FROM Student WHERE StudentID = @Id", new { Id = myId });

            if (leader?.IsGroupLeader != true)
            {
                TempData["Err"] = "فقط سرگروه مجاز به ویرایش گروه است.";
                return RedirectToAction("CreateGroup");
            }

            int groupId = (int)leader.GroupNumber;

            if (studentIds == null || studentIds.Count < 3)
            {
                TempData["Err"] = "گروه باید حداقل ۳ نفر داشته باشد.";
                return RedirectToAction("CreateGroup");
            }

            var genders = await conn.QueryAsync<string>(
                "SELECT Gender FROM Student WHERE StudentID IN @Ids", new { Ids = studentIds });
            if (genders.Distinct().Count() != 1)
            {
                TempData["Err"] = "همهٔ اعضا باید هم‌جنسیت باشند.";
                return RedirectToAction("CreateGroup");
            }

            await conn.OpenAsync();
            using var tran = conn.BeginTransaction();
            try
            {
                await conn.ExecuteAsync(
                    "UPDATE Student SET GroupNumber = NULL, IsGroupLeader = 0 WHERE GroupNumber = @Gid",
                    new { Gid = groupId }, tran);

                await conn.ExecuteAsync(
                    "UPDATE Student SET GroupNumber = @Gid, IsGroupLeader = 0 WHERE StudentID IN @Ids",
                    new { Gid = groupId, Ids = studentIds }, tran);

                await conn.ExecuteAsync(
                    "UPDATE Student SET IsGroupLeader = 1 WHERE StudentID = @Id AND GroupNumber = @Gid",
                    new { Id = myId, Gid = groupId }, tran);

                tran.Commit();
                TempData["Msg"] = "اعضای گروه با موفقیت به‌روزرسانی شدند.";
            }
            catch (Exception ex)
            {
                tran.Rollback();
                TempData["Err"] = ex.Message;
            }
            return RedirectToAction("CreateGroup");
        }

        [HttpGet]
        public async Task<IActionResult> ReserveRoom()
        {
            var roleCheck = CheckRole();
            if (roleCheck != null) return roleCheck;
            await SetInfo();

            int studentId = GetStudentId();
            using var conn = new SqlConnection(_connStr);

            var group = await conn.QueryFirstOrDefaultAsync<dynamic>(
                "SELECT GroupNumber, IsGroupLeader FROM Student WHERE StudentID = @Id", new { Id = studentId });

            if (group == null || group.GroupNumber == null)
            {
                ViewBag.ErrorMessage = "شما عضو هیچ گروهی نیستید.";
                return View();
            }
            if (group.IsGroupLeader != true)
            {
                ViewBag.ErrorMessage = "فقط سرگروه مجاز به رزرو اتاق است.";
                return View();
            }

            int groupId = (int)group.GroupNumber;
            string groupType = await conn.ExecuteScalarAsync<string>(
                "SELECT GroupType FROM [Group] WHERE GroupID = @Gid", new { Gid = groupId });

            var now = DateTime.Now.Date;
            var slots = await conn.QueryAsync<int>(
                "SELECT BlockID FROM TimeSlot WHERE IsActive = 1 AND @Now BETWEEN SelectionStartDate AND SelectionEndDate",
                new { Now = now });
            var blockIds = slots.ToList();
            if (!blockIds.Any())
            {
                ViewBag.ErrorMessage = "در حال حاضر بازهٔ انتخاب اتاق فعال نیست.";
                return View();
            }

            var rooms = await conn.QueryAsync<RoomViewModel>(@"
                SELECT r.RoomID, r.NameRoom, r.Capacity,
                       r.Capacity - ISNULL((SELECT COUNT(*) FROM Student s JOIN [Group] g ON s.GroupNumber = g.GroupID WHERE g.RoomID = r.RoomID), 0) AS AvailableCapacity,
                       b.BlockID, b.BName AS BlockName, d.DormID, d.DormitoryName, d.DormitoryType
                FROM Room r
                JOIN Block b ON r.BlockID = b.BlockID
                JOIN Dormitory d ON b.DormID = d.DormID
                WHERE b.BlockID IN @BlockIds
                  AND d.DormitoryType = @GType
                  AND r.Status = N'خالی' AND r.RoomCondition = N'قابل سکونت'
                  AND r.Capacity >= (SELECT COUNT(*) FROM Student WHERE GroupNumber = @Gid)",
                new { BlockIds = blockIds, GType = groupType, Gid = groupId });

            ViewBag.GroupId = groupId;
            ViewBag.GroupType = groupType;
            return View(rooms);
        }

        [HttpPost]
        public async Task<IActionResult> ReserveRoom(int groupId, int roomId)
        {
            using var conn = new SqlConnection(_connStr);
            try
            {
                await conn.ExecuteAsync("ReserveRoomForGroup",
                    new { GroupID = groupId, NewRoomID = roomId },
                    commandType: CommandType.StoredProcedure);
                TempData["Msg"] = "اتاق با موفقیت رزرو شد.";
            }
            catch (SqlException ex) { TempData["Err"] = ex.Message; }
            return RedirectToAction("ResidenceInfo");
        }
        [HttpPost]
        public async Task<IActionResult> UnreserveRoom()
        {
            int studentId = GetStudentId();
            using var conn = new SqlConnection(_connStr);

            var groupId = await conn.QueryFirstOrDefaultAsync<int?>(
                "SELECT GroupNumber FROM Student WHERE StudentID = @Id AND IsGroupLeader = 1",
                new { Id = studentId });

            if (groupId == null)
            {
                TempData["Err"] = "فقط سرگروه می‌تواند رزرو را لغو کند.";
                return RedirectToAction("ResidenceInfo");
            }

            try
            {
                await conn.ExecuteAsync("UnreserveRoomForGroup",
                    new { GroupID = groupId.Value },
                    commandType: CommandType.StoredProcedure);
                TempData["Msg"] = "رزرو با موفقیت لغو شد.";
            }
            catch (SqlException ex) { TempData["Err"] = ex.Message; }

            return RedirectToAction("ResidenceInfo");
        }

        public async Task<IActionResult> Payments()
        {
            var roleCheck = CheckRole();
            if (roleCheck != null) return roleCheck;
            await SetInfo();

            int studentId = GetStudentId();
            using var conn = new SqlConnection(_connStr);
            var payments = await conn.QueryAsync<SupervisorPaymentItem>(
                "SELECT TransactionID, StudentID, Title, Date, PaymentStatus FROM Payment WHERE StudentID = @Id AND PaymentStatus = N'در انتظار' ORDER BY Date DESC",
                new { Id = studentId });
            return View(payments);
        }

        [HttpPost]
        public async Task<IActionResult> Pay(int transactionId)
        {
            using var conn = new SqlConnection(_connStr);
            await conn.ExecuteAsync(
                "UPDATE Payment SET PaymentStatus = N'موفق' WHERE TransactionID = @Tid AND StudentID = @Sid",
                new { Tid = transactionId, Sid = GetStudentId() });
            TempData["Msg"] = "پرداخت با موفقیت انجام شد.";
            return RedirectToAction("Payments");
        }

        public async Task<IActionResult> Reports(int page = 1)
        {
            var roleCheck = CheckRole();
            if (roleCheck != null) return roleCheck;
            await SetInfo();

            int studentId = GetStudentId();
            using var conn = new SqlConnection(_connStr);
            int total = await conn.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM Report WHERE StudentID = @Id", new { Id = studentId });
            var list = await conn.QueryAsync<SupervisorReportItem>(
                "SELECT RID, Title, Type, Date, Target, Description, '' AS StudentName FROM Report WHERE StudentID = @Id ORDER BY Date DESC OFFSET @Off ROWS FETCH NEXT 5 ROWS ONLY",
                new { Id = studentId, Off = (page - 1) * 5 });
            ViewBag.Page = page; ViewBag.TotalPages = (int)Math.Ceiling((double)total / 5); ViewBag.TotalCount = total;
            return View(list);
        }

        [HttpPost]
        public async Task<IActionResult> SubmitReport(string Title, string Type, string Target, string Description)
        {
            using var conn = new SqlConnection(_connStr);
            await conn.ExecuteAsync(
                "INSERT INTO Report (Target, Date, Title, Type, Description, StudentID, ManagerID) VALUES (@Target, GETDATE(), @Title, @Type, @Description, @Sid, 1)",
                new { Target, Title, Type, Description, Sid = GetStudentId() });
            TempData["Msg"] = "گزارش ثبت شد.";
            return RedirectToAction("Reports");
        }

        public async Task<IActionResult> Profile()
        {
            var roleCheck = CheckRole();
            if (roleCheck != null) return roleCheck;
            await SetInfo();

            int studentId = GetStudentId();
            using var conn = new SqlConnection(_connStr);
            var info = await conn.QueryFirstOrDefaultAsync<GuardInfoViewModel>(
                "SELECT StudentID AS PersonnelID, FirstName, LastName, NationalID, Password, Gender, BirthDate, MobilePhoneNumber, OrganizationalEmail, Major AS UserRole, AcademicDegree, EmergencyContactNumber FROM Student WHERE StudentID = @Id",
                new { Id = studentId });
            return View(info);
        }

        [HttpPost]
        public async Task<IActionResult> Profile(string MobilePhoneNumber, string OrganizationalEmail)
        {
            int studentId = GetStudentId();
            using var conn = new SqlConnection(_connStr);
            await conn.ExecuteAsync(
                "UPDATE Student SET MobilePhoneNumber=@Mob, OrganizationalEmail=@Email WHERE StudentID = @Id",
                new { Mob = MobilePhoneNumber, Email = OrganizationalEmail, Id = studentId });
            ViewBag.SuccessMessage = "اطلاعات به‌روزرسانی شد.";
            return await Profile();
        }

        public async Task<IActionResult> ResidenceInfo()
        {
            var roleCheck = CheckRole();
            if (roleCheck != null) return roleCheck;
            await SetInfo();

            int studentId = GetStudentId();
            using var conn = new SqlConnection(_connStr);
            var residence = await conn.QueryFirstOrDefaultAsync<dynamic>(@"
                SELECT s.GroupNumber, g.GroupType, r.RoomID, r.NameRoom, b.BName AS BlockName, d.DormitoryName,
                       s.IsGroupLeader
                FROM Student s
                LEFT JOIN [Group] g ON s.GroupNumber = g.GroupID
                LEFT JOIN Room r ON g.RoomID = r.RoomID
                LEFT JOIN Block b ON r.BlockID = b.BlockID
                LEFT JOIN Dormitory d ON b.DormID = d.DormID
                WHERE s.StudentID = @Id", new { Id = studentId });

            if (residence?.GroupNumber == null)
            {
                ViewBag.Message = "شما در حال حاضر عضو گروهی نیستید.";
                return View();
            }

            var roommates = await conn.QueryAsync<dynamic>(
                "SELECT StudentID, FirstName, LastName, IsGroupLeader FROM Student WHERE GroupNumber = @Gid",
                new { Gid = residence.GroupNumber });

            ViewBag.Residence = residence;
            ViewBag.Roommates = roommates;
            return View();
        }

        public async Task<IActionResult> Announcements(int page = 1)
        {
            var roleCheck = CheckRole();
            if (roleCheck != null) return roleCheck;
            await SetInfo();

            int studentId = GetStudentId();
            using var conn = new SqlConnection(_connStr);
            int total = await conn.ExecuteScalarAsync<int>(@"
                SELECT COUNT(DISTINCT a.AnnouncementID)
                FROM Announcement a
                LEFT JOIN AnnToSTU ast ON a.AnnouncementID = ast.AnnouncementID AND ast.STUID = @Sid
                LEFT JOIN AnnToBlock ab ON a.AnnouncementID = ab.AnnouncementID
                LEFT JOIN AnnToDorm ad ON a.AnnouncementID = ad.AnnouncementID
                LEFT JOIN [Group] g ON g.GroupID = (SELECT GroupNumber FROM Student WHERE StudentID = @Sid)
                LEFT JOIN Room r ON g.RoomID = r.RoomID
                LEFT JOIN Block b ON r.BlockID = b.BlockID
                WHERE a.Status = 1 AND a.EXPDate > GETDATE()
                  AND (ast.STUID = @Sid OR ab.BlockID = b.BlockID OR ad.DormID = b.DormID)",
                new { Sid = studentId });

            var list = await conn.QueryAsync<dynamic>(@"
                SELECT DISTINCT a.AnnouncementID, a.Title, a.Description, a.Date, a.EXPDate,
                       CASE WHEN ast.STUID IS NOT NULL THEN N'شخصی'
                            WHEN ab.BlockID IS NOT NULL THEN N'بلوک'
                            WHEN ad.DormID IS NOT NULL THEN N'خوابگاه'
                       END AS TargetType
                FROM Announcement a
                LEFT JOIN AnnToSTU ast ON a.AnnouncementID = ast.AnnouncementID AND ast.STUID = @Sid
                LEFT JOIN AnnToBlock ab ON a.AnnouncementID = ab.AnnouncementID
                LEFT JOIN AnnToDorm ad ON a.AnnouncementID = ad.AnnouncementID
                LEFT JOIN [Group] g ON g.GroupID = (SELECT GroupNumber FROM Student WHERE StudentID = @Sid)
                LEFT JOIN Room r ON g.RoomID = r.RoomID
                LEFT JOIN Block b ON r.BlockID = b.BlockID
                WHERE a.Status = 1 AND a.EXPDate > GETDATE()
                  AND (ast.STUID = @Sid OR ab.BlockID = b.BlockID OR ad.DormID = b.DormID)
                ORDER BY a.Date DESC
                OFFSET @Off ROWS FETCH NEXT 5 ROWS ONLY",
                new { Sid = studentId, Off = (page - 1) * 5 });

            ViewBag.Page = page; ViewBag.TotalPages = (int)Math.Ceiling((double)total / 5); ViewBag.TotalCount = total;
            return View(list);
        }
        [HttpPost]
        public async Task<IActionResult> LeaveGroup()
        {
            int studentId = GetStudentId();
            using var conn = new SqlConnection(_connStr);

            var info = await conn.QueryFirstOrDefaultAsync<dynamic>(
                "SELECT GroupNumber, IsGroupLeader FROM Student WHERE StudentID = @Id", new { Id = studentId });

            if (info?.GroupNumber == null)
            {
                TempData["Err"] = "شما عضو هیچ گروهی نیستید.";
                return RedirectToAction("ResidenceInfo");
            }

            int groupId = (int)info.GroupNumber;
            bool isLeader = info.IsGroupLeader == true;

            if (isLeader)
            {
                TempData["Err"] = "سرگروه نمی‌تواند گروه را ترک کند؛ لطفاً گروه را حذف کنید.";
                return RedirectToAction("ResidenceInfo");
            }

            int memberCount = await conn.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM Student WHERE GroupNumber = @Gid", new { Gid = groupId });

            if (memberCount == 1)
            {
                await conn.ExecuteAsync("UPDATE Student SET GroupNumber = NULL, IsGroupLeader = 0 WHERE StudentID = @Id", new { Id = studentId });
                await conn.ExecuteAsync("DELETE FROM [Group] WHERE GroupID = @Gid", new { Gid = groupId });
                TempData["Msg"] = "شما از گروه خارج شدید و گروه حذف گردید.";
            }
            else
            {
                await conn.ExecuteAsync("UPDATE Student SET GroupNumber = NULL, IsGroupLeader = 0 WHERE StudentID = @Id", new { Id = studentId });
                TempData["Msg"] = "از گروه خارج شدید.";
            }

            return RedirectToAction("ResidenceInfo");
        }

        [HttpPost]
        public async Task<IActionResult> DeleteGroup()
        {
            int studentId = GetStudentId();
            using var conn = new SqlConnection(_connStr);

            var group = await conn.QueryFirstOrDefaultAsync<int?>(
                "SELECT GroupNumber FROM Student WHERE StudentID = @Id AND IsGroupLeader = 1", new { Id = studentId });

            if (group == null)
            {
                TempData["Err"] = "فقط سرگروه می‌تواند گروه را حذف کند.";
                return RedirectToAction("ResidenceInfo");
            }

            await conn.OpenAsync();
            using var tran = conn.BeginTransaction();
            try
            {
                await conn.ExecuteAsync(
                    "UPDATE Student SET GroupNumber = NULL, IsGroupLeader = 0 WHERE GroupNumber = @Gid",
                    new { Gid = group }, tran);

                await conn.ExecuteAsync(
                    "DELETE FROM [Group] WHERE GroupID = @Gid",
                    new { Gid = group }, tran);

                tran.Commit();
                TempData["Msg"] = "گروه با موفقیت حذف شد.";
            }
            catch (Exception ex)
            {
                tran.Rollback();
                TempData["Err"] = "خطا در حذف گروه: " + ex.Message;
            }
            return RedirectToAction("ResidenceInfo");
        }

    }
}
using Dapper;
using DormWeb.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Data;

namespace DormWeb.Controllers
{
    public class SupervisorController : Controller
    {
        private readonly string _connStr;

        public SupervisorController(IConfiguration config)
        {
            _connStr = config.GetConnectionString("Default")!;
        }

        private IActionResult CheckRole()
        {
            if (HttpContext.Session.GetString("UserRole") != "سرپرست")
                return RedirectToAction("Login", "Auth");
            return null!;
        }

        private int GetSupervisorId() => HttpContext.Session.GetInt32("UserId") ?? 0;

        private async Task SetInfo()
        {
            var userId = GetSupervisorId();
            using var conn = new SqlConnection(_connStr);
            var info = await conn.QueryFirstOrDefaultAsync<dynamic>(
                "SELECT FirstName, LastName, PersonnelID, UserRole FROM Staff WHERE PersonnelID = @Id", new { Id = userId });
            ViewBag.GuardFirstName = info?.FirstName ?? "سرپرست";
            ViewBag.GuardLastName = info?.LastName ?? "";
            ViewBag.GuardPersonnelID = info?.PersonnelID ?? 0;
            ViewBag.GuardRole = info?.UserRole ?? "سرپرست";
        }

        private async Task<List<int>> GetManagedBlockIds()
        {
            using var conn = new SqlConnection(_connStr);
            var blocks = await conn.QueryAsync<int>(
                "SELECT BlockID FROM ManagesBlock WHERE ManagerID = @Id", new { Id = GetSupervisorId() });
            return blocks.ToList();
        }

        public async Task<IActionResult> Index()
        {
            var roleCheck = CheckRole();
            if (roleCheck != null) return roleCheck;
            await SetInfo();

            var blockIds = await GetManagedBlockIds();
            using var conn = new SqlConnection(_connStr);
            var blocks = await conn.QueryAsync<SupervisorBlockItem>(@"
                SELECT b.BlockID, b.BName, d.DormitoryName, b.NumberOfFloors,
                    (SELECT COUNT(*) FROM Room r WHERE r.BlockID = b.BlockID) AS TotalRooms,
                    (SELECT COUNT(*) FROM dbo.GetAvailableRooms(NULL, b.BlockID)) AS AvailableRooms
                FROM Block b JOIN Dormitory d ON b.DormID = d.DormID
                WHERE b.BlockID IN @Ids", new { Ids = blockIds });

            return View(blocks);
        }

        public async Task<IActionResult> Profile()
        {
            var roleCheck = CheckRole();
            if (roleCheck != null) return roleCheck;
            await SetInfo();

            using var conn = new SqlConnection(_connStr);
            var info = await conn.QueryFirstOrDefaultAsync<StaffItem>(
                "SELECT * FROM Staff WHERE PersonnelID = @Id", new { Id = GetSupervisorId() });
            return View(info);
        }

        [HttpPost]
        public async Task<IActionResult> Profile(string MobilePhoneNumber, string OrganizationalEmail, string WorkShift, string Workplace)
        {
            using var conn = new SqlConnection(_connStr);
            await conn.ExecuteAsync("UPDATE Staff SET MobilePhoneNumber=@MobilePhoneNumber, OrganizationalEmail=@OrganizationalEmail, WorkShift=@WorkShift, Workplace=@Workplace WHERE PersonnelID=@Id",
                new { MobilePhoneNumber, OrganizationalEmail, WorkShift, Workplace, Id = GetSupervisorId() });
            ViewBag.SuccessMessage = "اطلاعات به‌روزرسانی شد.";
            return await Profile();
        }

        public async Task<IActionResult> Students(string? search, int page = 1)
        {
            var roleCheck = CheckRole();
            if (roleCheck != null) return roleCheck;
            await SetInfo();

            var blockIds = await GetManagedBlockIds();
            using var conn = new SqlConnection(_connStr);
            string where = "WHERE g.RoomID IS NOT NULL AND r.BlockID IN @Ids";
            if (!string.IsNullOrEmpty(search))
                where += " AND (CAST(s.StudentID AS NVARCHAR) LIKE @Search OR s.FirstName LIKE @Search OR s.LastName LIKE @Search)";

            int total = await conn.ExecuteScalarAsync<int>(
                $"SELECT COUNT(*) FROM Student s JOIN [Group] g ON s.GroupNumber = g.GroupID JOIN Room r ON g.RoomID = r.RoomID {where}",
                new { Search = $"%{search}%", Ids = blockIds });

            var list = await conn.QueryAsync<SupervisorStudentItem>(
                $"SELECT s.StudentID, s.FirstName, s.LastName, s.MobilePhoneNumber, r.RoomID, r.NameRoom AS RoomName, b.BName AS BlockName FROM Student s JOIN [Group] g ON s.GroupNumber = g.GroupID JOIN Room r ON g.RoomID = r.RoomID JOIN Block b ON r.BlockID = b.BlockID {where} ORDER BY s.StudentID OFFSET @Off ROWS FETCH NEXT 5 ROWS ONLY",
                new { Search = $"%{search}%", Ids = blockIds, Off = (page - 1) * 5 });

            ViewBag.Search = search; ViewBag.Page = page;
            ViewBag.TotalPages = (int)Math.Ceiling((double)total / 5); ViewBag.TotalCount = total;
            ViewBag.Blocks = await conn.QueryAsync<BlockItem>("SELECT b.*, d.DormitoryName FROM Block b JOIN Dormitory d ON b.DormID = d.DormID WHERE b.BlockID IN @Ids", new { Ids = blockIds });
            return View(list);
        }

        [HttpPost]
        public async Task<IActionResult> AddStudentToRoom(int studentId, string roomName)
        {
            using var conn = new SqlConnection(_connStr);

            var roomId = await conn.QueryFirstOrDefaultAsync<int?>(
                "SELECT RoomID FROM Room WHERE NameRoom = @Name", new { Name = roomName });

            if (roomId == null)
            {
                TempData["Err"] = "اتاقی با این نام یافت نشد.";
                return RedirectToAction("Students");
            }

            var blockIds = await GetManagedBlockIds();
            var belongs = await conn.QueryFirstOrDefaultAsync<bool>(
                "SELECT 1 FROM Room r JOIN Block b ON r.BlockID = b.BlockID WHERE r.RoomID = @Rid AND b.BlockID IN @Ids",
                new { Rid = roomId, Ids = blockIds });
            if (!belongs)
            {
                TempData["Err"] = "این اتاق تحت نظارت شما نیست.";
                return RedirectToAction("Students");
            }

            try
            {
                await conn.ExecuteAsync("AddStudentToRoomGroup",
                    new { StudentID = studentId, RoomID = roomId },
                    commandType: CommandType.StoredProcedure);
                TempData["Msg"] = "دانشجو اضافه شد.";
            }
            catch (SqlException ex) { TempData["Err"] = ex.Message; }
            return RedirectToAction("Students");
        }

        [HttpPost]
        public async Task<IActionResult> RemoveStudentFromRoom(int studentId, string roomName)
        {
            using var conn = new SqlConnection(_connStr);
            var roomId = await conn.QueryFirstOrDefaultAsync<int?>(
                "SELECT RoomID FROM Room WHERE NameRoom = @Name", new { Name = roomName });

            if (roomId == null)
            {
                TempData["Err"] = "اتاقی با این نام یافت نشد.";
                return RedirectToAction("Students");
            }

            var blockIds = await GetManagedBlockIds();
            var belongs = await conn.QueryFirstOrDefaultAsync<bool>(
                "SELECT 1 FROM Room r JOIN Block b ON r.BlockID = b.BlockID WHERE r.RoomID = @Rid AND b.BlockID IN @Ids",
                new { Rid = roomId, Ids = blockIds });
            if (!belongs)
            {
                TempData["Err"] = "این اتاق تحت نظارت شما نیست.";
                return RedirectToAction("Students");
            }

            try
            {
                await conn.ExecuteAsync("RemoveStudentFromRoomGroup",
                    new { StudentID = studentId, RoomID = roomId },
                    commandType: CommandType.StoredProcedure);
                TempData["Msg"] = "دانشجو حذف شد.";
            }
            catch (SqlException ex) { TempData["Err"] = ex.Message; }
            return RedirectToAction("Students");
        }

        public async Task<IActionResult> Payments(string? search, int page = 1, string? statusFilter = null)
        {
            var roleCheck = CheckRole();
            if (roleCheck != null) return roleCheck;
            await SetInfo();

            var blockIds = await GetManagedBlockIds();
            using var conn = new SqlConnection(_connStr);
            string where = "WHERE s.GroupNumber IS NOT NULL AND r.BlockID IN @Ids";
            if (!string.IsNullOrEmpty(search))
                where += " AND (CAST(p.StudentID AS NVARCHAR) LIKE @Search OR s.FirstName LIKE @Search)";
            if (!string.IsNullOrEmpty(statusFilter))
                where += " AND p.PaymentStatus = @Status";

            int total = await conn.ExecuteScalarAsync<int>(
                $"SELECT COUNT(*) FROM Payment p JOIN Student s ON p.StudentID = s.StudentID JOIN [Group] g ON s.GroupNumber = g.GroupID JOIN Room r ON g.RoomID = r.RoomID {where}",
                new { Search = $"%{search}%", Ids = blockIds, Status = statusFilter });

            var list = await conn.QueryAsync<SupervisorPaymentItem>(
                $"SELECT p.TransactionID, p.StudentID, s.FirstName + ' ' + s.LastName AS StudentName, p.Title, p.Date, p.PaymentStatus FROM Payment p JOIN Student s ON p.StudentID = s.StudentID JOIN [Group] g ON s.GroupNumber = g.GroupID JOIN Room r ON g.RoomID = r.RoomID {where} ORDER BY p.Date DESC OFFSET @Off ROWS FETCH NEXT 5 ROWS ONLY",
                new { Search = $"%{search}%", Ids = blockIds, Status = statusFilter, Off = (page - 1) * 5 });

            ViewBag.Search = search; ViewBag.Page = page; ViewBag.StatusFilter = statusFilter;
            ViewBag.TotalPages = (int)Math.Ceiling((double)total / 5); ViewBag.TotalCount = total;
            return View(list);
        }

        public async Task<IActionResult> Announcements(int page = 1)
        {
            var roleCheck = CheckRole();
            if (roleCheck != null) return roleCheck;
            await SetInfo();

            var blockIds = await GetManagedBlockIds();
            using var conn = new SqlConnection(_connStr);

            ViewBag.Blocks = await conn.QueryAsync<BlockItem>(
                "SELECT b.*, d.DormitoryName FROM Block b JOIN Dormitory d ON b.DormID = d.DormID WHERE b.BlockID IN @Ids",
                new { Ids = blockIds });

            int total = await conn.ExecuteScalarAsync<int>(@"
        SELECT COUNT(DISTINCT a.AnnouncementID)
        FROM Announcement a
        WHERE a.AnnouncementID IN (
            SELECT ab.AnnouncementID FROM AnnToBlock ab WHERE ab.BlockID IN @Ids
            UNION
            SELECT ast.AnnouncementID FROM AnnToSTU ast
            JOIN Student s ON ast.STUID = s.StudentID
            JOIN [Group] g ON s.GroupNumber = g.GroupID
            JOIN Room r ON g.RoomID = r.RoomID
            WHERE r.BlockID IN @Ids
        )",
                new { Ids = blockIds });

            var list = await conn.QueryAsync<AnnouncementItem>(@"
        SELECT DISTINCT a.*
        FROM Announcement a
        WHERE a.AnnouncementID IN (
            SELECT ab.AnnouncementID FROM AnnToBlock ab WHERE ab.BlockID IN @Ids
            UNION
            SELECT ast.AnnouncementID FROM AnnToSTU ast
            JOIN Student s ON ast.STUID = s.StudentID
            JOIN [Group] g ON s.GroupNumber = g.GroupID
            JOIN Room r ON g.RoomID = r.RoomID
            WHERE r.BlockID IN @Ids
        )
        ORDER BY a.Date DESC
        OFFSET @Off ROWS FETCH NEXT 5 ROWS ONLY",
                new { Ids = blockIds, Off = (page - 1) * 5 });

            ViewBag.Page = page;
            ViewBag.TotalPages = (int)Math.Ceiling((double)total / 5);
            ViewBag.TotalCount = total;

            return View(list);
        }

        [HttpPost]
        public async Task<IActionResult> AddAnnouncement(string Title, string Description, string DeliveryMethod, DateTime EXPDate, int? BlockID, int? StudentID)
        {
            using var conn = new SqlConnection(_connStr);
            await conn.OpenAsync();
            using var tran = conn.BeginTransaction();
            try
            {
                var annId = await conn.ExecuteScalarAsync<int>(
                    "INSERT INTO Announcement (Title, Description, Date, Status, DeliveryMethod, EXPDate, ManagerID) OUTPUT INSERTED.AnnouncementID VALUES (@Title, @Description, GETDATE(), 1, @DeliveryMethod, @EXPDate, @ManagerID)",
                    new { Title, Description, DeliveryMethod, EXPDate, ManagerID = GetSupervisorId() }, tran);

                if (BlockID.HasValue)
                    await conn.ExecuteAsync("INSERT INTO AnnToBlock VALUES (@AnnID, @BlockID)", new { AnnID = annId, BlockID = BlockID }, tran);

                if (StudentID.HasValue)
                    await conn.ExecuteAsync("INSERT INTO AnnToSTU VALUES (@AnnID, @STUID)", new { AnnID = annId, STUID = StudentID }, tran);

                tran.Commit();
                TempData["Msg"] = "اطلاعیه ثبت شد.";
            }
            catch { tran.Rollback(); TempData["Err"] = "خطا"; }
            return RedirectToAction("Announcements");
        }

        [HttpPost]
        public async Task<IActionResult> DeleteAnnouncement(int id)
        {
            using var conn = new SqlConnection(_connStr);
            await conn.ExecuteAsync("DELETE FROM Announcement WHERE AnnouncementID = @Id", new { Id = id });
            TempData["Msg"] = "اطلاعیه حذف شد.";
            return RedirectToAction("Announcements");
        }

        public async Task<IActionResult> Reports(string? search, int page = 1)
        {
            var roleCheck = CheckRole();
            if (roleCheck != null) return roleCheck;
            await SetInfo();

            var blockIds = await GetManagedBlockIds();
            using var conn = new SqlConnection(_connStr);

            string where = "WHERE b.BlockID IN @Ids";
            if (!string.IsNullOrEmpty(search))
                where += " AND (r.Title LIKE @Search OR r.Target LIKE @Search OR CAST(r.StudentID AS NVARCHAR) LIKE @Search)";

            int total = await conn.ExecuteScalarAsync<int>(
                $"SELECT COUNT(*) FROM Report r JOIN Room ro ON r.Target = ro.NameRoom JOIN Block b ON ro.BlockID = b.BlockID {where}",
                new { Search = $"%{search}%", Ids = blockIds });

            var list = await conn.QueryAsync<SupervisorReportItem>(
                $"SELECT r.RID, r.Title, r.Type, r.Date, r.Target, r.Description, s.FirstName + ' ' + s.LastName AS StudentName FROM Report r JOIN Student s ON r.StudentID = s.StudentID JOIN Room ro ON r.Target = ro.NameRoom JOIN Block b ON ro.BlockID = b.BlockID {where} ORDER BY r.Date DESC OFFSET @Off ROWS FETCH NEXT 10 ROWS ONLY",
                new { Search = $"%{search}%", Ids = blockIds, Off = (page - 1) * 10 });

            ViewBag.Search = search; ViewBag.Page = page;
            ViewBag.TotalPages = (int)Math.Ceiling((double)total / 10); ViewBag.TotalCount = total;
            return View(list);
        }

        [HttpPost]
        public async Task<IActionResult> DeleteReport(int rid)
        {
            using var conn = new SqlConnection(_connStr);
            await conn.ExecuteAsync("DELETE FROM Report WHERE RID = @Id", new { Id = rid });
            TempData["Msg"] = "گزارش حذف شد.";
            return RedirectToAction("Reports");
        }

        public async Task<IActionResult> TimeSlots(int page = 1)
        {
            var roleCheck = CheckRole();
            if (roleCheck != null) return roleCheck;
            await SetInfo();

            var blockIds = await GetManagedBlockIds();
            using var conn = new SqlConnection(_connStr);

            int total = await conn.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM TimeSlot WHERE BlockID IN @Ids", new { Ids = blockIds });

            var list = await conn.QueryAsync<TimeSlotItem>(
                @"SELECT t.*, b.BName AS BlockName FROM TimeSlot t 
                  JOIN Block b ON t.BlockID = b.BlockID 
                  WHERE t.BlockID IN @Ids 
                  ORDER BY t.SelectionStartDate DESC 
                  OFFSET @Off ROWS FETCH NEXT 5 ROWS ONLY",
                new { Ids = blockIds, Off = (page - 1) * 5 });

            ViewBag.Page = page;
            ViewBag.TotalPages = (int)Math.Ceiling((double)total / 5);
            ViewBag.TotalCount = total;
            ViewBag.Blocks = await conn.QueryAsync<BlockItem>(
                "SELECT b.*, d.DormitoryName FROM Block b JOIN Dormitory d ON b.DormID = d.DormID WHERE b.BlockID IN @Ids",
                new { Ids = blockIds });

            return View(list);
        }

        [HttpPost]
        public async Task<IActionResult> AddTimeSlot(int BlockID, DateTime GroupingStartDate, DateTime SelectionStartDate, DateTime SelectionEndDate, int Tuition)
        {
            using var conn = new SqlConnection(_connStr);
            await conn.ExecuteAsync("UPDATE TimeSlot SET IsActive = 0 WHERE BlockID = @BlockID AND IsActive = 1", new { BlockID });
            await conn.ExecuteAsync(
                "INSERT INTO TimeSlot (BlockID, GroupingStartDate, SelectionStartDate, SelectionEndDate, Tuition, IsActive) VALUES (@BlockID, @GroupingStartDate, @SelectionStartDate, @SelectionEndDate, @Tuition, 1)",
                new { BlockID, GroupingStartDate, SelectionStartDate, SelectionEndDate, Tuition });
            TempData["Msg"] = "بازهٔ جدید با موفقیت ثبت شد.";
            return RedirectToAction("TimeSlots");
        }

        [HttpPost]
        public async Task<IActionResult> ToggleTimeSlot(int id)
        {
            using var conn = new SqlConnection(_connStr);
            await conn.ExecuteAsync("UPDATE TimeSlot SET IsActive = CASE WHEN IsActive = 1 THEN 0 ELSE 1 END WHERE TID = @Id", new { Id = id });
            return RedirectToAction("TimeSlots");
        }

        [HttpPost]
        public async Task<IActionResult> DeleteTimeSlot(int id)
        {
            using var conn = new SqlConnection(_connStr);
            await conn.ExecuteAsync("DELETE FROM TimeSlot WHERE TID = @Id", new { Id = id });
            return RedirectToAction("TimeSlots");
        }

        [HttpGet]
        public async Task<IActionResult> BlockRooms(int blockId)
        {
            var roleCheck = CheckRole();
            if (roleCheck != null) return roleCheck;
            await SetInfo();

            if (blockId <= 0)
            {
                TempData["Err"] = "شناسه بلوک نامعتبر است.";
                return RedirectToAction("Index");
            }

            var blockIds = await GetManagedBlockIds();
            if (!blockIds.Contains(blockId))
            {
                TempData["Err"] = $"بلوک {blockId} جزو بلوک‌های تحت نظارت شما نیست.";
                return RedirectToAction("Index");
            }

            using var conn = new SqlConnection(_connStr);
            var block = await conn.QueryFirstOrDefaultAsync<dynamic>(
                "SELECT BName FROM Block WHERE BlockID = @Id", new { Id = blockId });
            ViewBag.BlockName = block?.BName ?? "بلوک";

            var rooms = await conn.QueryAsync<RoomViewModel>(@"
                SELECT r.RoomID, r.NameRoom, r.Capacity,
                       r.Capacity - ISNULL((SELECT COUNT(*) FROM Student s 
                                            JOIN [Group] g ON s.GroupNumber = g.GroupID 
                                            WHERE g.RoomID = r.RoomID), 0) AS AvailableCapacity,
                       b.BlockID, b.BName AS BlockName, d.DormID, d.DormitoryName, d.DormitoryType
                FROM Room r
                INNER JOIN Block b ON r.BlockID = b.BlockID
                INNER JOIN Dormitory d ON b.DormID = d.DormID
                WHERE b.BlockID = @BlockID
                  AND r.Capacity - ISNULL((SELECT COUNT(*) FROM Student s 
                                           JOIN [Group] g ON s.GroupNumber = g.GroupID 
                                           WHERE g.RoomID = r.RoomID), 0) > 0
                  AND r.RoomCondition = N'قابل سکونت'",
                new { BlockID = blockId });

            return View(rooms);
        }

        public async Task<IActionResult> Groups(string? search, int page = 1)
        {
            var roleCheck = CheckRole();
            if (roleCheck != null) return roleCheck;
            await SetInfo();

            var blockIds = await GetManagedBlockIds();
            using var conn = new SqlConnection(_connStr);

            string where = "WHERE r.BlockID IN @Ids";
            object parameters = new { Search = $"%{search}%", Ids = blockIds, Off = (page - 1) * 5 };

            if (!string.IsNullOrEmpty(search))
                where += " AND (CAST(g.GroupID AS NVARCHAR) LIKE @Search OR s.LeaderName LIKE @Search OR CAST(s.LeaderID AS NVARCHAR) LIKE @Search)";

            string countSql = $@"
                SELECT COUNT(*) FROM [Group] g 
                JOIN Room r ON g.RoomID = r.RoomID 
                LEFT JOIN (SELECT GroupNumber, MIN(StudentID) AS LeaderID, MIN(FirstName + ' ' + LastName) AS LeaderName FROM Student WHERE IsGroupLeader = 1 GROUP BY GroupNumber) s ON g.GroupID = s.GroupNumber
                {where}";

            int total = await conn.ExecuteScalarAsync<int>(countSql, parameters);

            string dataSql = $@"
                SELECT g.GroupID, g.GroupType, g.RoomID, r.NameRoom AS RoomName, b.BName AS BlockName,
                    (SELECT COUNT(*) FROM Student WHERE GroupNumber = g.GroupID) AS MemberCount,
                    s.LeaderID, s.LeaderName
                FROM [Group] g
                JOIN Room r ON g.RoomID = r.RoomID
                JOIN Block b ON r.BlockID = b.BlockID
                LEFT JOIN (SELECT GroupNumber, MIN(StudentID) AS LeaderID, MIN(FirstName + ' ' + LastName) AS LeaderName FROM Student WHERE IsGroupLeader = 1 GROUP BY GroupNumber) s ON g.GroupID = s.GroupNumber
                {where}
                ORDER BY g.GroupID
                OFFSET @Off ROWS FETCH NEXT 5 ROWS ONLY";

            var list = await conn.QueryAsync<SupervisorGroupItem>(dataSql, parameters);

            ViewBag.Search = search;
            ViewBag.Page = page;
            ViewBag.TotalPages = (int)Math.Ceiling((double)total / 5);
            ViewBag.TotalCount = total;
            ViewBag.Blocks = await conn.QueryAsync<BlockItem>(
                "SELECT b.*, d.DormitoryName FROM Block b JOIN Dormitory d ON b.DormID = d.DormID WHERE b.BlockID IN @Ids",
                new { Ids = blockIds });

            return View(list);
        }

        [HttpPost]
        public async Task<IActionResult> CreateGroup(string GroupType, int BlockID, int? RoomID, List<int>? StudentIDs, int? LeaderStudentID)
        {
            using var conn = new SqlConnection(_connStr);
            await conn.OpenAsync();
            using var tran = conn.BeginTransaction();
            try
            {
                if (RoomID.HasValue && RoomID.Value > 0)
                {
                    var roomStatus = await conn.QueryFirstOrDefaultAsync<string>("SELECT Status FROM Room WHERE RoomID = @Id", new { Id = RoomID }, tran);
                    if (roomStatus != "خالی")
                    {
                        tran.Rollback();
                        TempData["Err"] = "اتاق انتخاب‌شده خالی نیست.";
                        return RedirectToAction("Groups");
                    }
                }

                int groupId = await conn.ExecuteScalarAsync<int>(
                    "INSERT INTO [Group] (RoomID, GroupType) OUTPUT INSERTED.GroupID VALUES (@RoomID, @GroupType)",
                    new { RoomID = RoomID > 0 ? RoomID : (int?)null, GroupType }, tran);

                if (StudentIDs != null && StudentIDs.Any())
                {
                    foreach (var sid in StudentIDs)
                    {
                        await conn.ExecuteAsync(
                            "UPDATE Student SET GroupNumber = @Gid, IsGroupLeader = 0 WHERE StudentID = @Sid",
                            new { Gid = groupId, Sid = sid }, tran);
                    }
                }

                if (LeaderStudentID.HasValue && StudentIDs != null && StudentIDs.Contains(LeaderStudentID.Value))
                {
                    await conn.ExecuteAsync(
                        "UPDATE Student SET IsGroupLeader = 1 WHERE StudentID = @Sid AND GroupNumber = @Gid",
                        new { Sid = LeaderStudentID.Value, Gid = groupId }, tran);
                }

                if (RoomID.HasValue && RoomID.Value > 0)
                {
                    await conn.ExecuteAsync("UPDATE Room SET Status = N'پر' WHERE RoomID = @Id", new { Id = RoomID }, tran);
                }

                tran.Commit();
                TempData["Msg"] = "گروه جدید با موفقیت ایجاد شد.";
            }
            catch (Exception ex)
            {
                tran.Rollback();
                TempData["Err"] = ex.Message;
            }
            return RedirectToAction("Groups");
        }

        [HttpPost]
        public async Task<IActionResult> DeleteGroup(int groupId)
        {
            using var conn = new SqlConnection(_connStr);
            await conn.ExecuteAsync("DELETE FROM [Group] WHERE GroupID = @Id", new { Id = groupId });
            TempData["Msg"] = "گروه حذف شد.";
            return RedirectToAction("Groups");
        }

        [HttpGet]
        public async Task<IActionResult> GetRoomsByBlock(int blockId)
        {
            using var conn = new SqlConnection(_connStr);
            var rooms = await conn.QueryAsync<dynamic>(
                "SELECT RoomID, NameRoom, Capacity FROM Room WHERE BlockID = @Id AND Status = N'خالی' AND RoomCondition = N'قابل سکونت'",
                new { Id = blockId });
            return Json(rooms);
        }

        [HttpGet]
        public async Task<IActionResult> GetAvailableRoomsForSupervisor()
        {
            var blockIds = await GetManagedBlockIds();
            using var conn = new SqlConnection(_connStr);
            var rooms = await conn.QueryAsync<dynamic>(
                "SELECT r.RoomID, r.Capacity, b.BName AS BlockName FROM Room r JOIN Block b ON r.BlockID = b.BlockID WHERE b.BlockID IN @Ids AND r.Status = N'خالی' AND r.RoomCondition = N'قابل سکونت'",
                new { Ids = blockIds });
            return Json(rooms);
        }

        [HttpPost]
        public async Task<IActionResult> SendPaymentReminders()
        {
            var roleCheck = CheckRole();
            if (roleCheck != null) return roleCheck;

            using var conn = new SqlConnection(_connStr);
            try
            {
                await conn.ExecuteAsync("NotifyUnpaidStudents",
                    new { ManagerID = GetSupervisorId() },
                    commandType: CommandType.StoredProcedure);
                TempData["Msg"] = "یادآوری‌های پرداخت ارسال شد.";
            }
            catch (SqlException ex)
            {
                TempData["Err"] = ex.Message;
            }
            return RedirectToAction("Announcements");
        }
        [HttpGet]
        public async Task<IActionResult> SearchStudent(int studentId)
        {
            var blockIds = await GetManagedBlockIds();
            using var conn = new SqlConnection(_connStr);
            var student = await conn.QueryFirstOrDefaultAsync<dynamic>(
                @"SELECT s.StudentID, s.FirstName + ' ' + s.LastName AS Name 
          FROM Student s
          JOIN [Group] g ON s.GroupNumber = g.GroupID
          JOIN Room r ON g.RoomID = r.RoomID
          WHERE s.StudentID = @Id AND r.BlockID IN @Ids",
                new { Id = studentId, Ids = blockIds });
            if (student == null) return Json(new { found = false });
            return Json(new { found = true, name = (string)student.Name });
        }
    }
}
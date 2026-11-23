using Dapper;
using DormWeb.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Data;

namespace DormWeb.Controllers
{
    public class ManagerController : Controller
    {
        private readonly string _connStr;

        public ManagerController(IConfiguration config)
        {
            _connStr = config.GetConnectionString("Default")!;
        }

        private string HashPassword(string password)
        {
            using var sha256 = System.Security.Cryptography.SHA256.Create();
            var bytes = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(password));
            return BitConverter.ToString(bytes).Replace("-", "").ToLower();
        }

        private IActionResult CheckRole()
        {
            if (HttpContext.Session.GetString("UserRole") != "مدیر")
                return RedirectToAction("Login", "Auth");
            return null!;
        }

        private async Task SetGuardInfo()
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            using var conn = new SqlConnection(_connStr);
            var info = await conn.QueryFirstOrDefaultAsync<dynamic>(
                "SELECT FirstName, LastName, PersonnelID, UserRole FROM Staff WHERE PersonnelID = @Id",
                new { Id = userId });
            ViewBag.GuardFirstName = info?.FirstName ?? "مدیر";
            ViewBag.GuardLastName = info?.LastName ?? "";
            ViewBag.GuardPersonnelID = info?.PersonnelID ?? 0;
            ViewBag.GuardRole = info?.UserRole ?? "مدیر";
        }

        public async Task<IActionResult> Index()
        {
            var roleCheck = CheckRole();
            if (roleCheck != null) return roleCheck;
            await SetGuardInfo();

            using var conn = new SqlConnection(_connStr);

            var stats = await conn.QueryFirstOrDefaultAsync<DashboardStats>(@"
                SELECT 
                    (SELECT COUNT(*) FROM Student) AS TotalStudents,
                    (SELECT COUNT(*) FROM Block) AS TotalBlocks,
                    (SELECT COUNT(*) FROM Dormitory) AS TotalDormitories");

            var announcements = await conn.QueryAsync<AnnouncementItem>(
                "SELECT TOP 5 * FROM Announcement WHERE Status = 1 ORDER BY Date DESC");

            ViewBag.Stats = stats;
            return View(announcements);
        }

        public async Task<IActionResult> Students(string? search, int page = 1)
        {
            var roleCheck = CheckRole();
            if (roleCheck != null) return roleCheck;
            await SetGuardInfo();

            using var conn = new SqlConnection(_connStr);
            string where = "";
            if (!string.IsNullOrEmpty(search))
                where = "WHERE CAST(StudentID AS NVARCHAR) LIKE @Search OR FirstName LIKE @Search OR LastName LIKE @Search";

            int total = await conn.ExecuteScalarAsync<int>($"SELECT COUNT(*) FROM Student {where}", new { Search = $"%{search}%" });
            var list = await conn.QueryAsync<StudentListItem>(
                $"SELECT StudentID, FirstName, LastName, NationalID, MobilePhoneNumber, Major, GroupNumber FROM Student {where} ORDER BY StudentID OFFSET @Off ROWS FETCH NEXT 5 ROWS ONLY",
                new { Search = $"%{search}%", Off = (page - 1) * 5 });

            ViewBag.Search = search;
            ViewBag.Page = page;
            ViewBag.TotalPages = (int)Math.Ceiling((double)total / 5);
            ViewBag.TotalCount = total;
            return View(list);
        }

        [HttpGet]
        public async Task<IActionResult> AddStudent()
        {
            var roleCheck = CheckRole();
            if (roleCheck != null) return roleCheck;
            await SetGuardInfo();
            return View(new StudentDetail { BirthDate = DateTime.Today });
        }

        [HttpGet]
        public async Task<IActionResult> SearchStudent(int studentId)
        {
            using var conn = new SqlConnection(_connStr);
            var student = await conn.QueryFirstOrDefaultAsync<dynamic>(
                "SELECT FirstName, LastName FROM Student WHERE StudentID = @Id", new { Id = studentId });
            if (student == null) return Json(new { found = false });
            return Json(new { found = true, name = $"{student.FirstName} {student.LastName}" });
        }

        [HttpPost]
        public async Task<IActionResult> AddStudent(StudentDetail model, List<ParentViewModel> Parents)
        {
            var roleCheck = CheckRole();
            if (roleCheck != null) return roleCheck;
            await SetGuardInfo();

            if (!string.IsNullOrEmpty(model.Password))
            {
                model.Password = HashPassword(model.Password);
            }

            using var conn = new SqlConnection(_connStr);
            await conn.OpenAsync();
            using var tran = conn.BeginTransaction();

            try
            {
                await conn.ExecuteAsync(@"
                    INSERT INTO Student (StudentID, FirstName, LastName, NationalID, Password, Gender, BirthDate, MobilePhoneNumber, OrganizationalEmail, Major, AcademicDegree, EmergencyContactNumber)
                    VALUES (@StudentID, @FirstName, @LastName, @NationalID, @Password, @Gender, @BirthDate, @MobilePhoneNumber, @OrganizationalEmail, @Major, @AcademicDegree, @EmergencyContactNumber)",
                    model, tran);

                if (Parents != null)
                {
                    foreach (var p in Parents)
                    {
                        await conn.ExecuteAsync(@"
                            INSERT INTO Parent (StudentID, NationalID, PhoneNum, FName, LName, City, Province, SpecAddress, PostID)
                            VALUES (@StudentID, @NationalID, @PhoneNum, @FName, @LName, @City, @Province, @SpecAddress, @PostID)",
                            new { model.StudentID, p.NationalID, p.PhoneNum, p.FName, p.LName, p.City, p.Province, p.SpecAddress, p.PostID }, tran);
                    }
                }

                tran.Commit();
                ViewBag.Success = "دانشجو با موفقیت ثبت شد.";
            }
            catch (Exception ex)
            {
                tran.Rollback();
                ViewBag.Error = ex.Message;
            }

            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> EditStudent(int id)
        {
            var roleCheck = CheckRole();
            if (roleCheck != null) return roleCheck;
            await SetGuardInfo();

            using var conn = new SqlConnection(_connStr);
            var student = await conn.QueryFirstOrDefaultAsync<StudentDetail>(
                "SELECT * FROM Student WHERE StudentID = @Id", new { Id = id });
            if (student == null) return NotFound();

            student.Parents = (await conn.QueryAsync<ParentViewModel>(
                "SELECT * FROM Parent WHERE StudentID = @Id", new { Id = id })).ToList();

            return View(student);
        }

        [HttpPost]
        public async Task<IActionResult> EditStudent(StudentDetail model, List<ParentViewModel> Parents)
        {
            var roleCheck = CheckRole();
            if (roleCheck != null) return roleCheck;
            await SetGuardInfo();

            using var conn = new SqlConnection(_connStr);
            await conn.OpenAsync();
            using var tran = conn.BeginTransaction();

            try
            {
                string sql = "UPDATE Student SET FirstName=@FirstName, LastName=@LastName, NationalID=@NationalID, Gender=@Gender, BirthDate=@BirthDate, MobilePhoneNumber=@MobilePhoneNumber, OrganizationalEmail=@OrganizationalEmail, Major=@Major, AcademicDegree=@AcademicDegree, EmergencyContactNumber=@EmergencyContactNumber";

                if (!string.IsNullOrEmpty(model.Password))
                {
                    model.Password = HashPassword(model.Password);
                    sql += ", Password=@Password";
                }

                sql += " WHERE StudentID=@StudentID";

                await conn.ExecuteAsync(sql, model, tran);

                await conn.ExecuteAsync("DELETE FROM Parent WHERE StudentID = @StudentID", new { model.StudentID }, tran);

                if (Parents != null)
                {
                    foreach (var p in Parents)
                    {
                        await conn.ExecuteAsync(@"
                            INSERT INTO Parent (StudentID, NationalID, PhoneNum, FName, LName, City, Province, SpecAddress, PostID)
                            VALUES (@StudentID, @NationalID, @PhoneNum, @FName, @LName, @City, @Province, @SpecAddress, @PostID)",
                            new { model.StudentID, p.NationalID, p.PhoneNum, p.FName, p.LName, p.City, p.Province, p.SpecAddress, p.PostID }, tran);
                    }
                }

                tran.Commit();
                ViewBag.Success = "تغییرات با موفقیت ذخیره شد.";
            }
            catch (Exception ex)
            {
                tran.Rollback();
                ViewBag.Error = ex.Message;
            }

            model.Parents = Parents;
            return View(model);
        }

        [HttpPost]
        public async Task<IActionResult> DeleteStudent(int id)
        {
            var roleCheck = CheckRole();
            if (roleCheck != null) return roleCheck;

            using var conn = new SqlConnection(_connStr);
            await conn.ExecuteAsync("DELETE FROM Student WHERE StudentID = @Id", new { Id = id });
            return RedirectToAction("Students");
        }

        public async Task<IActionResult> ReserveRoom()
        {
            var roleCheck = CheckRole();
            if (roleCheck != null) return roleCheck;
            await SetGuardInfo();

            using var conn = new SqlConnection(_connStr);
            var rooms = await conn.QueryAsync<RoomViewModel>(
                "SELECT * FROM dbo.GetAvailableRooms(NULL, NULL)");
            return View(rooms);
        }

        [HttpPost]
        public async Task<IActionResult> AddStudentToRoom(int studentId, int roomId)
        {
            using var conn = new SqlConnection(_connStr);
            try
            {
                await conn.ExecuteAsync("AddStudentToRoomGroup", new { StudentID = studentId, RoomID = roomId }, commandType: CommandType.StoredProcedure);
                TempData["Msg"] = "دانشجو به اتاق اضافه شد.";
            }
            catch (SqlException ex) { TempData["Err"] = ex.Message; }
            return RedirectToAction("ReserveRoom");
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
                return RedirectToAction("ReserveRoom");
            }

            try
            {
                await conn.ExecuteAsync("RemoveStudentFromRoomGroup",
                    new { StudentID = studentId, RoomID = roomId },
                    commandType: CommandType.StoredProcedure);
                TempData["Msg"] = "دانشجو حذف شد.";
            }
            catch (SqlException ex) { TempData["Err"] = ex.Message; }
            return RedirectToAction("ReserveRoom");
        }

        public async Task<IActionResult> Dormitories(string? search, int page = 1)
        {
            await SetGuardInfo();
            using var conn = new SqlConnection(_connStr);
            string where = "";
            if (!string.IsNullOrEmpty(search))
                where = "WHERE DormitoryName LIKE @Search OR City LIKE @Search OR Province LIKE @Search";

            int total = await conn.ExecuteScalarAsync<int>($"SELECT COUNT(*) FROM Dormitory {where}", new { Search = $"%{search}%" });
            var list = await conn.QueryAsync<DormitoryItem>(
                $"SELECT * FROM Dormitory {where} ORDER BY DormID OFFSET @Off ROWS FETCH NEXT 5 ROWS ONLY",
                new { Search = $"%{search}%", Off = (page - 1) * 5 });

            ViewBag.Search = search; ViewBag.Page = page; ViewBag.TotalPages = (int)Math.Ceiling((double)total / 5); ViewBag.TotalCount = total;
            return View(list);
        }

        [HttpPost]
        public async Task<IActionResult> AddDormitory(DormitoryItem model)
        {
            using var conn = new SqlConnection(_connStr);
            await conn.ExecuteAsync("INSERT INTO Dormitory (DormitoryName, City, Province, DormitoryType, PostID, AddressDescription) VALUES (@DormitoryName, @City, @Province, @DormitoryType, @PostID, @AddressDescription)", model);
            return RedirectToAction("Dormitories");
        }

        [HttpPost]
        public async Task<IActionResult> EditDormitory(DormitoryItem model)
        {
            using var conn = new SqlConnection(_connStr);
            await conn.ExecuteAsync("UPDATE Dormitory SET DormitoryName=@DormitoryName, City=@City, Province=@Province, DormitoryType=@DormitoryType, PostID=@PostID, AddressDescription=@AddressDescription WHERE DormID=@DormID", model);
            return RedirectToAction("Dormitories");
        }

        [HttpPost]
        public async Task<IActionResult> DeleteDormitory(int id)
        {
            using var conn = new SqlConnection(_connStr);
            await conn.ExecuteAsync("DELETE FROM Dormitory WHERE DormID = @Id", new { Id = id });
            return RedirectToAction("Dormitories");
        }

        public async Task<IActionResult> Blocks(string? search, int page = 1)
        {
            await SetGuardInfo();
            using var varConn = new SqlConnection(_connStr);
            string where = "";
            if (!string.IsNullOrEmpty(search))
                where = "WHERE b.BName LIKE @Search OR d.DormitoryName LIKE @Search";

            int total = await varConn.ExecuteScalarAsync<int>(
                $"SELECT COUNT(*) FROM Block b JOIN Dormitory d ON b.DormID = d.DormID {where}", new { Search = $"%{search}%" });
            var list = await varConn.QueryAsync<BlockItem>(
                $"SELECT b.*, d.DormitoryName FROM Block b JOIN Dormitory d ON b.DormID = d.DormID {where} ORDER BY b.BlockID OFFSET @Off ROWS FETCH NEXT 5 ROWS ONLY",
                new { Search = $"%{search}%", Off = (page - 1) * 5 });

            ViewBag.Search = search; ViewBag.Page = page; ViewBag.TotalPages = (int)Math.Ceiling((double)total / 5); ViewBag.TotalCount = total;
            ViewBag.Dormitories = await varConn.QueryAsync<DormitoryItem>("SELECT * FROM Dormitory");
            return View(list);
        }

        [HttpPost]
        public async Task<IActionResult> AddBlock(BlockItem model)
        {
            using var conn = new SqlConnection(_connStr);
            await conn.ExecuteAsync("INSERT INTO Block (BName, NumberOfFloors, TotalArea, SharedRestroomCount, StorageAvailability, ElevatorAvailability, BlockPhoneNumber, DormID) VALUES (@BName, @NumberOfFloors, @TotalArea, @SharedRestroomCount, @StorageAvailability, @ElevatorAvailability, @BlockPhoneNumber, @DormID)", model);
            return RedirectToAction("Blocks");
        }

        [HttpPost]
        public async Task<IActionResult> EditBlock(BlockItem model)
        {
            using var conn = new SqlConnection(_connStr);
            await conn.ExecuteAsync("UPDATE Block SET BName=@BName, NumberOfFloors=@NumberOfFloors, TotalArea=@TotalArea, SharedRestroomCount=@SharedRestroomCount, StorageAvailability=@StorageAvailability, ElevatorAvailability=@ElevatorAvailability, BlockPhoneNumber=@BlockPhoneNumber, DormID=@DormID WHERE BlockID=@BlockID", model);
            return RedirectToAction("Blocks");
        }

        [HttpPost]
        public async Task<IActionResult> DeleteBlock(int id)
        {
            using var conn = new SqlConnection(_connStr);
            await conn.ExecuteAsync("DELETE FROM Block WHERE BlockID = @Id", new { Id = id });
            return RedirectToAction("Blocks");
        }

        public async Task<IActionResult> Rooms(string? search, int page = 1)
        {
            await SetGuardInfo();
            using var conn = new SqlConnection(_connStr);
            string where = "";
            if (!string.IsNullOrEmpty(search))
                where = "WHERE r.NameRoom LIKE @Search OR b.BName LIKE @Search OR d.DormitoryName LIKE @Search";

            int total = await conn.ExecuteScalarAsync<int>(
                $"SELECT COUNT(*) FROM Room r JOIN Block b ON r.BlockID = b.BlockID JOIN Dormitory d ON b.DormID = d.DormID {where}", new { Search = $"%{search}%" });
            var list = await conn.QueryAsync<RoomItem>(
                $"SELECT r.*, b.BName AS BlockName FROM Room r JOIN Block b ON r.BlockID = b.BlockID JOIN Dormitory d ON b.DormID = d.DormID {where} ORDER BY r.RoomID OFFSET @Off ROWS FETCH NEXT 5 ROWS ONLY",
                new { Search = $"%{search}%", Off = (page - 1) * 5 });

            ViewBag.Search = search; ViewBag.Page = page; ViewBag.TotalPages = (int)Math.Ceiling((double)total / 5); ViewBag.TotalCount = total;
            ViewBag.Blocks = await conn.QueryAsync<BlockItem>("SELECT b.*, d.DormitoryName FROM Block b JOIN Dormitory d ON b.DormID = d.DormID");
            return View(list);
        }

        [HttpPost]
        public async Task<IActionResult> AddRoom(RoomItem model)
        {
            using var conn = new SqlConnection(_connStr);
            await conn.ExecuteAsync("INSERT INTO Room (NameRoom, InternetAccess, Capacity, Status, RoomCondition, HasBathroom, HasBalcony, BlockID) VALUES (@NameRoom, 1, @Capacity, N'خالی', @RoomCondition, @HasBathroom, @HasBalcony, @BlockID)", model);
            return RedirectToAction("Rooms");
        }

        [HttpPost]
        public async Task<IActionResult> EditRoom(RoomItem model)
        {
            using var conn = new SqlConnection(_connStr);
            await conn.ExecuteAsync("UPDATE Room SET NameRoom=@NameRoom, Capacity=@Capacity, RoomCondition=@RoomCondition, HasBathroom=@HasBathroom, HasBalcony=@HasBalcony WHERE RoomID=@RoomID", model);
            return RedirectToAction("Rooms");
        }

        [HttpPost]
        public async Task<IActionResult> DeleteRoom(int id)
        {
            using var conn = new SqlConnection(_connStr);
            await conn.ExecuteAsync("DELETE FROM Room WHERE RoomID = @Id", new { Id = id });
            return RedirectToAction("Rooms");
        }

        public async Task<IActionResult> TimeSlots(int page = 1)
        {
            await SetGuardInfo();
            using var conn = new SqlConnection(_connStr);
            int total = await conn.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM TimeSlot");
            var list = await conn.QueryAsync<TimeSlotItem>(
                @"SELECT t.*, b.BName AS BlockName FROM TimeSlot t JOIN Block b ON t.BlockID = b.BlockID 
                  ORDER BY t.SelectionStartDate DESC OFFSET @Off ROWS FETCH NEXT 5 ROWS ONLY",
                new { Off = (page - 1) * 5 });

            ViewBag.Page = page; ViewBag.TotalPages = (int)Math.Ceiling((double)total / 5); ViewBag.TotalCount = total;
            ViewBag.Blocks = await conn.QueryAsync<BlockItem>("SELECT b.*, d.DormitoryName FROM Block b JOIN Dormitory d ON b.DormID = d.DormID");
            return View(list);
        }

        [HttpPost]
        public async Task<IActionResult> AddTimeSlot(TimeSlotItem model)
        {
            using var conn = new SqlConnection(_connStr);
            await conn.ExecuteAsync("UPDATE TimeSlot SET IsActive = 0 WHERE BlockID = @BlockID AND IsActive = 1", new { model.BlockID });
            await conn.ExecuteAsync("INSERT INTO TimeSlot (BlockID, GroupingStartDate, SelectionStartDate, SelectionEndDate, Tuition, IsActive) VALUES (@BlockID, @GroupingStartDate, @SelectionStartDate, @SelectionEndDate, @Tuition, 1)", model);
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

        public async Task<IActionResult> Staffs(string? search, int page = 1)
        {
            await SetGuardInfo();
            using var conn = new SqlConnection(_connStr);
            string where = "";
            if (!string.IsNullOrEmpty(search))
                where = "WHERE FirstName LIKE @Search OR LastName LIKE @Search OR CAST(PersonnelID AS NVARCHAR) LIKE @Search";

            int total = await conn.ExecuteScalarAsync<int>($"SELECT COUNT(*) FROM Staff {where}", new { Search = $"%{search}%" });
            var list = await conn.QueryAsync<StaffItem>(
                $"SELECT * FROM Staff {where} ORDER BY PersonnelID OFFSET @Off ROWS FETCH NEXT 5 ROWS ONLY",
                new { Search = $"%{search}%", Off = (page - 1) * 5 });
            ViewBag.AllBlocks = await conn.QueryAsync<BlockItem>(
                "SELECT b.*, d.DormitoryName FROM Block b JOIN Dormitory d ON b.DormID = d.DormID");
            ViewBag.Search = search; ViewBag.Page = page; ViewBag.TotalPages = (int)Math.Ceiling((double)total / 5); ViewBag.TotalCount = total;
            return View(list);
        }

        [HttpPost]
        public async Task<IActionResult> AddStaff(StaffItem model, List<int>? BlockIDs)
        {
            if (string.IsNullOrEmpty(model.Password))
            {
                model.Password = "pass123";
            }
            model.Password = HashPassword(model.Password);

            using var conn = new SqlConnection(_connStr);
            await conn.OpenAsync();
            using var tran = conn.BeginTransaction();
            try
            {
                await conn.ExecuteAsync(
                    "INSERT INTO Staff (PersonnelID, FirstName, LastName, NationalID, Password, Gender, BirthDate, MobilePhoneNumber, OrganizationalEmail, UserRole, EmploymentStartDate, WorkShift, Salary, Workplace, BankAccountNumber) VALUES (@PersonnelID, @FirstName, @LastName, @NationalID, @Password, @Gender, GETDATE(), @MobilePhoneNumber, @OrganizationalEmail, @UserRole, GETDATE(), @WorkShift, 0, @Workplace, '')",
                    model, tran);

                if (model.UserRole == "سرپرست" && BlockIDs != null && BlockIDs.Any())
                {
                    foreach (var blockId in BlockIDs)
                    {
                        await conn.ExecuteAsync(
                            "INSERT INTO ManagesBlock (ManagerID, BlockID, Date_Started) VALUES (@Mid, @Bid, GETDATE())",
                            new { Mid = model.PersonnelID, Bid = blockId }, tran);
                    }
                }

                tran.Commit();
                TempData["Msg"] = "کارمند با موفقیت ثبت شد.";
            }
            catch (Exception ex)
            {
                tran.Rollback();
                TempData["Err"] = ex.Message;
            }
            return RedirectToAction("Staffs");
        }

        [HttpPost]
        public async Task<IActionResult> EditStaff(StaffItem model, List<int>? BlockIDs)
        {
            using var conn = new SqlConnection(_connStr);
            await conn.OpenAsync();
            using var tran = conn.BeginTransaction();
            try
            {
                string sql = "UPDATE Staff SET FirstName=@FirstName, LastName=@LastName, NationalID=@NationalID, Gender=@Gender, MobilePhoneNumber=@MobilePhoneNumber, OrganizationalEmail=@OrganizationalEmail, UserRole=@UserRole, WorkShift=@WorkShift, Workplace=@Workplace";

                if (!string.IsNullOrEmpty(model.Password))
                {
                    model.Password = HashPassword(model.Password);
                    sql += ", Password=@Password";
                }

                sql += " WHERE PersonnelID=@PersonnelID";

                await conn.ExecuteAsync(sql, model, tran);

                await conn.ExecuteAsync(
                    "DELETE FROM ManagesBlock WHERE ManagerID = @Mid", new { Mid = model.PersonnelID }, tran);

                if (model.UserRole == "سرپرست" && BlockIDs != null && BlockIDs.Any())
                {
                    foreach (var blockId in BlockIDs)
                    {
                        await conn.ExecuteAsync(
                            "INSERT INTO ManagesBlock (ManagerID, BlockID, Date_Started) VALUES (@Mid, @Bid, GETDATE())",
                            new { Mid = model.PersonnelID, Bid = blockId }, tran);
                    }
                }

                tran.Commit();
                TempData["Msg"] = "کارمند با موفقیت ویرایش شد.";
            }
            catch (Exception ex)
            {
                tran.Rollback();
                TempData["Err"] = ex.Message;
            }
            return RedirectToAction("Staffs");
        }

        [HttpPost]
        public async Task<IActionResult> DeleteStaff(int id)
        {
            using var conn = new SqlConnection(_connStr);
            await conn.ExecuteAsync("DELETE FROM Staff WHERE PersonnelID = @Id", new { Id = id });
            return RedirectToAction("Staffs");
        }

        public async Task<IActionResult> Profile()
        {
            var roleCheck = CheckRole();
            if (roleCheck != null) return roleCheck;
            await SetGuardInfo();

            var userId = HttpContext.Session.GetInt32("UserId");
            using var conn = new SqlConnection(_connStr);
            var info = await conn.QueryFirstOrDefaultAsync<GuardInfoViewModel>(
                "SELECT * FROM Staff WHERE PersonnelID = @Id", new { Id = userId });
            return View(info);
        }

        [HttpPost]
        public async Task<IActionResult> Profile(string MobilePhoneNumber, string OrganizationalEmail, string WorkShift, string Workplace, string? password)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            using var conn = new SqlConnection(_connStr);

            string sql = "UPDATE Staff SET MobilePhoneNumber=@MobilePhoneNumber, OrganizationalEmail=@OrganizationalEmail, WorkShift=@WorkShift, Workplace=@Workplace";

            if (!string.IsNullOrEmpty(password))
            {
                password = HashPassword(password);
                sql += ", Password=@Password";
            }

            sql += " WHERE PersonnelID=@Id";

            await conn.ExecuteAsync(sql, new { MobilePhoneNumber, OrganizationalEmail, WorkShift, Workplace, Password = password, Id = userId });
            ViewBag.SuccessMessage = "اطلاعات با موفقیت به‌روزرسانی شد.";

            var info = await conn.QueryFirstOrDefaultAsync<GuardInfoViewModel>(
                "SELECT * FROM Staff WHERE PersonnelID = @Id", new { Id = userId });
            return View(info);
        }

        public async Task<IActionResult> Announcements()
        {
            var roleCheck = CheckRole();
            if (roleCheck != null) return roleCheck;
            await SetGuardInfo();

            using var conn = new SqlConnection(_connStr);

            var announcements = await conn.QueryAsync<AnnouncementItem>(
                "SELECT * FROM Announcement ORDER BY Date DESC");

            ViewBag.Dormitories = await conn.QueryAsync<DormitoryItem>("SELECT * FROM Dormitory");
            ViewBag.Blocks = await conn.QueryAsync<BlockItem>(
                "SELECT b.*, d.DormitoryName FROM Block b JOIN Dormitory d ON b.DormID = d.DormID");

            return View(announcements);
        }

        [HttpPost]
        public async Task<IActionResult> AddAnnouncement(string Title, string Description, string DeliveryMethod, DateTime EXPDate, string TargetType, int? DormID, int? BlockID, int? StudentID)
        {
            var roleCheck = CheckRole();
            if (roleCheck != null) return roleCheck;

            if (TargetType == "Dormitory" && !DormID.HasValue)
            {
                TempData["Err"] = "لطفاً یک خوابگاه انتخاب کنید.";
                return RedirectToAction("Announcements");
            }
            if (TargetType == "Block" && !BlockID.HasValue)
            {
                TempData["Err"] = "لطفاً یک بلوک انتخاب کنید.";
                return RedirectToAction("Announcements");
            }
            if (TargetType == "Student" && !StudentID.HasValue)
            {
                TempData["Err"] = "لطفاً شماره دانشجویی را جستجو و تأیید کنید.";
                return RedirectToAction("Announcements");
            }

            using var conn = new SqlConnection(_connStr);
            await conn.OpenAsync();
            using var tran = conn.BeginTransaction();
            try
            {
                var managerId = HttpContext.Session.GetInt32("UserId") ?? 1;

                var annId = await conn.ExecuteScalarAsync<int>(
                    @"INSERT INTO Announcement (Title, Description, Date, Status, DeliveryMethod, EXPDate, ManagerID) 
                      OUTPUT INSERTED.AnnouncementID 
                      VALUES (@Title, @Description, GETDATE(), 1, @DeliveryMethod, @EXPDate, @ManagerID)",
                    new { Title, Description, DeliveryMethod, EXPDate, ManagerID = managerId }, tran);

                if (TargetType == "Dormitory" && DormID.HasValue)
                {
                    await conn.ExecuteAsync("INSERT INTO AnnToDorm (AnnouncementID, DormID) VALUES (@AnnID, @DormID)", new { AnnID = annId, DormID = DormID.Value }, tran);
                }
                else if (TargetType == "Block" && BlockID.HasValue)
                {
                    await conn.ExecuteAsync("INSERT INTO AnnToBlock (AnnouncementID, BlockID) VALUES (@AnnID, @BlockID)", new { AnnID = annId, BlockID = BlockID.Value }, tran);
                }
                else if (TargetType == "Student" && StudentID.HasValue)
                {
                    await conn.ExecuteAsync("INSERT INTO AnnToSTU (AnnouncementID, STUID) VALUES (@AnnID, @STUID)", new { AnnID = annId, STUID = StudentID.Value }, tran);
                }

                tran.Commit();
                TempData["Msg"] = "✅ اطلاعیه با موفقیت برای مخاطبین هدف ثبت و ارسال شد.";
            }
            catch (Exception ex)
            {
                tran.Rollback();
                TempData["Err"] = "❌ خطایی در فرآیند ثبت رخ داد: " + ex.Message;
            }
            return RedirectToAction("Announcements");
        }
        [HttpPost]
        public async Task<IActionResult> DeleteAnnouncement(int id)
        {
            var roleCheck = CheckRole();
            if (roleCheck != null) return roleCheck;

            using var conn = new SqlConnection(_connStr);
            await conn.ExecuteAsync("DELETE FROM Announcement WHERE AnnouncementID = @Id", new { Id = id });

            TempData["Msg"] = "اطلاعیه حذف شد.";
            return RedirectToAction("Announcements");
        }
    }
}
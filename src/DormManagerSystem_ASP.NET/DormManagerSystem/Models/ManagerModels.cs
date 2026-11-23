namespace DormWeb.Models
{
    public class DashboardStats
    {
        public int TotalStudents { get; set; }
        public int TotalBlocks { get; set; }
        public int TotalDormitories { get; set; }
    }
    public class RoomViewModel
    {
        public int RoomID { get; set; }
        public int Capacity { get; set; }
        public int AvailableCapacity { get; set; }
        public int BlockID { get; set; }
        public string BlockName { get; set; } = "";
        public int DormID { get; set; }
        public string DormitoryName { get; set; } = "";
        public string DormitoryType { get; set; } = "";
        public string NameRoom { get; set; } = "";

    }

    public class AnnouncementItem
    {
        public int AnnouncementID { get; set; }
        public string Title { get; set; } = "";
        public string Description { get; set; } = "";
        public DateTime Date { get; set; }
        public string DeliveryMethod { get; set; } = "";
        public DateTime EXPDate { get; set; }
    }

    public class StudentListItem
    {
        public int StudentID { get; set; }
        public string FirstName { get; set; } = "";
        public string LastName { get; set; } = "";
        public string NationalID { get; set; } = "";
        public string MobilePhoneNumber { get; set; } = "";
        public string Major { get; set; } = "";
        public int? GroupNumber { get; set; }
    }

    public class StudentDetail
    {
        public int StudentID { get; set; }
        public string FirstName { get; set; } = "";
        public string LastName { get; set; } = "";
        public string NationalID { get; set; } = "";
        public string Password { get; set; } = "";
        public string Gender { get; set; } = "";
        public DateTime BirthDate { get; set; }
        public string MobilePhoneNumber { get; set; } = "";
        public string OrganizationalEmail { get; set; } = "";
        public string Major { get; set; } = "";
        public string AcademicDegree { get; set; } = "";
        public int? GroupNumber { get; set; }
        public string EmergencyContactNumber { get; set; } = "";
        public List<ParentViewModel> Parents { get; set; } = new();
    }

    public class ParentViewModel
    {
        public string NationalID { get; set; } = "";
        public string PhoneNum { get; set; } = "";
        public string FName { get; set; } = "";
        public string LName { get; set; } = "";
        public string City { get; set; } = "";
        public string Province { get; set; } = "";
        public string SpecAddress { get; set; } = "";
        public string PostID { get; set; } = "";
    }

    public class DormitoryItem
    {
        public int DormID { get; set; }
        public string DormitoryName { get; set; } = "";
        public string City { get; set; } = "";
        public string Province { get; set; } = "";
        public string DormitoryType { get; set; } = "";
        public string PostID { get; set; } = "";
        public string AddressDescription { get; set; } = "";
    }

    public class BlockItem
    {
        public int BlockID { get; set; }
        public string BName { get; set; } = "";
        public int NumberOfFloors { get; set; }
        public decimal TotalArea { get; set; }
        public int SharedRestroomCount { get; set; }
        public bool StorageAvailability { get; set; }
        public bool ElevatorAvailability { get; set; }
        public string BlockPhoneNumber { get; set; } = "";
        public string DormitoryName { get; set; } = "";
    }

    public class RoomItem
    {
        public int RoomID { get; set; }
        public string NameRoom { get; set; } = "";
        public int Capacity { get; set; }
        public string Status { get; set; } = "";
        public string RoomCondition { get; set; } = "";
        public bool HasBathroom { get; set; }
        public bool HasBalcony { get; set; }
        public string BlockName { get; set; } = "";
    }

    public class TimeSlotItem
    {
        public int TID { get; set; }
        public int BlockID { get; set; }
        public string BlockName { get; set; } = "";
        public DateTime GroupingStartDate { get; set; }
        public DateTime SelectionStartDate { get; set; }
        public DateTime SelectionEndDate { get; set; }
        public int Tuition { get; set; }
        public bool IsActive { get; set; }
    }

    public class StaffItem
    {
        public int PersonnelID { get; set; }
        public string FirstName { get; set; } = "";
        public string LastName { get; set; } = "";
        public string NationalID { get; set; } = "";
        public string Gender { get; set; } = "";
        public string MobilePhoneNumber { get; set; } = "";
        public string OrganizationalEmail { get; set; } = "";
        public string UserRole { get; set; } = "";
        public string WorkShift { get; set; } = "";
        public string Workplace { get; set; } = "";
        public string Password { get; set; } = "";
    }

    public class StudyHallItem
    {
        public int FACID { get; set; }
        public string Name { get; set; } = "";
        public int ChairNum { get; set; }
        public decimal TotalArea { get; set; }
        public int DeskNum { get; set; }
        public int StandardCapacity { get; set; }
        public string LocationType { get; set; } = "";
        public int ComputerNum { get; set; }
        public int BookNum { get; set; }
        public string DormitoryName { get; set; } = "";
    }

    public class SportHallItem
    {
        public int FACID { get; set; }
        public string Name { get; set; } = "";
        public string VentilationStatus { get; set; } = "";
        public decimal TotalArea { get; set; }
        public bool RestroomShowerAvailability { get; set; }
        public int StandardCapacity { get; set; }
        public string LocationType { get; set; } = "";
        public int LockerNum { get; set; }
        public string DormitoryName { get; set; } = "";
    }
}
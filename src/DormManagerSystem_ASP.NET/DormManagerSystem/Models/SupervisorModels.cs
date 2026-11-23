namespace DormWeb.Models
{
    public class SupervisorBlockItem
    {
        public int BlockID { get; set; }
        public string BName { get; set; } = "";
        public string DormitoryName { get; set; } = "";
        public int NumberOfFloors { get; set; }
        public int TotalRooms { get; set; }
        public int AvailableRooms { get; set; }
    }

    public class SupervisorStudentItem
    {
        public int StudentID { get; set; }
        public string FirstName { get; set; } = "";
        public string LastName { get; set; } = "";
        public string MobilePhoneNumber { get; set; } = "";
        public int? RoomID { get; set; }
        public string RoomName { get; set; } = "";
        public string BlockName { get; set; } = "";
    }

    public class SupervisorPaymentItem
    {
        public int TransactionID { get; set; }
        public int StudentID { get; set; }
        public string StudentName { get; set; } = "";
        public string Title { get; set; } = "";
        public DateTime Date { get; set; }
        public string PaymentStatus { get; set; } = "";
    }

    public class SupervisorGroupItem
    {
        public int GroupID { get; set; }
        public string GroupType { get; set; } = "";
        public int? RoomID { get; set; }
        public string RoomName { get; set; } = "";
        public string BlockName { get; set; } = "";
        public int MemberCount { get; set; }
        public int? LeaderID { get; set; }
        public string LeaderName { get; set; } = "";
    }

    public class SupervisorReportItem
    {
        public int RID { get; set; }
        public string Title { get; set; } = "";
        public string Type { get; set; } = "";
        public DateTime Date { get; set; }
        public string Target { get; set; } = "";
        public string Description { get; set; } = "";
        public string StudentName { get; set; } = "";
    }
}
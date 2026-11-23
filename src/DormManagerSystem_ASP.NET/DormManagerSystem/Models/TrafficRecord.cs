namespace DormWeb.Models
{
    public class TrafficRecord
    {
        public int AID { get; set; }
        public int StudentID { get; set; }
        public string FirstName { get; set; } = "";
        public string LastName { get; set; } = "";
        public string FullName => $"{FirstName} {LastName}";
        public string AccessType { get; set; } = "";
        public DateTime? AccessDate { get; set; }
        public TimeSpan? AccessTime { get; set; }
    }
}
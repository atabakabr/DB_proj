namespace DormWeb.Models
{
    public class StudentGuardViewModel
    {
        public int StudentID { get; set; }
        public string FirstName { get; set; } = "";
        public string LastName { get; set; } = "";
        public string FatherName { get; set; } = "";
        public string StudentPhone { get; set; } = "";
        public string ParentPhone { get; set; } = "";
        public string Province { get; set; } = "";
        public string City { get; set; } = "";
        public string Address { get; set; } = "";
    }
}
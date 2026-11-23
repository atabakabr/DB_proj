namespace DormWeb.Models
{
    public class GuardInfoViewModel
    {
        public int PersonnelID { get; set; }
        public string FirstName { get; set; } = "";
        public string LastName { get; set; } = "";
        public string NationalID { get; set; } = "";
        public string Password { get; set; } = "";
        public string Gender { get; set; } = "";
        public DateTime BirthDate { get; set; }
        public string MobilePhoneNumber { get; set; } = "";
        public string? OrganizationalEmail { get; set; }
        public string UserRole { get; set; } = "";
        public DateTime EmploymentStartDate { get; set; }
        public string? WorkShift { get; set; }
        public decimal? Salary { get; set; }
        public string? Workplace { get; set; }
        public string? BankAccountNumber { get; set; }
    }
}
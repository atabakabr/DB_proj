CREATE VIEW vw_GuardStudentSearch AS
SELECT 
    s.StudentID,
    s.FirstName,
    s.LastName,
    p.FName AS FatherName,
    s.MobilePhoneNumber AS StudentPhone,
    p.PhoneNum AS ParentPhone,
    p.Province,
    p.City,
    p.SpecAddress AS [Address]
FROM Student s
LEFT JOIN Parent p ON s.StudentID = p.StudentID;
GO


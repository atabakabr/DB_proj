CREATE TABLE Staff (
    PersonnelID INT NOT NULL,
    FirstName NVARCHAR(50) NOT NULL,
    LastName NVARCHAR(50) NOT NULL,
    NationalID NVARCHAR(10) NOT NULL,
    Password NVARCHAR(255) NOT NULL,
    Gender CHAR(1) NOT NULL,
    BirthDate DATE NOT NULL,
    MobilePhoneNumber NVARCHAR(15) NOT NULL,
    OrganizationalEmail NVARCHAR(100) NULL,
    UserRole NVARCHAR(50) NOT NULL,
    EmploymentStartDate DATE NOT NULL,
    WorkShift NVARCHAR(50) NULL,
    Salary DECIMAL(18,2) NULL,
    Workplace NVARCHAR(100) NULL,
    BankAccountNumber NVARCHAR(20) NULL,

    CONSTRAINT PK_Staff
        PRIMARY KEY (PersonnelID),
    CONSTRAINT UQ_Staff_NationalID
        UNIQUE (NationalID),
    CONSTRAINT UQ_Staff_MobilePhoneNumber
        UNIQUE (MobilePhoneNumber),
    CONSTRAINT UQ_Staff_OrganizationalEmail
        UNIQUE (OrganizationalEmail),
    CONSTRAINT UQ_Staff_BankAccountNumber
        UNIQUE (BankAccountNumber),
    CONSTRAINT CHK_Staff_Gender
        CHECK (Gender IN (N'M', N'F')),
    CONSTRAINT CHK_Staff_UserRole
        CHECK (UserRole IN (N'مدیر', N'سرپرست', N'نگهبان')),
    CONSTRAINT CHK_Staff_Salary
        CHECK (Salary >= 0)
);

CREATE TABLE Dormitory (
    DormID INT IDENTITY(1,1) NOT NULL,
    DormitoryName NVARCHAR(100) NOT NULL,
    City NVARCHAR(50) NOT NULL,
    Province NVARCHAR(50) NOT NULL,
    DormitoryType CHAR(7) NOT NULL,
    PostID NVARCHAR(10) NOT NULL,
    AddressDescription NVARCHAR(500) NULL,

    CONSTRAINT CHK_Dormitory_Type
    CHECK (DormitoryType IN ('F', 'M', 'Married')),
    CONSTRAINT UQ_Dorm_PostID
        UNIQUE (PostID),
    CONSTRAINT PK_Dormitory
        PRIMARY KEY (DormID),

);

CREATE TABLE Block (
    BlockID INT IDENTITY(1,1),
    BName NVARCHAR(100) NOT NULL,
    NumberOfFloors INT NOT NULL CHECK (NumberOfFloors > 0),
    TotalArea DECIMAL(10,2) NOT NULL,
    SharedRestroomCount INT NOT NULL CHECK (SharedRestroomCount >= 0),
    StorageAvailability BIT NOT NULL DEFAULT 0,
    ElevatorAvailability BIT NOT NULL DEFAULT 0,
    BlockPhoneNumber NVARCHAR(15) NULL,
    DormID INT NOT NULL,

    CONSTRAINT PK_Block
        PRIMARY KEY (BlockID),

    CONSTRAINT FK_Block_Dormitory
        FOREIGN KEY (DormID)
        REFERENCES Dormitory(DormID)
        ON DELETE CASCADE ON UPDATE CASCADE
);

CREATE TABLE Room (
    RoomID INT IDENTITY(1,1),
    NameRoom NVARCHAR(50) NOT NULL,
    InternetAccess BIT NOT NULL DEFAULT 0,
    Capacity INT NOT NULL CHECK (Capacity > 0),
    Status NVARCHAR(50) NOT NULL DEFAULT N'خالی',
    RoomCondition NVARCHAR(100) NOT NULL DEFAULT N'قابل سکونت',
    HasBathroom BIT NOT NULL DEFAULT 0,
    HasBalcony BIT NOT NULL DEFAULT 0,
    BlockID INT NOT NULL,

    CONSTRAINT PK_Room
        PRIMARY KEY (RoomID),
    CONSTRAINT CHK_Room_Status
        CHECK (Status IN (N'خالی', N'پر', N'تعمیرات')),
    CONSTRAINT CHK_Room_Condition
        CHECK (RoomCondition IN (N'قابل سکونت', N'نیازمند تعمیر', N'غیرقابل استفاده')),
    CONSTRAINT FK_Room_Block
        FOREIGN KEY (BlockID)
        REFERENCES Block(BlockID)
        ON DELETE CASCADE
        ON UPDATE CASCADE
);

CREATE TABLE [Group] (
    GroupID INT IDENTITY(1,1),
    IsActive BIT NOT NULL DEFAULT 1,
    CreatedAt DATETIME NOT NULL DEFAULT GETDATE(),
    RoomID INT NULL,
    GroupType NVARCHAR(10) NOT NULL,

    CONSTRAINT CHK_GroupType
        CHECK (GroupType IN (N'M', N'F', N'Married')),

    CONSTRAINT PK_Group
        PRIMARY KEY (GroupID),
    CONSTRAINT FK_Group_Room
        FOREIGN KEY (RoomID)
        REFERENCES Room(RoomID)
        ON DELETE SET NULL
        ON UPDATE CASCADE
);

CREATE TABLE Student (
    StudentID INT NOT NULL,
    FirstName NVARCHAR(50) NOT NULL,
    LastName NVARCHAR(50) NOT NULL,
    NationalID NVARCHAR(10) UNIQUE NOT NULL,
    Password NVARCHAR(255) NOT NULL,
    Gender CHAR(1) CHECK (Gender IN (N'M', N'F')) NOT NULL,
    BirthDate DATE NOT NULL,
    MobilePhoneNumber NVARCHAR(15) UNIQUE NOT NULL,
    OrganizationalEmail NVARCHAR(100) UNIQUE NULL,
    Major NVARCHAR(100) NOT NULL,
    AcademicDegree CHAR(2) NOT NULL CHECK (AcademicDegree IN ('BS', 'MS', 'PH')),
    IsGroupLeader BIT NOT NULL DEFAULT 0,
    GroupNumber INT NULL,
    EmergencyContactNumber NVARCHAR(15) NOT NULL,

    CONSTRAINT PK_Student
        PRIMARY KEY (StudentID),
    CONSTRAINT FK_Student_Group
        FOREIGN KEY (GroupNumber)
        REFERENCES [Group](GroupID)
        ON DELETE SET NULL ON UPDATE CASCADE
);


CREATE TABLE TimeSlot (
    TID INT IDENTITY(1,1),
    BlockID INT NOT NULL,
    GroupingStartDate DATE NOT NULL,
    SelectionStartDate DATE NOT NULL,
    SelectionEndDate DATE NOT NULL,
    Tuition INT NOT NULL,
    IsActive BIT NOT NULL DEFAULT 1,

    CONSTRAINT PK_TimeSlot PRIMARY KEY (TID),
    CONSTRAINT FK_TimeSlot_Block
        FOREIGN KEY (BlockID)
        REFERENCES Block(BlockID)
        ON DELETE CASCADE ON UPDATE CASCADE,
    CONSTRAINT CHK_TimeSlot_Dates
        CHECK (SelectionStartDate > GroupingStartDate AND SelectionEndDate > SelectionStartDate)
);

CREATE TABLE AccessLog (
    AID INT IDENTITY(1,1),
    AccessType NVARCHAR(50) NOT NULL CHECK (
        AccessType IN (N'ورود', N'خروج')
    ),
    Date DATE NOT NULL,
    Time TIME NOT NULL,
    StudentID INT NOT NULL,

    CONSTRAINT PK_AID PRIMARY KEY (AID),
    CONSTRAINT FK_AccessLog_Student
        FOREIGN KEY (StudentID)
        REFERENCES Student(StudentID)
        ON DELETE CASCADE ON UPDATE CASCADE
);

CREATE TABLE Payment (
    TransactionID INT IDENTITY(1,1) NOT NULL,
    Title NVARCHAR(50) NOT NULL,
    Date DATE NOT NULL,
    PaymentStatus NVARCHAR(50) NOT NULL,
    StudentID INT NOT NULL,
    CONSTRAINT PK_Payment
        PRIMARY KEY (TransactionID),
    CONSTRAINT FK_Payment_Student
        FOREIGN KEY (StudentID)
        REFERENCES Student(StudentID)
        ON DELETE CASCADE ON UPDATE CASCADE,
    CONSTRAINT CHK_Payment_Status
        CHECK (PaymentStatus IN (N'موفق', N'در انتظار'))
);

CREATE TABLE Announcement (
    AnnouncementID INT IDENTITY(1,1),
    Title NVARCHAR(200) NOT NULL,
    Description NVARCHAR(1000) NULL,
    Date DATE NOT NULL DEFAULT GETDATE(),
    Status BIT NOT NULL DEFAULT 1,
    DeliveryMethod CHAR(2) NOT NULL DEFAULT N'SY',
    ManagerID INT NOT NULL,
    CONSTRAINT CHK_DeliveryMethod CHECK (DeliveryMethod IN (N'SM', N'EM', N'SY')),    
    EXPDate DATE NOT NULL,
    CONSTRAINT PK_Announcement
        PRIMARY KEY (AnnouncementID),
    CONSTRAINT CHK_Announcement_ExpDate_After_Date
        CHECK (EXPDate > Date), 
    CONSTRAINT FK_Announcement_Staff
        FOREIGN KEY (ManagerID)
        REFERENCES Staff(PersonnelID)
        ON DELETE CASCADE ON UPDATE CASCADE
);

CREATE TABLE Report (
    RID INT IDENTITY(1,1) NOT NULL,
    Target NVARCHAR(100) NOT NULL,
    Date DATE NOT NULL DEFAULT GETDATE(),
    Title NVARCHAR(100) NOT NULL,
    Type NVARCHAR(50) NOT NULL,
    Description NVARCHAR(500) NULL,
    StudentID INT NOT NULL,
    ManagerID INT NOT NULL,
    CONSTRAINT PK_Report
        PRIMARY KEY (RID),
    CONSTRAINT FK_Report_Student
        FOREIGN KEY (StudentID)
        REFERENCES Student(StudentID)
        ON DELETE CASCADE ON UPDATE CASCADE,

    CONSTRAINT FK_Report_Staff
        FOREIGN KEY (ManagerID)
        REFERENCES Staff(PersonnelID)
        ON DELETE CASCADE ON UPDATE CASCADE
);

CREATE TABLE StudyHall (
    FACID INT IDENTITY(1,1) NOT NULL,
    Name NVARCHAR(100) NOT NULL,
    ChairNum INT NOT NULL,
    TotalArea DECIMAL(10,2) NOT NULL CHECK (TotalArea > 0),
    DeskNum INT NOT NULL,
    StandardCapacity INT NOT NULL,
    LocationType NVARCHAR(100) NOT NULL,
    ComputerNum INT NOT NULL,
    BookNum INT NOT NULL,
    DormID INT NOT NULL,

    CONSTRAINT FK_StudyHall_Dormitory
        FOREIGN KEY (DormID)
        REFERENCES Dormitory(DormID)
        ON DELETE CASCADE ON UPDATE CASCADE,

    CONSTRAINT PK_SH_FACID
        PRIMARY KEY (FACID),

);

CREATE TABLE SportHall (
    FACID INT IDENTITY(1,1) NOT NULL,
    Name NVARCHAR(100) NOT NULL,
    VentilationStatus NVARCHAR(50) NOT NULL,
    TotalArea DECIMAL(10,2) NOT NULL CHECK (TotalArea > 0),
    RestroomShowerAvailability BIT NOT NULL DEFAULT 0,
    StandardCapacity INT NOT NULL,
    LocationType NVARCHAR(100) NOT NULL,
    LockerNum INT NOT NULL,
    DormID INT NOT NULL,

    CONSTRAINT FK_SportHall_Dormitory
        FOREIGN KEY (DormID)
        REFERENCES Dormitory(DormID)
        ON DELETE CASCADE ON UPDATE CASCADE,

    CONSTRAINT PK_PH_FACID
        PRIMARY KEY (FACID),

);

CREATE TABLE ManagesBlock (
    ManagerID INT NOT NULL,
    BlockID INT NOT NULL,
    Date_Started DATE NOT NULL,

    CONSTRAINT PK_ManagesBlock PRIMARY KEY (ManagerID, BlockID),

    CONSTRAINT FK_ManagesBlock_Staff
        FOREIGN KEY (ManagerID)
        REFERENCES Staff(PersonnelID)
        ON DELETE CASCADE ON UPDATE CASCADE,

    CONSTRAINT FK_ManagesBlock_Block
        FOREIGN KEY (BlockID)
        REFERENCES Block(BlockID)
        ON DELETE CASCADE ON UPDATE CASCADE
);

CREATE TABLE AnnToDorm (
    AnnouncementID INT NOT NULL,
    DormID INT NOT NULL,

    CONSTRAINT PK_AnnToDorm PRIMARY KEY (AnnouncementID, DormID),

    CONSTRAINT FK_AnnToDorm_Announcement
        FOREIGN KEY (AnnouncementID)
        REFERENCES Announcement(AnnouncementID)
        ON DELETE CASCADE ON UPDATE CASCADE,

    CONSTRAINT FK_AnnToDorm_Dormitory
        FOREIGN KEY (DormID)
        REFERENCES Dormitory(DormID)
        ON DELETE CASCADE ON UPDATE CASCADE
);

CREATE TABLE AnnToBlock (
    AnnouncementID INT NOT NULL,
    BlockID INT NOT NULL,

    CONSTRAINT PK_AnnToBlock PRIMARY KEY (AnnouncementID, BlockID),

    CONSTRAINT FK_AnnToBlock_Announcement
        FOREIGN KEY (AnnouncementID)
        REFERENCES Announcement(AnnouncementID)
        ON DELETE CASCADE ON UPDATE CASCADE,

    CONSTRAINT FK_AnnToBlock_Block
        FOREIGN KEY (BlockID)
        REFERENCES Block(BlockID)
        ON DELETE CASCADE ON UPDATE CASCADE
);

CREATE TABLE AnnToSTU (
    AnnouncementID INT NOT NULL,
    STUID INT NOT NULL,

    CONSTRAINT PK_AnnToSTU PRIMARY KEY (AnnouncementID, STUID),

    CONSTRAINT FK_AnnToSTU_Announcement
        FOREIGN KEY (AnnouncementID)
        REFERENCES Announcement(AnnouncementID)
        ON DELETE CASCADE ON UPDATE CASCADE,

    CONSTRAINT FK_AnnToSTU_Student
        FOREIGN KEY (STUID)
        REFERENCES Student(StudentID)
        ON DELETE CASCADE ON UPDATE CASCADE
);

CREATE TABLE Parent (
    StudentID INT NOT NULL,
    NationalID NVARCHAR(10) NOT NULL,
    PhoneNum NVARCHAR(15) NOT NULL,
    FName NVARCHAR(50) NOT NULL,
    LName NVARCHAR(50) NOT NULL,
    City NVARCHAR(50) NOT NULL,
    Province NVARCHAR(50) NOT NULL,
    SpecAddress NVARCHAR(300) NULL,
    PostID NVARCHAR(10) NOT NULL,

    CONSTRAINT PK_Parent PRIMARY KEY (StudentID, NationalID),

    CONSTRAINT FK_Parent_Student
        FOREIGN KEY (StudentID)
        REFERENCES Student(StudentID)
        ON DELETE CASCADE ON UPDATE CASCADE
);

GO
CREATE OR ALTER PROCEDURE NotifyUnpaidStudents
    @ManagerID INT
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @TransactionID INT, @StudentID INT, @PaymentDate DATE;
    DECLARE @AnnID INT;

    DECLARE payment_cursor CURSOR FOR
        SELECT StudentID, TransactionID, [Date]
        FROM Payment
        WHERE PaymentStatus = N'در انتظار';

    OPEN payment_cursor;
    FETCH NEXT FROM payment_cursor INTO @StudentID, @TransactionID, @PaymentDate;

    WHILE @@FETCH_STATUS = 0
    BEGIN
        INSERT INTO Announcement (Title, Description, Date, Status, DeliveryMethod, EXPDate, ManagerID)
        VALUES (
            N'یادآوری پرداخت',
            N'پرداخت شما با کد پیگیری ' + CAST(@TransactionID AS NVARCHAR(20)) +
            N' در تاریخ ' + CAST(@PaymentDate AS NVARCHAR(20)) + N' هنوز تکمیل نشده است.',
            GETDATE(),
            1,
            'SY',
            DATEADD(DAY, 3, GETDATE()),
            @ManagerID
        );

        SET @AnnID = SCOPE_IDENTITY();

        INSERT INTO AnnToSTU (AnnouncementID, STUID)
        VALUES (@AnnID, @StudentID);

        FETCH NEXT FROM payment_cursor INTO @StudentID, @TransactionID, @PaymentDate;
    END

    CLOSE payment_cursor;
    DEALLOCATE payment_cursor;

    PRINT N'اطلاعیه‌های یادآوری برای پرداخت‌های در انتظار ارسال شد.';
END;
GO
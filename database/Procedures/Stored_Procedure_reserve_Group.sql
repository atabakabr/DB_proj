CREATE OR ALTER PROCEDURE ReserveRoomForGroup
    @GroupID    INT,
    @NewRoomID  INT
AS
BEGIN
    SET NOCOUNT ON;

    IF NOT EXISTS (SELECT 1 FROM [Group] WHERE GroupID = @GroupID)
    BEGIN
        RAISERROR(N'گروه مورد نظر یافت نشد یا فعال نیست.', 16, 1);
        RETURN;
    END

    IF NOT EXISTS (SELECT 1 FROM Room WHERE RoomID = @NewRoomID AND Status = N'خالی' AND RoomCondition = N'قابل سکونت')
    BEGIN
        RAISERROR(N'اتاق انتخاب‌شده در دسترس نیست.', 16, 1);
        RETURN;
    END

    IF (SELECT RoomID FROM [Group] WHERE GroupID = @GroupID) = @NewRoomID
    BEGIN
        RAISERROR(N'گروه هم‌اکنون در همین اتاق ساکن است.', 16, 1);
        RETURN;
    END

    IF NOT EXISTS (
        SELECT 1
        FROM [Group] g
        JOIN Room r ON r.RoomID = @NewRoomID
        JOIN Block b ON b.BlockID = r.BlockID
        JOIN Dormitory d ON d.DormID = b.DormID
        WHERE g.GroupID = @GroupID
          AND g.GroupType = d.DormitoryType
    )
    BEGIN
        RAISERROR(N'جنسیت گروه با نوع خوابگاه اتاق همخوانی ندارد.', 16, 1);
        RETURN;
    END

    IF (
        SELECT COUNT(*)
        FROM Student
        WHERE GroupNumber = @GroupID
    ) > (SELECT Capacity FROM Room WHERE RoomID = @NewRoomID)
    BEGIN
        RAISERROR(N'تعداد اعضای گروه بیش از ظرفیت اتاق جدید است.', 16, 1);
        RETURN;
    END

    DECLARE @BlockID INT = (SELECT BlockID FROM Room WHERE RoomID = @NewRoomID);
    DECLARE @Tuition INT = dbo.GetBlockTuition(@BlockID, GETDATE());

    DECLARE @CurrentRoomID INT = (SELECT RoomID FROM [Group] WHERE GroupID = @GroupID);

    BEGIN TRY
        BEGIN TRANSACTION;

            UPDATE [Group] SET RoomID = @NewRoomID WHERE GroupID = @GroupID;

            UPDATE Room SET Status = N'خالی' WHERE RoomID = @CurrentRoomID AND RoomID IS NOT NULL;
            UPDATE Room SET Status = N'پر'   WHERE RoomID = @NewRoomID;

            INSERT INTO Payment (Title, Date, PaymentStatus, StudentID)
            SELECT
                N'رزرو اتاق ' + CAST(@NewRoomID AS NVARCHAR) + N' (شهریه: ' + CAST(@Tuition AS NVARCHAR) + N' تومان)',
                GETDATE(),
                N'در انتظار',
                s.StudentID
            FROM Student s
            WHERE s.GroupNumber = @GroupID;

        COMMIT TRANSACTION;
        PRINT N'رزرو با موفقیت انجام شد و پرداخت‌ها ثبت گردید.';
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0
            ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END;
GO
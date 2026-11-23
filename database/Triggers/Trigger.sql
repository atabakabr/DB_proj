CREATE OR ALTER TRIGGER trg_ValidateAccessLogOrder
ON AccessLog
AFTER INSERT
AS
BEGIN
    SET NOCOUNT ON;

    IF EXISTS (
        SELECT 1
        FROM inserted i
        WHERE i.AccessType = (
            SELECT TOP 1 al.AccessType
            FROM AccessLog al
            WHERE al.StudentID = i.StudentID
              AND al.AID < i.AID
            ORDER BY al.AID DESC
        )
    )
    BEGIN
        RAISERROR (N'ترتیب ورود و خروج دانشجو رعایت نشده است.', 16, 1);
        ROLLBACK TRANSACTION;
    END
END;
GO
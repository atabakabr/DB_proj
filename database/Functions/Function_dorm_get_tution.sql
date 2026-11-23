CREATE OR ALTER FUNCTION dbo.GetBlockTuition
(
    @BlockID    INT,
    @TargetDate DATE
)
RETURNS INT
AS
BEGIN
    DECLARE @Tuition INT = 0;

    SELECT TOP 1 @Tuition = Tuition
    FROM TimeSlot
    WHERE BlockID = @BlockID
      AND IsActive = 1
      AND @TargetDate BETWEEN SelectionStartDate AND SelectionEndDate
    ORDER BY SelectionStartDate;

    RETURN @Tuition;
END;
GO
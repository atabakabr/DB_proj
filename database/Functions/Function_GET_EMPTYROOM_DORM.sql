CREATE OR ALTER FUNCTION dbo.GetAvailableRooms
(
    @DormID INT = NULL,
    @BlockID INT = NULL
)
RETURNS TABLE
AS
RETURN
(
    SELECT
        r.RoomID,
        r.Capacity,
        r.Capacity - ISNULL(
            (SELECT COUNT(*)
             FROM Student s
             JOIN [Group] g ON s.GroupNumber = g.GroupID
             WHERE g.RoomID = r.RoomID), 0
        ) AS AvailableCapacity,
        b.BlockID,
        b.BName AS BlockName,
        d.DormID,
        d.DormitoryName,
        d.DormitoryType
    FROM Room r
    INNER JOIN Block b ON r.BlockID = b.BlockID
    INNER JOIN Dormitory d ON b.DormID = d.DormID
    WHERE (@DormID IS NULL OR d.DormID = @DormID)
      AND (@BlockID IS NULL OR b.BlockID = @BlockID)
      AND r.Capacity - ISNULL(
            (SELECT COUNT(*)
             FROM Student s
             JOIN [Group] g ON s.GroupNumber = g.GroupID
             WHERE g.RoomID = r.RoomID), 0
        ) > 0
);
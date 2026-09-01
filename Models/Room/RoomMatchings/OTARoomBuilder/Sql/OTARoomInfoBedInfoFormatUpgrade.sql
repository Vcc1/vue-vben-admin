/*
    OTARoomInfo.BedInfo format upgrade

    Legacy format:
        [{"and":[{"q":2,"s":"Twin","b":"SofaBed","n":"单人沙发床"}]}]

    New format:
        [[{"q":2,"s":"Twin","b":"SofaBed","n":"单人沙发床"}]]

    Only the outer `and` wrapper is removed. The property names and values inside
    each bed item are preserved as-is.
*/
SET XACT_ABORT ON;
BEGIN TRY
    BEGIN TRANSACTION;

    IF OBJECT_ID(N'dbo.OTARoomInfo', N'U') IS NULL
        THROW 50001, 'dbo.OTARoomInfo does not exist.', 1;

    ;WITH Converted AS
    (
        SELECT
            r.Id,
            NewBedInfo = CONVERT(nvarchar(max),
                N'[' + STRING_AGG(
                    CONVERT(nvarchar(max), JSON_QUERY(item.[value], N'$.and')),
                    N','
                ) WITHIN GROUP (ORDER BY TRY_CONVERT(int, item.[key])) + N']'),
            ItemCount = COUNT_BIG(*),
            AndItemCount = COUNT_BIG(JSON_QUERY(item.[value], N'$.and'))
        FROM dbo.OTARoomInfo AS r
        CROSS APPLY OPENJSON(r.BedInfo) AS item
        WHERE ISJSON(r.BedInfo) = 1
        GROUP BY r.Id
    )
    UPDATE r
    SET r.BedInfo = c.NewBedInfo
    FROM dbo.OTARoomInfo AS r
    INNER JOIN Converted AS c ON c.Id = r.Id
    WHERE c.ItemCount > 0
      AND c.ItemCount = c.AndItemCount
      AND ISJSON(c.NewBedInfo) = 1
      AND c.NewBedInfo <> r.BedInfo;

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
        ROLLBACK TRANSACTION;
    THROW;
END CATCH;
GO

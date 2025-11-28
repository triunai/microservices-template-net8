USE [TenantMaster]
GO

/****** Object:  StoredProcedure [dbo].[PurgeOldAuditLogs]    Script Date: 27/11/2025 8:53:42 AM ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO


CREATE PROCEDURE [dbo].[PurgeOldAuditLogs]
AS
BEGIN
    SET NOCOUNT ON;
    
    DECLARE @CutoffDate DATETIMEOFFSET = DATEADD(DAY, -90, SYSDATETIMEOFFSET());
    DECLARE @RowsDeleted INT;
    
    -- Delete in batches to avoid long-running transactions
    WHILE 1 = 1
    BEGIN
        DELETE TOP (10000) FROM dbo.AuditLog
        WHERE [Timestamp] < @CutoffDate;
        
        SET @RowsDeleted = @@ROWCOUNT;
        
        IF @RowsDeleted = 0
            BREAK;
        
        -- Log progress
        PRINT CONCAT('Deleted ', @RowsDeleted, ' audit log rows older than ', @CutoffDate);
        
        -- Small delay to reduce load
        WAITFOR DELAY '00:00:01';
    END
    
    -- Rebuild indexes after large deletes (monthly)
    IF DAY(GETDATE()) = 1
    BEGIN
        PRINT 'Reorganizing indexes...';
        ALTER INDEX ALL ON dbo.AuditLog REORGANIZE;
    END
END
GO


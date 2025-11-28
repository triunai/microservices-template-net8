
CREATE TYPE [dbo].[AuditEntryType] AS TABLE
(
    [AuditId]         UNIQUEIDENTIFIER NOT NULL,
    [TimestampUtc]     DATETIME2(3) NOT NULL,
    [TenantId]         NVARCHAR(64) NULL,
    [UserId]           UNIQUEIDENTIFIER NULL,
    [EventCategory]    NVARCHAR(32) NOT NULL,
    [EventType]        NVARCHAR(64) NOT NULL,
    [IsSuccess]        BIT NOT NULL,
    [StatusCode]       INT NULL,
    [ErrorCode]        NVARCHAR(64) NULL,
    [ErrorMessage]     NVARCHAR(1024) NULL,
    [CorrelationId]    NVARCHAR(64) NULL,
    [RequestPath]      NVARCHAR(256) NOT NULL,
    [HttpMethod]       NVARCHAR(16) NOT NULL,
    [IpAddress]        NVARCHAR(64) NULL,
    [UserAgent]        NVARCHAR(512) NULL,
    [RequestPayload]   NVARCHAR(MAX) NULL,
    [ResponsePayload]  NVARCHAR(MAX) NULL,
    [DurationMs]       INT NULL
);
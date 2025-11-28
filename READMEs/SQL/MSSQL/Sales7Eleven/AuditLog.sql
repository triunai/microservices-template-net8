USE [Sales7Eleven]
GO

/****** Object:  Table [dbo].[AuditLog]    Script Date: 27/11/2025 8:59:43 AM ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO

CREATE TABLE [dbo].[AuditLog](
	[Id] [bigint] IDENTITY(1,1) NOT NULL,
	[TenantId] [nvarchar](100) NOT NULL,
	[UserId] [nvarchar](100) NULL,
	[ClientId] [nvarchar](100) NULL,
	[IpAddress] [nvarchar](50) NULL,
	[UserAgent] [nvarchar](500) NULL,
	[Action] [nvarchar](100) NOT NULL,
	[EntityType] [nvarchar](100) NULL,
	[EntityId] [nvarchar](100) NULL,
	[Timestamp] [datetimeoffset](7) NOT NULL,
	[CorrelationId] [nvarchar](100) NULL,
	[RequestPath] [nvarchar](500) NULL,
	[IsSuccess] [bit] NOT NULL,
	[StatusCode] [int] NULL,
	[ErrorCode] [nvarchar](50) NULL,
	[ErrorMessage] [nvarchar](max) NULL,
	[DurationMs] [int] NULL,
	[RequestData] [varbinary](max) NULL,
	[ResponseData] [varbinary](max) NULL,
	[Delta] [varbinary](max) NULL,
	[IdempotencyKey] [nvarchar](100) NULL,
	[Source] [nvarchar](50) NOT NULL,
	[RequestHash] [nvarchar](64) NULL,
 CONSTRAINT [PK_AuditLog] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO

ALTER TABLE [dbo].[AuditLog] ADD  DEFAULT (sysdatetimeoffset()) FOR [Timestamp]
GO

ALTER TABLE [dbo].[AuditLog] ADD  DEFAULT ('API') FOR [Source]
GO


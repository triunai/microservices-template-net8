USE [RgtAuthPrototype]
GO

/****** Object:  Table [dbo].[ExternalIdentities]    Script Date: 4/11/2025 9:58:31 AM ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO

CREATE TABLE [dbo].[ExternalIdentities](
	[Id] [uniqueidentifier] NOT NULL,
	[UserId] [uniqueidentifier] NOT NULL,
	[Provider] [nvarchar](50) NOT NULL,
	[Issuer] [nvarchar](500) NOT NULL,
	[Subject] [nvarchar](500) NOT NULL,
	[Email] [nvarchar](256) NULL,
	[CreatedAt] [datetimeoffset](7) NOT NULL,
	[LastUsedAt] [datetimeoffset](7) NULL,
PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY],
 CONSTRAINT [UK_ExternalIdentity] UNIQUE NONCLUSTERED 
(
	[Provider] ASC,
	[Issuer] ASC,
	[Subject] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO

ALTER TABLE [dbo].[ExternalIdentities] ADD  DEFAULT (newid()) FOR [Id]
GO

ALTER TABLE [dbo].[ExternalIdentities] ADD  CONSTRAINT [DF_ExternalIdentities_CreatedAt]  DEFAULT (switchoffset(sysdatetimeoffset(),'+08:00')) FOR [CreatedAt]
GO

ALTER TABLE [dbo].[ExternalIdentities]  WITH CHECK ADD  CONSTRAINT [FK_ExternalIdentities_Users] FOREIGN KEY([UserId])
REFERENCES [dbo].[Users] ([UserId])
ON DELETE CASCADE
GO

ALTER TABLE [dbo].[ExternalIdentities] CHECK CONSTRAINT [FK_ExternalIdentities_Users]
GO


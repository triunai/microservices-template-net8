USE [RgtAuthPrototype]
GO

/****** Object:  Table [dbo].[RefreshTokens]    Script Date: 4/11/2025 9:59:24 AM ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO

CREATE TABLE [dbo].[RefreshTokens](
	[RefreshTokenId] [uniqueidentifier] NOT NULL,
	[UserId] [uniqueidentifier] NOT NULL,
	[TokenHash] [nvarchar](128) NOT NULL,
	[FamilyId] [uniqueidentifier] NOT NULL,
	[ExpiresAt] [datetimeoffset](7) NOT NULL,
	[IsRevoked] [bit] NOT NULL,
	[RevokedAt] [datetimeoffset](7) NULL,
	[RevokedReason] [nvarchar](50) NULL,
	[ReplacedByTokenId] [uniqueidentifier] NULL,
	[CreatedAt] [datetimeoffset](7) NOT NULL,
	[CreatedByIp] [nvarchar](50) NULL,
	[UserAgent] [nvarchar](500) NULL,
PRIMARY KEY CLUSTERED 
(
	[RefreshTokenId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY],
UNIQUE NONCLUSTERED 
(
	[TokenHash] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO

ALTER TABLE [dbo].[RefreshTokens] ADD  DEFAULT (newid()) FOR [RefreshTokenId]
GO

ALTER TABLE [dbo].[RefreshTokens] ADD  DEFAULT ((0)) FOR [IsRevoked]
GO

ALTER TABLE [dbo].[RefreshTokens] ADD  DEFAULT (sysdatetimeoffset()) FOR [CreatedAt]
GO

ALTER TABLE [dbo].[RefreshTokens]  WITH CHECK ADD  CONSTRAINT [FK_RefreshTokens_ReplacedBy] FOREIGN KEY([ReplacedByTokenId])
REFERENCES [dbo].[RefreshTokens] ([RefreshTokenId])
GO

ALTER TABLE [dbo].[RefreshTokens] CHECK CONSTRAINT [FK_RefreshTokens_ReplacedBy]
GO

ALTER TABLE [dbo].[RefreshTokens]  WITH CHECK ADD  CONSTRAINT [FK_RefreshTokens_Users] FOREIGN KEY([UserId])
REFERENCES [dbo].[Users] ([UserId])
ON DELETE CASCADE
GO

ALTER TABLE [dbo].[RefreshTokens] CHECK CONSTRAINT [FK_RefreshTokens_Users]
GO


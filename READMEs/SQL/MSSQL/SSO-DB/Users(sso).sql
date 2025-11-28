USE [RgtAuthPrototype]
GO

/****** Object:  Table [dbo].[Users]    Script Date: 4/11/2025 10:01:51 AM ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO

CREATE TABLE [dbo].[Users](
	[UserId] [uniqueidentifier] NOT NULL,
	[Email] [nvarchar](256) NOT NULL,
	[DisplayName] [nvarchar](200) NULL,
	[IsActive] [bit] NOT NULL,
	[CreatedAt] [datetimeoffset](7) NOT NULL,
	[LastLoginAt] [datetimeoffset](7) NULL,
PRIMARY KEY CLUSTERED 
(
	[UserId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY],
 CONSTRAINT [UK_Users_Email] UNIQUE NONCLUSTERED 
(
	[Email] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO

ALTER TABLE [dbo].[Users] ADD  DEFAULT (newid()) FOR [UserId]
GO

ALTER TABLE [dbo].[Users] ADD  DEFAULT ((1)) FOR [IsActive]
GO

ALTER TABLE [dbo].[Users] ADD  CONSTRAINT [DF_Users_CreatedAt]  DEFAULT (switchoffset(sysdatetimeoffset(),'+08:00')) FOR [CreatedAt]
GO


USE [RgtAuthPrototype]
GO

/****** Object:  Table [dbo].[RgtAuth_SsoConfigs]    Script Date: 4/11/2025 10:00:03 AM ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO

CREATE TABLE [dbo].[RgtAuth_SsoConfigs](
	[SsoConfigId] [uniqueidentifier] NOT NULL,
	[TenantKey] [nvarchar](100) NOT NULL,
	[Provider] [nvarchar](50) NOT NULL,
	[Authority] [nvarchar](400) NOT NULL,
	[ClientId] [nvarchar](200) NOT NULL,
	[ClientSecretEnc] [varbinary](max) NOT NULL,
	[Scopes] [nvarchar](500) NOT NULL,
	[RedirectUrisJson] [nvarchar](max) NOT NULL,
	[Enabled] [bit] NOT NULL,
	[UpdatedAt] [datetimeoffset](7) NOT NULL,
	[UpdatedBy] [nvarchar](100) NULL,
	[KeyVaultSecretName] [nvarchar](200) NULL,
PRIMARY KEY CLUSTERED 
(
	[SsoConfigId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO

ALTER TABLE [dbo].[RgtAuth_SsoConfigs] ADD  DEFAULT (newid()) FOR [SsoConfigId]
GO

ALTER TABLE [dbo].[RgtAuth_SsoConfigs] ADD  DEFAULT ((1)) FOR [Enabled]
GO

ALTER TABLE [dbo].[RgtAuth_SsoConfigs] ADD  DEFAULT (sysdatetimeoffset()) FOR [UpdatedAt]
GO


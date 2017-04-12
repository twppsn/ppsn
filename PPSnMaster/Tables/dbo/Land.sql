CREATE TABLE [dbo].[Land]
(
	[Id] BIGINT NOT NULL CONSTRAINT pkLandId PRIMARY KEY IDENTITY (1,1), 
	[Name] NVARCHAR(50) NOT NULL,
	[EnglishName] NVARCHAR(50) NOT NULL,
	[Iso] CHAR(2) NULL, 
	[Iso3] CHAR(3) NULL, 
	[Tld] VARCHAR(63) NULL, 
	[Vorwahl] INT NULL, 
	[Zone] NVARCHAR(50) NULL , 
	[PostAdr] NVARCHAR(50) NULL,
	[IsActive] BIT NOT NULL CONSTRAINT dfLandIsActive DEFAULT  1
)
GO
ALTER TABLE [dbo].[Land] ENABLE CHANGE_TRACKING;
GO
EXEC sp_addextendedproperty @name = N'MS_Description',
    @value = N'Primary key, FK zu Knst',
    @level0type = N'SCHEMA',
    @level0name = N'dbo',
    @level1type = N'TABLE',
    @level1name = N'Land',
    @level2type = N'COLUMN',
    @level2name = N'Id'
GO
EXEC sp_addextendedproperty @name = N'MS_Description',
    @value = N'Name des Landes',
    @level0type = N'SCHEMA',
    @level0name = N'dbo',
    @level1type = N'TABLE',
    @level1name = N'Land',
    @level2type = N'COLUMN',
    @level2name = N'Name'
GO
EXEC sp_addextendedproperty @name = N'MS_Description',
    @value = N'Iso 3166 Alpha 2 Code',
    @level0type = N'SCHEMA',
    @level0name = N'dbo',
    @level1type = N'TABLE',
    @level1name = N'Land',
    @level2type = N'COLUMN',
    @level2name = N'Iso'
GO
EXEC sp_addextendedproperty @name = N'MS_Description',
    @value = N'Iso 3166 Alpha 3 Code',
    @level0type = N'SCHEMA',
    @level0name = N'dbo',
    @level1type = N'TABLE',
    @level1name = N'Land',
    @level2type = N'COLUMN',
    @level2name = N'Iso3'
GO
EXEC sp_addextendedproperty @name = N'MS_Description',
    @value = N'Tol Level Domain',
    @level0type = N'SCHEMA',
    @level0name = N'dbo',
    @level1type = N'TABLE',
    @level1name = N'Land',
    @level2type = N'COLUMN',
    @level2name = N'Tld'
GO
EXEC sp_addextendedproperty @name = N'MS_Description',
    @value = N'Vorwahl des Landes',
    @level0type = N'SCHEMA',
    @level0name = N'dbo',
    @level1type = N'TABLE',
    @level1name = N'Land',
    @level2type = N'COLUMN',
    @level2name = N'Vorwahl'
GO
EXEC sp_addextendedproperty @name = N'MS_Description',
    @value = N'engl. Name für Postanschrift',
    @level0type = N'SCHEMA',
    @level0name = N'dbo',
    @level1type = N'TABLE',
    @level1name = N'Land',
    @level2type = N'COLUMN',
    @level2name = N'EnglishName'
GO
EXEC sp_addextendedproperty @name = N'MS_Description',
    @value = N'Währungszone z.B. Europäische Gemeinschaft (EU)',
    @level0type = N'SCHEMA',
    @level0name = N'dbo',
    @level1type = N'TABLE',
    @level1name = N'Land',
    @level2type = N'COLUMN',
    @level2name = 'Zone'
GO
EXEC sp_addextendedproperty @name = N'MS_Description',
    @value = N'Vorlage Postanschrift',
    @level0type = N'SCHEMA',
    @level0name = N'dbo',
    @level1type = N'TABLE',
    @level1name = N'Land',
    @level2type = N'COLUMN',
    @level2name = N'PostAdr'
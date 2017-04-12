CREATE TABLE [dbo].[Waeh]
(
	[Id] BIGINT NOT NULL CONSTRAINT pkWaehId PRIMARY KEY IDENTITY (1,1), 
	[Name] NVARCHAR(30) NULL, 
	[Symbol] NCHAR(5) NULL, 
	[Kurs] DECIMAL(18, 4) NULL, 
	[Iso] CHAR(3) NOT NULL,
	[System] BIT NOT NULL,
	[IsActive] BIT NOT NULL CONSTRAINT dfWaehIsActive DEFAULT  1
)
GO
ALTER TABLE [dbo].[Waeh] ENABLE CHANGE_TRACKING;
GO
EXEC sp_addextendedproperty @name = N'MS_Description',
    @value = N'Primary Key',
    @level0type = N'SCHEMA',
    @level0name = N'dbo',
    @level1type = N'TABLE',
    @level1name = N'Waeh',
    @level2type = N'COLUMN',
    @level2name = 'Id'
GO
EXEC sp_addextendedproperty @name = N'MS_Description',
    @value = N'Systemwährung',
    @level0type = N'SCHEMA',
    @level0name = N'dbo',
    @level1type = N'TABLE',
    @level1name = N'Waeh',
    @level2type = N'COLUMN',
    @level2name = N'System'
GO
EXEC sp_addextendedproperty @name = N'MS_Description',
    @value = N'Wechselkurs zur Systemwährung',
    @level0type = N'SCHEMA',
    @level0name = N'dbo',
    @level1type = N'TABLE',
    @level1name = N'Waeh',
    @level2type = N'COLUMN',
    @level2name = N'Kurs'
GO
EXEC sp_addextendedproperty @name = N'MS_Description',
    @value = N'Währungssymbol',
    @level0type = N'SCHEMA',
    @level0name = N'dbo',
    @level1type = N'TABLE',
    @level1name = N'Waeh',
    @level2type = N'COLUMN',
    @level2name = N'Symbol'
GO
EXEC sp_addextendedproperty @name = N'MS_Description',
    @value = N'Name der Währung',
    @level0type = N'SCHEMA',
    @level0name = N'dbo',
    @level1type = N'TABLE',
    @level1name = N'Waeh',
    @level2type = N'COLUMN',
    @level2name = N'Name'
GO
EXEC sp_addextendedproperty @name = N'MS_Description',
    @value = N'Iso-Abkürzung',
    @level0type = N'SCHEMA',
    @level0name = N'dbo',
    @level1type = N'TABLE',
    @level1name = N'Waeh',
    @level2type = N'COLUMN',
    @level2name = N'Iso'
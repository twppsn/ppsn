CREATE TABLE [dbo].[Mein]
(
	[Id] BIGINT NOT NULL CONSTRAINT pkMeinId PRIMARY KEY IDENTITY (1,1),
	[Name] NVARCHAR(50) NOT NULL,
	[IsActive] BIT NOT NULL CONSTRAINT dfMeinIsActive DEFAULT  1
)
GO
ALTER TABLE [dbo].[Mein] ENABLE CHANGE_TRACKING;
GO
EXEC sp_addextendedproperty @name = N'MS_Description',
    @value = N'Primary key',
    @level0type = N'SCHEMA',
    @level0name = N'dbo',
    @level1type = N'TABLE',
    @level1name = N'Mein',
    @level2type = N'COLUMN',
    @level2name = N'Id'
GO
EXEC sp_addextendedproperty @name = N'MS_Description',
    @value = N'Mengeneinheit',
    @level0type = N'SCHEMA',
    @level0name = N'dbo',
    @level1type = N'TABLE',
    @level1name = N'Mein',
    @level2type = N'COLUMN',
    @level2name = N'Name'
CREATE TABLE [pm].[OBJK]
(
	[OBJKID] BIGINT NOT NULL CONSTRAINT [PK_OBJKID] PRIMARY KEY IDENTITY (1, 1), 
	[OBJKGUID] UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(), 
	[OBJKTYP] CHAR(2) NOT NULL,
	[OBJKNR] NVARCHAR(20) NOT NULL, 
	[OBJKDATA] XML NULL
)

GO
EXEC sp_addextendedproperty @name = N'MS_Description',
    @value = N'Basic typ of the object',
    @level0type = N'SCHEMA',
    @level0name = N'pm',
    @level1type = N'TABLE',
    @level1name = N'OBJK',
    @level2type = N'COLUMN',
    @level2name = 'OBJKTYP'
GO
EXEC sp_addextendedproperty @name = N'MS_Description',
    @value = N'Extendet data',
    @level0type = N'SCHEMA',
    @level0name = N'pm',
    @level1type = N'TABLE',
    @level1name = N'OBJK',
    @level2type = N'COLUMN',
    @level2name = N'OBJKDATA'
GO

GO
EXEC sp_addextendedproperty @name = N'MS_Description',
    @value = N'unique number within the object type e.g. B12002 for BEKOID, ...',
    @level0type = N'SCHEMA',
    @level0name = N'pm',
    @level1type = N'TABLE',
    @level1name = N'OBJK',
    @level2type = N'COLUMN',
    @level2name = N'OBJKNR'
GO
EXEC sp_addextendedproperty @name = N'MS_Description',
    @value = N'internal unique Id for the object',
    @level0type = N'SCHEMA',
    @level0name = N'pm',
    @level1type = N'TABLE',
    @level1name = N'OBJK',
    @level2type = N'COLUMN',
    @level2name = N'OBJKID'
GO
EXEC sp_addextendedproperty @name = N'MS_Description',
    @value = N'Basic Hub for all main data entities.',
    @level0type = N'SCHEMA',
    @level0name = N'pm',
    @level1type = N'TABLE',
    @level1name = N'OBJK',
    @level2type = NULL,
    @level2name = NULL
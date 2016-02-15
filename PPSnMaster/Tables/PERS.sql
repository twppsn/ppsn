CREATE TABLE [pm].[PERS]
(
	[PERSID] BIGINT NOT NULL CONSTRAINT PK_PERSID PRIMARY KEY IDENTITY (1, 1), 
	[PERSOBJKID] BIGINT NOT NULL, 
	[PERSNAME] NVARCHAR(200) NOT NULL, 
	[PERSLOGIN] [sys].[sysname] COLLATE Latin1_General_CI_AS null, 
	[PERSLOGINVERSION] BIGINT NOT NULL DEFAULT 0, 
	[PERSSECURITY] NVARCHAR(MAX) NULL, 
	CONSTRAINT [FK_PERSID_OBJKID] FOREIGN KEY ([PERSOBJKID]) REFERENCES [pm].[OBJK]([OBJKID])
)

GO
EXEC sp_addextendedproperty @name = N'MS_Description',
    @value = N'Name of the user/employee',
    @level0type = N'SCHEMA',
    @level0name = N'pm',
    @level1type = N'TABLE',
    @level1name = N'PERS',
    @level2type = N'COLUMN',
    @level2name = N'PERSNAME'
GO
EXEC sp_addextendedproperty @name = N'MS_Description',
    @value = N'Reference to the object data',
    @level0type = N'SCHEMA',
    @level0name = N'pm',
    @level1type = N'TABLE',
    @level1name = N'PERS',
    @level2type = N'COLUMN',
    @level2name = N'PERSOBJKID'
GO
EXEC sp_addextendedproperty @name = N'MS_Description',
    @value = N'PK',
    @level0type = N'SCHEMA',
    @level0name = N'pm',
    @level1type = N'TABLE',
    @level1name = N'PERS',
    @level2type = N'COLUMN',
    @level2name = N'PERSID'
GO
EXEC sp_addextendedproperty @name = N'MS_Description',
    @value = N'Login name of the user',
    @level0type = N'SCHEMA',
    @level0name = N'pm',
    @level1type = N'TABLE',
    @level1name = N'PERS',
    @level2type = N'COLUMN',
    @level2name = 'PERSLOGIN'
GO
EXEC sp_addextendedproperty @name = N'MS_Description',
    @value = N'Security mask (tokens), for the user.',
    @level0type = N'SCHEMA',
    @level0name = N'pm',
    @level1type = N'TABLE',
    @level1name = N'PERS',
    @level2type = N'COLUMN',
    @level2name = N'PERSSECURITY'
go
alter table pm.PERS enable CHANGE_TRACKING
GO
EXEC sp_addextendedproperty @name = N'MS_Description',
    @value = N'Marker, if login data in the system was changed.',
    @level0type = N'SCHEMA',
    @level0name = N'pm',
    @level1type = N'TABLE',
    @level1name = N'PERS',
    @level2type = N'COLUMN',
    @level2name = N'PERSLOGINVERSION'
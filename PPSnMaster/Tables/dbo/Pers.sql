CREATE TABLE [dbo].[Pers]
(
	[Id] BIGINT NOT NULL CONSTRAINT pkPersId PRIMARY KEY IDENTITY (1,1), 
	--[KontId] BIGINT NOT NULL CONSTRAINT fkPersKontId REFERENCES dbo.Kont (Id), 
	[Name] NVARCHAR(100) NOT NULL, 
	[Inaktiv] SMALLDATETIME NULL,
	[Iban] CHAR(34) NULL, 
	[Bic] CHAR(11) NULL
)
GO
--CREATE INDEX [idxPersKontId] ON [dbo].[Pers] ([KontId])
GO
ALTER TABLE [dbo].[Pers] ENABLE CHANGE_TRACKING;
GO

EXEC sp_addextendedproperty @name = N'MS_Description',
    @value = N'PK',
    @level0type = N'SCHEMA',
    @level0name = N'dbo',
    @level1type = N'TABLE',
    @level1name = N'Pers',
    @level2type = N'COLUMN',
    @level2name = N'Id'
GO
--EXEC sp_addextendedproperty @name = N'MS_Description',
--    @value = N'FK, Kontakt',
--    @level0type = N'SCHEMA',
--    @level0name = N'dbo',
--    @level1type = N'TABLE',
--    @level1name = N'Pers',
--    @level2type = N'COLUMN',
--    @level2name = N'KontId'
GO
EXEC sp_addextendedproperty @name = N'MS_Description',
    @value = N'Name des Personals',
    @level0type = N'SCHEMA',
    @level0name = N'dbo',
    @level1type = N'TABLE',
    @level1name = N'Pers',
    @level2type = N'COLUMN',
    @level2name = N'Name'
GO
EXEC sp_addextendedproperty @name = N'MS_Description',
    @value = N'Inaktiv seit',
    @level0type = N'SCHEMA',
    @level0name = N'dbo',
    @level1type = N'TABLE',
    @level1name = N'Pers',
    @level2type = N'COLUMN',
    @level2name = N'Inaktiv'
CREATE TABLE [dbo].[Objk]
(
	[Id] BIGINT NOT NULL  CONSTRAINT pkObjkId PRIMARY KEY IDENTITY (1, 1),
    [Guid] UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(), 
    [Typ] CHAR(2) NOT NULL, 
    [Nr] NVARCHAR(20) NOT NULL,
	[CurRevId] BIGINT NULL CONSTRAINT fkObjkObjrCurId REFERENCES dbo.Objr (Id),
	[HeadRevId] BIGINT NULL CONSTRAINT fkObjkObjrHeadId REFERENCES dbo.Objr (Id)
)

GO
EXEC sp_addextendedproperty @name = N'MS_Description',
    @value = N'PK',
    @level0type = N'SCHEMA',
    @level0name = N'dbo',
    @level1type = N'TABLE',
    @level1name = N'Objk',
    @level2type = N'COLUMN',
    @level2name = N'Id'
GO
EXEC sp_addextendedproperty @name = N'MS_Description',
    @value = N'Type of the object e.g. KO-Contact, BE-Order',
    @level0type = N'SCHEMA',
    @level0name = N'dbo',
    @level1type = N'TABLE',
    @level1name = N'Objk',
    @level2type = N'COLUMN',
    @level2name = N'Typ'
GO
EXEC sp_addextendedproperty @name = N'MS_Description',
    @value = N'user visible number of the objekt e.g. B160399 - OrderNr',
    @level0type = N'SCHEMA',
    @level0name = N'dbo',
    @level1type = N'TABLE',
    @level1name = N'Objk',
    @level2type = N'COLUMN',
    @level2name = N'Nr'
GO
EXEC sp_addextendedproperty @name = N'MS_Description',
    @value = N'unique primary key for every object',
    @level0type = N'SCHEMA',
    @level0name = N'dbo',
    @level1type = N'TABLE',
    @level1name = N'Objk',
    @level2type = N'COLUMN',
    @level2name = N'Guid'
GO
EXEC sp_addextendedproperty @name = N'MS_Description',
    @value = N'Pointer to the revision, that is the newest',
    @level0type = N'SCHEMA',
    @level0name = N'dbo',
    @level1type = N'TABLE',
    @level1name = N'Objk',
    @level2type = N'COLUMN',
    @level2name = N'HeadRevId'
GO
EXEC sp_addextendedproperty @name = N'MS_Description',
    @value = N'Pointer to the revision, that is relational in sql database',
    @level0type = N'SCHEMA',
    @level0name = N'dbo',
    @level1type = N'TABLE',
    @level1name = N'Objk',
    @level2type = N'COLUMN',
    @level2name = N'CurRevId'
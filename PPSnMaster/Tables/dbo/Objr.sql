CREATE TABLE [dbo].[Objr]
(
	[Id] BIGINT NOT NULL CONSTRAINT pkObjrId PRIMARY KEY IDENTITY (1, 1),
	[ParentId] BIGINT NOT NULL CONSTRAINT fkObjrObjrId REFERENCES dbo.Objr (Id),
	[ObjkId] BIGINT NOT NULL CONSTRAINT fkObjrObjkId REFERENCES dbo.Objk (Id), 
    [Data] XML NULL, 
	[CreateDate] DATETIME NOT NULL DEFAULT getdate(),
	[CreateUserId] BIGINT NOT NULL CONSTRAINT fkObjrUserId REFERENCES dbo.[User] (Id)
)
GO
EXEC sp_addextendedproperty @name = N'MS_Description',
    @value = N'Extended data store',
    @level0type = N'SCHEMA',
    @level0name = N'dbo',
    @level1type = N'TABLE',
    @level1name = N'Objr',
    @level2type = N'COLUMN',
    @level2name = N'Data'
GO

EXEC sp_addextendedproperty @name = N'MS_Description',
    @value = N'Revision of of the object',
    @level0type = N'SCHEMA',
    @level0name = N'dbo',
    @level1type = N'TABLE',
    @level1name = N'Objr',
    @level2type = NULL,
    @level2name = NULL
GO
EXEC sp_addextendedproperty @name = N'MS_Description',
    @value = N'Revision Id of the object',
    @level0type = N'SCHEMA',
    @level0name = N'dbo',
    @level1type = N'TABLE',
    @level1name = N'Objr',
    @level2type = N'COLUMN',
    @level2name = N'Id'
GO
EXEC sp_addextendedproperty @name = N'MS_Description',
    @value = N'Foreignkey to the object',
    @level0type = N'SCHEMA',
    @level0name = N'dbo',
    @level1type = N'TABLE',
    @level1name = N'Objr',
    @level2type = N'COLUMN',
    @level2name = N'ObjkId'
GO
EXEC sp_addextendedproperty @name = N'MS_Description',
    @value = N'Parent revision',
    @level0type = N'SCHEMA',
    @level0name = N'dbo',
    @level1type = N'TABLE',
    @level1name = N'Objr',
    @level2type = N'COLUMN',
    @level2name = N'ParentId'
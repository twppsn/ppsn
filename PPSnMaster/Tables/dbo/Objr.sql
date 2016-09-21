CREATE TABLE [dbo].[Objr]
(
	[Id] BIGINT NOT NULL CONSTRAINT pkObjrId PRIMARY KEY CLUSTERED IDENTITY (1, 1),
	[ParentId] BIGINT NULL CONSTRAINT fkObjrObjrId REFERENCES dbo.Objr (Id),
	[ObjkId] BIGINT NOT NULL CONSTRAINT fkObjrObjkId REFERENCES dbo.Objk (Id), 
	[Tags] XML NOT NULL CONSTRAINT dfObjrTags DEFAULT '<tags />',
	[Document] XML NOT NULL, 
	[CreateDate] DATETIME NOT NULL CONSTRAINT dfObjrCreateDate DEFAULT getdate(),
	[CreateUserId] BIGINT NOT NULL CONSTRAINT fkObjrUserId REFERENCES dbo.[User] (Id)
)
GO
EXEC sp_addextendedproperty @name = N'MS_Description',
    @value = N'Document data',
    @level0type = N'SCHEMA',
    @level0name = N'dbo',
    @level1type = N'TABLE',
    @level1name = N'Objr',
    @level2type = N'COLUMN',
    @level2name = 'Document'
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
GO
EXEC sp_addextendedproperty @name = N'MS_Description',
    @value = N'Simple view data for search proposes (todo: ggf. eigene Tabelle)',
    @level0type = N'SCHEMA',
    @level0name = N'dbo',
    @level1type = N'TABLE',
    @level1name = N'Objr',
    @level2type = N'COLUMN',
    @level2name = N'Tags'
GO
CREATE INDEX [idxObjrObjkId] ON [dbo].[Objr] ([ObjkId])
GO
CREATE INDEX [idxObjrParentId] ON [dbo].[Objr] ([ParentId])
GO
CREATE INDEX [idxObjrUserId] ON [dbo].[Objr] ([CreateUserId])
GO
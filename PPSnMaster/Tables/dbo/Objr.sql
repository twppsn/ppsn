CREATE TABLE [dbo].[ObjR]
(
	[Id] BIGINT NOT NULL CONSTRAINT pkObjrId PRIMARY KEY CLUSTERED IDENTITY (1, 1),
	[ParentId] BIGINT NULL CONSTRAINT fkObjrObjrId REFERENCES dbo.ObjR (Id),
	[ObjkId] BIGINT NOT NULL CONSTRAINT fkObjrObjkId REFERENCES dbo.ObjK (Id), 
	[IsDocumentText] BIT DEFAULT 0 NOT NULL,
	[IsDocumentDeflate] BIT DEFAULT 0 NOT NULL,
	[Document] VARBINARY(MAX) NULL, 
	[DocumentId] BIGINT NULL CONSTRAINT fkObjrObjfId REFERENCES dbo.ObjF (Id),
	[DocumentLink] VARCHAR(MAX) NULL,
	[CreateDate] DATETIME NOT NULL CONSTRAINT dfObjrCreateDate DEFAULT getdate(),
	[CreateUserId] BIGINT NOT NULL CONSTRAINT fkObjrUserId REFERENCES dbo.[User] (Id)
)
GO
EXEC sp_addextendedproperty @name = N'MS_Description',
    @value = N'Document data (inline)',
    @level0type = N'SCHEMA',
    @level0name = N'dbo',
    @level1type = N'TABLE',
    @level1name = N'ObjR',
    @level2type = N'COLUMN',
    @level2name = 'Document'
GO

EXEC sp_addextendedproperty @name = N'MS_Description',
    @value = N'Revision of of the object',
    @level0type = N'SCHEMA',
    @level0name = N'dbo',
    @level1type = N'TABLE',
    @level1name = N'ObjR',
    @level2type = NULL,
    @level2name = NULL
GO
EXEC sp_addextendedproperty @name = N'MS_Description',
    @value = N'Revision Id of the object',
    @level0type = N'SCHEMA',
    @level0name = N'dbo',
    @level1type = N'TABLE',
    @level1name = N'ObjR',
    @level2type = N'COLUMN',
    @level2name = N'Id'
GO
EXEC sp_addextendedproperty @name = N'MS_Description',
    @value = N'Foreignkey to the object',
    @level0type = N'SCHEMA',
    @level0name = N'dbo',
    @level1type = N'TABLE',
    @level1name = N'ObjR',
    @level2type = N'COLUMN',
    @level2name = N'ObjkId'
GO
EXEC sp_addextendedproperty @name = N'MS_Description',
    @value = N'Parent revision',
    @level0type = N'SCHEMA',
    @level0name = N'dbo',
    @level1type = N'TABLE',
    @level1name = N'ObjR',
    @level2type = N'COLUMN',
    @level2name = N'ParentId'
GO

GO
CREATE INDEX [idxObjrObjkId] ON [dbo].[ObjR] ([ObjkId])
GO
CREATE INDEX [idxObjrParentId] ON [dbo].[ObjR] ([ParentId])
GO
CREATE INDEX [idxObjrObjfId] ON [dbo].[ObjR] ([DocumentId])
GO
CREATE INDEX [idxObjrUserId] ON [dbo].[ObjR] ([CreateUserId])
GO
EXEC sp_addextendedproperty @name = N'MS_Description',
    @value = N'Reference to the revision data',
    @level0type = N'SCHEMA',
    @level0name = N'dbo',
    @level1type = N'TABLE',
    @level1name = N'ObjR',
    @level2type = N'COLUMN',
    @level2name = N'DocumentId'
GO

EXEC sp_addextendedproperty @name = N'MS_Description',
    @value = N'Uri to a external data source',
    @level0type = N'SCHEMA',
    @level0name = N'dbo',
    @level1type = N'TABLE',
    @level1name = N'ObjR',
    @level2type = N'COLUMN',
    @level2name = N'DocumentLink'
GO

EXEC sp_addextendedproperty @name = N'MS_Description',
    @value = N'Is the document a utf8 encoded text',
    @level0type = N'SCHEMA',
    @level0name = N'dbo',
    @level1type = N'TABLE',
    @level1name = N'ObjR',
    @level2type = N'COLUMN',
    @level2name = N'IsDocumentText'
GO
EXEC sp_addextendedproperty @name = N'MS_Description',
    @value = N'Is the document packed with deflate',
    @level0type = N'SCHEMA',
    @level0name = N'dbo',
    @level1type = N'TABLE',
    @level1name = N'ObjR',
    @level2type = N'COLUMN',
    @level2name = N'IsDocumentDeflate'
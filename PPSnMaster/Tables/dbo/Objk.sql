CREATE TABLE [dbo].[ObjK]
(
	[Id] BIGINT NOT NULL  CONSTRAINT pkObjkId PRIMARY KEY IDENTITY (1, 1),
	[Guid] UNIQUEIDENTIFIER NOT NULL CONSTRAINT dfObjkGuid DEFAULT NEWID(), 
	[Typ] CHAR(25) NOT NULL CONSTRAINT dfObjkTyp CHECK (LEN(Typ) > 0 ), 
	[MimeType] VARCHAR(30) NULL,
	[Nr] NVARCHAR(20) NOT NULL CONSTRAINT chkObjkNr CHECK (LEN(Nr) > 0),
	[IsRev] BIT NOT NULL,
	[IsHidden] BIT NOT NULL CONSTRAINT dfObjkIsHidden DEFAULT 0,
	[CurRevId] BIGINT NULL CONSTRAINT fkObjkObjrCurId REFERENCES dbo.ObjR (Id),
	[HeadRevId] BIGINT NULL CONSTRAINT fkObjkObjrHeadId REFERENCES dbo.ObjR (Id)
)
GO
ALTER TABLE [dbo].[ObjK] ENABLE CHANGE_TRACKING;
GO
EXEC sp_addextendedproperty @name = N'MS_Description',
    @value = N'PK',
    @level0type = N'SCHEMA',
    @level0name = N'dbo',
    @level1type = N'TABLE',
    @level1name = N'ObjK',
    @level2type = N'COLUMN',
    @level2name = N'Id'
GO
EXEC sp_addextendedproperty @name = N'MS_Description',
    @value = N'Type of the object e.g. crmContacts-Contact, sdsOrder-Order',
    @level0type = N'SCHEMA',
    @level0name = N'dbo',
    @level1type = N'TABLE',
    @level1name = N'ObjK',
    @level2type = N'COLUMN',
    @level2name = N'Typ'
GO
EXEC sp_addextendedproperty @name = N'MS_Description',
    @value = N'user visible number of the objekt e.g. B160399 - OrderNr',
    @level0type = N'SCHEMA',
    @level0name = N'dbo',
    @level1type = N'TABLE',
    @level1name = N'ObjK',
    @level2type = N'COLUMN',
    @level2name = N'Nr'
GO
EXEC sp_addextendedproperty @name = N'MS_Description',
    @value = N'unique primary key for every object',
    @level0type = N'SCHEMA',
    @level0name = N'dbo',
    @level1type = N'TABLE',
    @level1name = N'ObjK',
    @level2type = N'COLUMN',
    @level2name = N'Guid'
GO
EXEC sp_addextendedproperty @name = N'MS_Description',
    @value = N'Pointer to the revision, that is the newest',
    @level0type = N'SCHEMA',
    @level0name = N'dbo',
    @level1type = N'TABLE',
    @level1name = N'ObjK',
    @level2type = N'COLUMN',
    @level2name = N'HeadRevId'
GO
EXEC sp_addextendedproperty @name = N'MS_Description',
    @value = N'Pointer to the revision, that is relational in sql database',
    @level0type = N'SCHEMA',
    @level0name = N'dbo',
    @level1type = N'TABLE',
    @level1name = N'ObjK',
    @level2type = N'COLUMN',
    @level2name = N'CurRevId'
GO

CREATE INDEX [idxObjkCurId] ON [dbo].[ObjK] ([CurRevId])
GO
CREATE INDEX [idxObjkHeadId] ON [dbo].[ObjK] ([HeadRevId])
GO
CREATE UNIQUE INDEX [idxObjkGuid] ON [dbo].[ObjK] ([Guid])
GO
CREATE UNIQUE INDEX [idxObjkTypNr] ON [dbo].[ObjK] ([Typ], [Nr]) INCLUDE ([Id])
GO

EXEC sp_addextendedproperty @name = N'MS_Description',
    @value = N'Is the object invisible to the user',
    @level0type = N'SCHEMA',
    @level0name = N'dbo',
    @level1type = N'TABLE',
    @level1name = N'ObjK',
    @level2type = N'COLUMN',
    @level2name = N'IsHidden'
GO
EXEC sp_addextendedproperty @name = N'MS_Description',
    @value = N'Has this object revisions, or is this only an hook',
    @level0type = N'SCHEMA',
    @level0name = N'dbo',
    @level1type = N'TABLE',
    @level1name = N'ObjK',
    @level2type = N'COLUMN',
    @level2name = N'IsRev'
GO

GO

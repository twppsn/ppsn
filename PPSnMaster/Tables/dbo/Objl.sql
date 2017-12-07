CREATE TABLE [dbo].[ObjL]
(
	[Id] INT NOT NULL CONSTRAINT pkObjLId PRIMARY KEY CLUSTERED IDENTITY (1, 1),
	[ParentObjKId] BIGINT NOT NULL CONSTRAINT fkParentObjLObjkId REFERENCES dbo.ObjK ([Id]) ON DELETE CASCADE,
	[ParentObjRId] BIGINT NULL CONSTRAINT fkParentObjLObjrId REFERENCES dbo.ObjR ([Id]) ON DELETE CASCADE,
	[LinkObjKId] BIGINT NULL CONSTRAINT fkLinkObjKId REFERENCES dbo.ObjK ([Id]) ON DELETE NO ACTION,
	[LinkObjRId] BIGINT NULL CONSTRAINT fkLinkObjRId REFERENCES dbo.ObjR ([Id]) ON DELETE NO ACTION,
	[RefCount] INT NOT NULL DEFAULT 0
);
GO
ALTER TABLE [dbo].[ObjL] ENABLE CHANGE_TRACKING;
GO
CREATE INDEX [idxObjlParentObjkId] ON [dbo].[ObjL] ([ParentObjKId])
GO
CREATE INDEX [idxObjlParentObjrId] ON [dbo].[ObjL] ([ParentObjRId])
GO
CREATE INDEX [idxObjlLinkObjkId] ON [dbo].[ObjL] ([LinkObjKId])
GO
CREATE INDEX [idxObjlLinkObjrId] ON [dbo].[ObjL] ([LinkObjRId])
GO
EXEC sp_addextendedproperty @name = N'MS_Description',
    @value = N'PK',
    @level0type = N'SCHEMA',
    @level0name = N'dbo',
    @level1type = N'TABLE',
    @level1name = N'ObjL',
    @level2type = N'COLUMN',
    @level2name = N'Id'
GO
EXEC sp_addextendedproperty @name = N'MS_Description',
    @value = N'FK to the object (Parent)',
    @level0type = N'SCHEMA',
    @level0name = N'dbo',
    @level1type = N'TABLE',
    @level1name = N'ObjL',
    @level2type = N'COLUMN',
    @level2name = N'ParentObjKId'
GO
EXEC sp_addextendedproperty @name = N'MS_Description',
    @value = N'FK to the revision (Parent)',
    @level0type = N'SCHEMA',
    @level0name = N'dbo',
    @level1type = N'TABLE',
    @level1name = N'ObjL',
    @level2type = N'COLUMN',
    @level2name = N'ParentObjRId'
GO
EXEC sp_addextendedproperty @name = N'MS_Description',
    @value = N'FK to the object (Link)',
    @level0type = N'SCHEMA',
    @level0name = N'dbo',
    @level1type = N'TABLE',
    @level1name = N'ObjL',
    @level2type = N'COLUMN',
    @level2name = N'LinkObjKId'
GO
EXEC sp_addextendedproperty @name = N'MS_Description',
    @value = N'FK to the revision (Link)',
    @level0type = N'SCHEMA',
    @level0name = N'dbo',
    @level1type = N'TABLE',
    @level1name = N'ObjL',
    @level2type = N'COLUMN',
    @level2name = N'LinkObjRId'
GO
EXEC sp_addextendedproperty @name = N'MS_Description',
    @value = N'Reference counter',
    @level0type = N'SCHEMA',
    @level0name = N'dbo',
    @level1type = N'TABLE',
    @level1name = N'ObjL',
    @level2type = N'COLUMN',
    @level2name = 'RefCount'
GO

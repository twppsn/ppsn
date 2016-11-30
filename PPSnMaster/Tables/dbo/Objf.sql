CREATE TABLE [dbo].[ObjF]
(
	[Id] BIGINT NOT NULL CONSTRAINT pkFileId PRIMARY KEY,
	[FileUId] UNIQUEIDENTIFIER NOT NULL ROWGUIDCOL CONSTRAINT uqFileUId UNIQUE DEFAULT NEWID(),
	[Md5] BINARY(16) NOT NULL,
	--[Data] VARBINARY(max) FILESTREAM NOT NULL
)

GO
EXEC sp_addextendedproperty @name = N'MS_Description',
    @value = N'PK',
    @level0type = N'SCHEMA',
    @level0name = N'dbo',
    @level1type = N'TABLE',
    @level1name = N'ObjF',
    @level2type = N'COLUMN',
    @level2name = N'Id'
GO
EXEC sp_addextendedproperty @name = N'MS_Description',
    @value = N'unique id',
    @level0type = N'SCHEMA',
    @level0name = N'dbo',
    @level1type = N'TABLE',
    @level1name = N'ObjF',
    @level2type = N'COLUMN',
    @level2name = N'FileUId'
GO
EXEC sp_addextendedproperty @name = N'MS_Description',
    @value = N'hash value of the file',
    @level0type = N'SCHEMA',
    @level0name = N'dbo',
    @level1type = N'TABLE',
    @level1name = N'ObjF',
    @level2type = N'COLUMN',
    @level2name = N'Md5'
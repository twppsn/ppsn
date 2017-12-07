CREATE TABLE [dbo].[Kgrp]
(
	[Id] BIGINT NOT NULL CONSTRAINT pkKgrpId PRIMARY KEY IDENTITY (1,1),
	[Name] NVARCHAR(50) NOT NULL,
	[IsActive] BIT NOT NULL CONSTRAINT dfKgrpIsActive DEFAULT  1
)
GO
ALTER TABLE [dbo].[Kgrp] ENABLE CHANGE_TRACKING;
GO
EXEC sp_addextendedproperty @name = N'MS_Description',
    @value = N'Primary Key',
    @level0type = N'SCHEMA',
    @level0name = N'dbo',
    @level1type = N'TABLE',
    @level1name = N'Kgrp',
    @level2type = N'COLUMN',
    @level2name = N'Id'
GO
EXEC sp_addextendedproperty @name = N'MS_Description',
    @value = N'Gruppenname',
    @level0type = N'SCHEMA',
    @level0name = N'dbo',
    @level1type = N'TABLE',
    @level1name = N'Kgrp',
    @level2type = N'COLUMN',
    @level2name = N'Name'
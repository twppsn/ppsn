CREATE TABLE [dbo].[Kgrp]
(
	[KnstId] BIGINT NOT NULL CONSTRAINT pkKgrpId PRIMARY KEY CONSTRAINT fkKgrpKnst REFERENCES dbo.Knst (Id),
	[Name] NVARCHAR(50) NOT NULL
)

GO
EXEC sp_addextendedproperty @name = N'MS_Description',
    @value = N'PK und FK zu Knst',
    @level0type = N'SCHEMA',
    @level0name = N'dbo',
    @level1type = N'TABLE',
    @level1name = N'Kgrp',
    @level2type = N'COLUMN',
    @level2name = N'KnstId'
GO
EXEC sp_addextendedproperty @name = N'MS_Description',
    @value = N'Gruppenname',
    @level0type = N'SCHEMA',
    @level0name = N'dbo',
    @level1type = N'TABLE',
    @level1name = N'Kgrp',
    @level2type = N'COLUMN',
    @level2name = N'Name'
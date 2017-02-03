CREATE TABLE [dbo].[Plzd]
(
	[KnstId] BIGINT NOT NULL CONSTRAINT pkPlzdId PRIMARY KEY CONSTRAINT fkPlzdKons REFERENCES dbo.Knst (Id), 
	[Plz] CHAR(5) NOT NULL, 
	[Ort] NVARCHAR(50) NOT NULL, 
	[LandId] BIGINT NOT NULL CONSTRAINT fkPlzdLand REFERENCES dbo.Land (Id), 
	[Vorwahl] CHAR(6) NULL, 
	[Kfz] CHAR(5) NULL, 
	[AmtSchl] NVARCHAR(12) NULL, 
	[RegSchl] NVARCHAR(10) NULL, 
	[Region] NVARCHAR(50) NULL
)

GO
EXEC sp_addextendedproperty @name = N'MS_Description',
    @value = N'Primary key, FK zu Knst',
    @level0type = N'SCHEMA',
    @level0name = N'dbo',
    @level1type = N'TABLE',
    @level1name = N'Plzd',
    @level2type = N'COLUMN',
    @level2name = N'KnstId'
GO
EXEC sp_addextendedproperty @name = N'MS_Description',
    @value = N'Postleitzahl',
    @level0type = N'SCHEMA',
    @level0name = N'dbo',
    @level1type = N'TABLE',
    @level1name = N'Plzd',
    @level2type = N'COLUMN',
    @level2name = N'Plz'
GO
EXEC sp_addextendedproperty @name = N'MS_Description',
    @value = N'Ort',
    @level0type = N'SCHEMA',
    @level0name = N'dbo',
    @level1type = N'TABLE',
    @level1name = N'Plzd',
    @level2type = N'COLUMN',
    @level2name = N'Ort'
GO
EXEC sp_addextendedproperty @name = N'MS_Description',
    @value = N'FK zu Land',
    @level0type = N'SCHEMA',
    @level0name = N'dbo',
    @level1type = N'TABLE',
    @level1name = N'Plzd',
    @level2type = N'COLUMN',
    @level2name = N'LandId'
GO
EXEC sp_addextendedproperty @name = N'MS_Description',
    @value = N'Vorwahl der Postleitzahl',
    @level0type = N'SCHEMA',
    @level0name = N'dbo',
    @level1type = N'TABLE',
    @level1name = N'Plzd',
    @level2type = N'COLUMN',
    @level2name = N'Vorwahl'
GO
EXEC sp_addextendedproperty @name = N'MS_Description',
    @value = N'Kfz-Kennzeichen',
    @level0type = N'SCHEMA',
    @level0name = N'dbo',
    @level1type = N'TABLE',
    @level1name = N'Plzd',
    @level2type = N'COLUMN',
    @level2name = N'Kfz'
GO
EXEC sp_addextendedproperty @name = N'MS_Description',
    @value = N'Amtlicher Gemeindeschlüssel',
    @level0type = N'SCHEMA',
    @level0name = N'dbo',
    @level1type = N'TABLE',
    @level1name = N'Plzd',
    @level2type = N'COLUMN',
    @level2name = N'AmtSchl'
GO
EXEC sp_addextendedproperty @name = N'MS_Description',
    @value = N'Regionalschlüssel',
    @level0type = N'SCHEMA',
    @level0name = N'dbo',
    @level1type = N'TABLE',
    @level1name = N'Plzd',
    @level2type = N'COLUMN',
    @level2name = N'RegSchl'
GO
EXEC sp_addextendedproperty @name = N'MS_Description',
    @value = N'Bundesland, Kanton',
    @level0type = N'SCHEMA',
    @level0name = N'dbo',
    @level1type = N'TABLE',
    @level1name = N'Plzd',
    @level2type = N'COLUMN',
    @level2name = N'Region'
GO

CREATE INDEX [idxPlzdLandId] ON [dbo].[Plzd] ([LandId])
GO
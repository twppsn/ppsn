CREATE TABLE [dbo].[Plzd]
(
	[Id] BIGINT NOT NULL CONSTRAINT pkPlzdId PRIMARY KEY IDENTITY (1,1), 
	[Plz] CHAR(5) NOT NULL, 
	[Ort] NVARCHAR(50) NOT NULL, 
	[LandId] BIGINT NOT NULL CONSTRAINT fkPlzdLand REFERENCES dbo.Land (Id), 
	[Vorwahl] CHAR(6) NULL, 
	[Kfz] CHAR(5) NULL, 
	[AmtSchl] CHAR(8) NULL, 
	[RegSchl] CHAR(12) NULL, 
	[Region] NVARCHAR(50) NULL,
	[IsActive] BIT NOT NULL CONSTRAINT dfPlzdIsActive DEFAULT  1
)
GO
ALTER TABLE [dbo].[Plzd] ENABLE CHANGE_TRACKING;
GO
EXEC sp_addextendedproperty @name = N'MS_Description',
    @value = N'Primary key',
    @level0type = N'SCHEMA',
    @level0name = N'dbo',
    @level1type = N'TABLE',
    @level1name = N'Plzd',
    @level2type = N'COLUMN',
    @level2name = N'Id'
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
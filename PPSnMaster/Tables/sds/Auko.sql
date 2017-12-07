CREATE TABLE [sds].[Auko]
(
	[Id] BIGINT NOT NULL CONSTRAINT pkAukoId PRIMARY KEY IDENTITY (1,1),
	[ObjkId] BIGINT NOT NULL CONSTRAINT fkAukoObjkId REFERENCES dbo.ObjK (Id), 
	[Datum] DATE NULL, 
	[BestDatum] DATE NULL, 
	[KontId] BIGINT NULL CONSTRAINT fkAukoKontId REFERENCES dbo.ObjK (Id), 
	[Adresse] NVARCHAR(1024) NULL, 
	[VeAdre] NVARCHAR(1024) NULL, 
	[ReAdre] NVARCHAR(1024) NULL, 
	[BestNr] NVARCHAR(20) NULL, 
	[Zusatz] NVARCHAR(128) NULL, 
	[PersId] BIGINT NULL, 
	[Ansp] BIGINT NULL, 
	[KopfText] NVARCHAR(MAX) NULL, 
	[FussText] NVARCHAR(MAX) NULL, 
	[Anmerk] NVARCHAR(MAX) NULL
)
GO
ALTER TABLE [sds].[Auko] ENABLE CHANGE_TRACKING;

GO
EXEC sp_addextendedproperty @name = N'MS_Description',
    @value = N'FK zu Objk',
    @level0type = N'SCHEMA',
    @level0name = N'sds',
    @level1type = N'TABLE',
    @level1name = N'Auko',
    @level2type = N'COLUMN',
    @level2name = N'ObjkId'
GO
EXEC sp_addextendedproperty @name = N'MS_Description',
    @value = N'Auftragsdatum',
    @level0type = N'SCHEMA',
    @level0name = N'sds',
    @level1type = N'TABLE',
    @level1name = N'Auko',
    @level2type = N'COLUMN',
    @level2name = N'Datum'
GO
EXEC sp_addextendedproperty @name = N'MS_Description',
    @value = N'Bestelldatum',
    @level0type = N'SCHEMA',
    @level0name = N'sds',
    @level1type = N'TABLE',
    @level1name = N'Auko',
    @level2type = N'COLUMN',
    @level2name = N'BestDatum'
GO
EXEC sp_addextendedproperty @name = N'MS_Description',
    @value = N'FK zu Kont',
    @level0type = N'SCHEMA',
    @level0name = N'sds',
    @level1type = N'TABLE',
    @level1name = N'Auko',
    @level2type = N'COLUMN',
    @level2name = N'KontId'
GO
EXEC sp_addextendedproperty @name = N'MS_Description',
    @value = N'Adresse des Kontakts',
    @level0type = N'SCHEMA',
    @level0name = N'sds',
    @level1type = N'TABLE',
    @level1name = N'Auko',
    @level2type = N'COLUMN',
    @level2name = N'Adresse'
GO
EXEC sp_addextendedproperty @name = N'MS_Description',
    @value = N'Versandadresse',
    @level0type = N'SCHEMA',
    @level0name = N'sds',
    @level1type = N'TABLE',
    @level1name = N'Auko',
    @level2type = N'COLUMN',
    @level2name = N'VeAdre'
GO
EXEC sp_addextendedproperty @name = N'MS_Description',
    @value = N'Rechnungsadresse',
    @level0type = N'SCHEMA',
    @level0name = N'sds',
    @level1type = N'TABLE',
    @level1name = N'Auko',
    @level2type = N'COLUMN',
    @level2name = N'ReAdre'
GO
EXEC sp_addextendedproperty @name = N'MS_Description',
    @value = N'Bestellnummer des Kontakts',
    @level0type = N'SCHEMA',
    @level0name = N'sds',
    @level1type = N'TABLE',
    @level1name = N'Auko',
    @level2type = N'COLUMN',
    @level2name = N'BestNr'
GO
EXEC sp_addextendedproperty @name = N'MS_Description',
    @value = N'Zusatzangaben zum Auftrag',
    @level0type = N'SCHEMA',
    @level0name = N'sds',
    @level1type = N'TABLE',
    @level1name = N'Auko',
    @level2type = N'COLUMN',
    @level2name = N'Zusatz'
GO
EXEC sp_addextendedproperty @name = N'MS_Description',
    @value = N'Sachbearbeiter, FK zu Pers',
    @level0type = N'SCHEMA',
    @level0name = N'sds',
    @level1type = N'TABLE',
    @level1name = N'Auko',
    @level2type = N'COLUMN',
    @level2name = N'PersId'
GO
EXEC sp_addextendedproperty @name = N'MS_Description',
    @value = N'Ansprechpartner, FK zu Ansp',
    @level0type = N'SCHEMA',
    @level0name = N'sds',
    @level1type = N'TABLE',
    @level1name = N'Auko',
    @level2type = N'COLUMN',
    @level2name = N'Ansp'
GO
EXEC sp_addextendedproperty @name = N'MS_Description',
    @value = N'Kopftext',
    @level0type = N'SCHEMA',
    @level0name = N'sds',
    @level1type = N'TABLE',
    @level1name = N'Auko',
    @level2type = N'COLUMN',
    @level2name = N'KopfText'
GO
EXEC sp_addextendedproperty @name = N'MS_Description',
    @value = N'Fusstext',
    @level0type = N'SCHEMA',
    @level0name = N'sds',
    @level1type = N'TABLE',
    @level1name = N'Auko',
    @level2type = N'COLUMN',
    @level2name = N'FussText'
GO
EXEC sp_addextendedproperty @name = N'MS_Description',
    @value = N'Anmerkungen',
    @level0type = N'SCHEMA',
    @level0name = N'sds',
    @level1type = N'TABLE',
    @level1name = N'Auko',
    @level2type = N'COLUMN',
    @level2name = N'Anmerk'
GO

CREATE INDEX [idxAukoObjkId] ON [sds].[Auko] ([ObjkId])
GO
CREATE INDEX [idxAukoKontId] ON [sds].[Auko] ([KontId])
GO

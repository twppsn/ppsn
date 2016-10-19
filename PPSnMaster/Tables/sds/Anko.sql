CREATE TABLE [sds].[Anko]
(
	[ObjkId] BIGINT NOT NULL CONSTRAINT pkAnkotId PRIMARY KEY CONSTRAINT fkAnkoObjkId REFERENCES dbo.Objk (Id), 
	[Datum] DATE NULL, 
	[BisDatum] DATE NULL, 
	[AnfDatum] DATE NULL, 
	[AnfNr] NVARCHAR(30) NULL, 
	[KontId] BIGINT NULL CONSTRAINT fkAnkoKontId REFERENCES dbo.Objk (Id), 
	[Adresse] NVARCHAR(1024) NULL, 
	[Variante] CHAR(2) NULL, 
	[Zusatz] NVARCHAR(128) NULL, 
	[PersId] BIGINT NULL, 
	[Ansp] BIGINT NULL, 
	[KopfText] NVARCHAR(MAX) NULL, 
	[Fusstext] NVARCHAR(MAX) NULL, 
	[Anmerk] NVARCHAR(MAX) NULL
)

GO
EXEC sp_addextendedproperty @name = N'MS_Description',
    @value = N'PK, FK zu Objk',
    @level0type = N'SCHEMA',
    @level0name = N'sds',
    @level1type = N'TABLE',
    @level1name = N'Anko',
    @level2type = N'COLUMN',
    @level2name = N'ObjkId'
GO
EXEC sp_addextendedproperty @name = N'MS_Description',
    @value = N'Angebotsdatum',
    @level0type = N'SCHEMA',
    @level0name = N'sds',
    @level1type = N'TABLE',
    @level1name = N'Anko',
    @level2type = N'COLUMN',
    @level2name = N'Datum'
GO
EXEC sp_addextendedproperty @name = N'MS_Description',
    @value = N'Angebot gültig bis',
    @level0type = N'SCHEMA',
    @level0name = N'sds',
    @level1type = N'TABLE',
    @level1name = N'Anko',
    @level2type = N'COLUMN',
    @level2name = N'BisDatum'
GO
EXEC sp_addextendedproperty @name = N'MS_Description',
    @value = N'Anfragedatum',
    @level0type = N'SCHEMA',
    @level0name = N'sds',
    @level1type = N'TABLE',
    @level1name = N'Anko',
    @level2type = N'COLUMN',
    @level2name = N'AnfDatum'
GO
EXEC sp_addextendedproperty @name = N'MS_Description',
    @value = N'Anfragenummer',
    @level0type = N'SCHEMA',
    @level0name = N'sds',
    @level1type = N'TABLE',
    @level1name = N'Anko',
    @level2type = N'COLUMN',
    @level2name = N'AnfNr'
GO
EXEC sp_addextendedproperty @name = N'MS_Description',
    @value = N'FK zu Kont',
    @level0type = N'SCHEMA',
    @level0name = N'sds',
    @level1type = N'TABLE',
    @level1name = N'Anko',
    @level2type = N'COLUMN',
    @level2name = N'KontId'
GO
EXEC sp_addextendedproperty @name = N'MS_Description',
    @value = N'Adresse',
    @level0type = N'SCHEMA',
    @level0name = N'sds',
    @level1type = N'TABLE',
    @level1name = N'Anko',
    @level2type = N'COLUMN',
    @level2name = N'Adresse'
GO
EXEC sp_addextendedproperty @name = N'MS_Description',
    @value = N'Variante des Angebots',
    @level0type = N'SCHEMA',
    @level0name = N'sds',
    @level1type = N'TABLE',
    @level1name = N'Anko',
    @level2type = N'COLUMN',
    @level2name = N'Variante'
GO
EXEC sp_addextendedproperty @name = N'MS_Description',
    @value = N'Bestellangaben',
    @level0type = N'SCHEMA',
    @level0name = N'sds',
    @level1type = N'TABLE',
    @level1name = N'Anko',
    @level2type = N'COLUMN',
    @level2name = N'Zusatz'
GO
EXEC sp_addextendedproperty @name = N'MS_Description',
    @value = N'Sachbearbeiter, FK zu Kont',
    @level0type = N'SCHEMA',
    @level0name = N'sds',
    @level1type = N'TABLE',
    @level1name = N'Anko',
    @level2type = N'COLUMN',
    @level2name = N'PersId'
GO
EXEC sp_addextendedproperty @name = N'MS_Description',
    @value = N'Ansprechpartner',
    @level0type = N'SCHEMA',
    @level0name = N'sds',
    @level1type = N'TABLE',
    @level1name = N'Anko',
    @level2type = N'COLUMN',
    @level2name = N'Ansp'
GO
EXEC sp_addextendedproperty @name = N'MS_Description',
    @value = N'Kopftext',
    @level0type = N'SCHEMA',
    @level0name = N'sds',
    @level1type = N'TABLE',
    @level1name = N'Anko',
    @level2type = N'COLUMN',
    @level2name = N'KopfText'
GO
EXEC sp_addextendedproperty @name = N'MS_Description',
    @value = N'Fusstext',
    @level0type = N'SCHEMA',
    @level0name = N'sds',
    @level1type = N'TABLE',
    @level1name = N'Anko',
    @level2type = N'COLUMN',
    @level2name = N'Fusstext'
GO
EXEC sp_addextendedproperty @name = N'MS_Description',
    @value = N'Anmerkung',
    @level0type = N'SCHEMA',
    @level0name = N'sds',
    @level1type = N'TABLE',
    @level1name = N'Anko',
    @level2type = N'COLUMN',
    @level2name = N'Anmerk'
GO

CREATE INDEX [idxAnkoKontId] ON [sds].[Anko] ([KontId])
GO

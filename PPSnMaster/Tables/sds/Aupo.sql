CREATE TABLE [sds].[Aupo]
(
	[Id] BIGINT NOT NULL CONSTRAINT pkAupotId PRIMARY KEY IDENTITY (1,1), 
	[AukoId] BIGINT NOT NULL CONSTRAINT fkAupoAukoId REFERENCES sds.Auko (Id), 
	[Pos] INT NOT NULL, 
	[ProdId] BIGINT NULL, 
	[ProdNr] NVARCHAR(20) NULL, 
	[Znr] NVARCHAR(50) NULL, 
	[Text] NVARCHAR(MAX) NULL, 
	[KontProdNr] NVARCHAR(50) NULL, 
	[Termin] DATE NULL, 
	[Menge] DECIMAL(15, 4) NULL
)

GO
EXEC sp_addextendedproperty @name = N'MS_Description',
    @value = N'FK zu Objk',
    @level0type = N'SCHEMA',
    @level0name = N'sds',
    @level1type = N'TABLE',
    @level1name = N'Aupo',
    @level2type = N'COLUMN',
    @level2name = N'AukoId'
GO
EXEC sp_addextendedproperty @name = N'MS_Description',
    @value = N'Id',
    @level0type = N'SCHEMA',
    @level0name = N'sds',
    @level1type = N'TABLE',
    @level1name = N'Aupo',
    @level2type = N'COLUMN',
    @level2name = N'Id'
GO
EXEC sp_addextendedproperty @name = N'MS_Description',
    @value = N'Position',
    @level0type = N'SCHEMA',
    @level0name = N'sds',
    @level1type = N'TABLE',
    @level1name = N'Aupo',
    @level2type = N'COLUMN',
    @level2name = N'Pos'
GO
EXEC sp_addextendedproperty @name = N'MS_Description',
    @value = N'Produkt, FK zu Prod',
    @level0type = N'SCHEMA',
    @level0name = N'sds',
    @level1type = N'TABLE',
    @level1name = N'Aupo',
    @level2type = N'COLUMN',
    @level2name = N'ProdId'
GO
EXEC sp_addextendedproperty @name = N'MS_Description',
    @value = N'Produktnummer',
    @level0type = N'SCHEMA',
    @level0name = N'sds',
    @level1type = N'TABLE',
    @level1name = N'Aupo',
    @level2type = N'COLUMN',
    @level2name = N'ProdNr'
GO
EXEC sp_addextendedproperty @name = N'MS_Description',
    @value = N'Zeichnungsnummer',
    @level0type = N'SCHEMA',
    @level0name = N'sds',
    @level1type = N'TABLE',
    @level1name = N'Aupo',
    @level2type = N'COLUMN',
    @level2name = N'Znr'
GO
EXEC sp_addextendedproperty @name = N'MS_Description',
    @value = N'Positionstext',
    @level0type = N'SCHEMA',
    @level0name = N'sds',
    @level1type = N'TABLE',
    @level1name = N'Aupo',
    @level2type = N'COLUMN',
    @level2name = N'Text'
GO
EXEC sp_addextendedproperty @name = N'MS_Description',
    @value = N'Produktnummer des Kontakts',
    @level0type = N'SCHEMA',
    @level0name = N'sds',
    @level1type = N'TABLE',
    @level1name = N'Aupo',
    @level2type = N'COLUMN',
    @level2name = N'KontProdNr'
GO
EXEC sp_addextendedproperty @name = N'MS_Description',
    @value = N'Termin',
    @level0type = N'SCHEMA',
    @level0name = N'sds',
    @level1type = N'TABLE',
    @level1name = N'Aupo',
    @level2type = N'COLUMN',
    @level2name = N'Termin'
GO
EXEC sp_addextendedproperty @name = N'MS_Description',
    @value = N'Menge der Position',
    @level0type = N'SCHEMA',
    @level0name = N'sds',
    @level1type = N'TABLE',
    @level1name = N'Aupo',
    @level2type = N'COLUMN',
    @level2name = N'Menge'
GO

CREATE INDEX [idxAupoObjkId] ON [sds].[Aupo] ([AukoId])
GO

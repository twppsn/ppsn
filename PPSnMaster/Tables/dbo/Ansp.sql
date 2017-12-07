CREATE TABLE [dbo].[Ansp]
(
	[Id] BIGINT NOT NULL CONSTRAINT pkAnspId PRIMARY KEY IDENTITY (1,1), 
	[AdreId] BIGINT NOT NULL CONSTRAINT fkAnspAdreId REFERENCES dbo.Adre (Id),
	[Name] NVARCHAR(100) NOT NULL, 
	[Vorname] NVARCHAR(100) NULL,
	[Titel] NVARCHAR(30) NULL, 
	[Tel] VARCHAR(30) NULL, 
	[Fax] VARCHAR(30) NULL, 
	[Mobil] VARCHAR(30) NULL, 
	[Mail] NVARCHAR(100) NULL, 
	[Std] BIT NOT NULL CONSTRAINT dfAnspStd DEFAULT 0, 
	[Geschl] CHAR NULL, 
	[Funktion] NVARCHAR(50) NULL, 
	[Brief] NVARCHAR(50) NULL, 
	[Anmerk] NVARCHAR(2048) NULL,
	[Postfach] NVARCHAR(20) NULL, 
	[Zusatz] NVARCHAR(20) NULL, 
	[Strasse] NVARCHAR(50) NULL, 
	[Ort] NVARCHAR(50) NULL, 
	[Region] NVARCHAR(50) NULL, 
	[Plz] NVARCHAR(10) NULL, 
	[Template] NVARCHAR(512) NULL, 
	[Adresse] NVARCHAR(512) NULL, 
	[Changed] BIT NULL, 

)
GO
ALTER TABLE [dbo].[Ansp] ENABLE CHANGE_TRACKING;
GO
EXEC sp_addextendedproperty @name = N'MS_Description',
    @value = N'PK',
    @level0type = N'SCHEMA',
    @level0name = N'dbo',
    @level1type = N'TABLE',
    @level1name = N'Ansp',
    @level2type = N'COLUMN',
    @level2name = N'Id'
GO

EXEC sp_addextendedproperty @name = N'MS_Description',
    @value = N'Name des Ansprechpartners',
    @level0type = N'SCHEMA',
    @level0name = N'dbo',
    @level1type = N'TABLE',
    @level1name = N'Ansp',
    @level2type = N'COLUMN',
    @level2name = N'Name'
GO

EXEC sp_addextendedproperty @name = N'MS_Description',
    @value = N'akademischer Titel',
    @level0type = N'SCHEMA',
    @level0name = N'dbo',
    @level1type = N'TABLE',
    @level1name = N'Ansp',
    @level2type = N'COLUMN',
    @level2name = N'Titel'
GO
EXEC sp_addextendedproperty @name = N'MS_Description',
    @value = N'Telefonnummer',
    @level0type = N'SCHEMA',
    @level0name = N'dbo',
    @level1type = N'TABLE',
    @level1name = N'Ansp',
    @level2type = N'COLUMN',
    @level2name = N'Tel'
GO
EXEC sp_addextendedproperty @name = N'MS_Description',
    @value = N'Faxnummer',
    @level0type = N'SCHEMA',
    @level0name = N'dbo',
    @level1type = N'TABLE',
    @level1name = N'Ansp',
    @level2type = N'COLUMN',
    @level2name = N'Fax'
GO
EXEC sp_addextendedproperty @name = N'MS_Description',
    @value = N'Mobilnummer (Handy)',
    @level0type = N'SCHEMA',
    @level0name = N'dbo',
    @level1type = N'TABLE',
    @level1name = N'Ansp',
    @level2type = N'COLUMN',
    @level2name = N'Mobil'
GO
EXEC sp_addextendedproperty @name = N'MS_Description',
    @value = N'Mailadresse',
    @level0type = N'SCHEMA',
    @level0name = N'dbo',
    @level1type = N'TABLE',
    @level1name = N'Ansp',
    @level2type = N'COLUMN',
    @level2name = N'Mail'
GO
EXEC sp_addextendedproperty @name = N'MS_Description',
    @value = N'Standardansprechpartner',
    @level0type = N'SCHEMA',
    @level0name = N'dbo',
    @level1type = N'TABLE',
    @level1name = N'Ansp',
    @level2type = N'COLUMN',
    @level2name = N'Std'
GO
EXEC sp_addextendedproperty @name = N'MS_Description',
    @value = N'Geschlecht (m=männlich, w=weiblich)',
    @level0type = N'SCHEMA',
    @level0name = N'dbo',
    @level1type = N'TABLE',
    @level1name = N'Ansp',
    @level2type = N'COLUMN',
    @level2name = N'Geschl'
GO
EXEC sp_addextendedproperty @name = N'MS_Description',
    @value = N'Funktion/Abteilung in der Firma',
    @level0type = N'SCHEMA',
    @level0name = N'dbo',
    @level1type = N'TABLE',
    @level1name = N'Ansp',
    @level2type = N'COLUMN',
    @level2name = N'Funktion'
GO
EXEC sp_addextendedproperty @name = N'MS_Description',
    @value = N'Briefanrede (z.B. Sehr geehrter Herr, Liebe Frau)',
    @level0type = N'SCHEMA',
    @level0name = N'dbo',
    @level1type = N'TABLE',
    @level1name = N'Ansp',
    @level2type = N'COLUMN',
    @level2name = N'Brief'
GO
EXEC sp_addextendedproperty @name = N'MS_Description',
    @value = N'Anmerkungen',
    @level0type = N'SCHEMA',
    @level0name = N'dbo',
    @level1type = N'TABLE',
    @level1name = N'Ansp',
    @level2type = N'COLUMN',
    @level2name = N'Anmerk'
GO
EXEC sp_addextendedproperty @name = N'MS_Description',
    @value = N'FK zu Adre',
    @level0type = N'SCHEMA',
    @level0name = N'dbo',
    @level1type = N'TABLE',
    @level1name = N'Ansp',
    @level2type = N'COLUMN',
    @level2name = 'AdreId'
GO
EXEC sp_addextendedproperty @name = N'MS_Description',
    @value = N'Postfach',
    @level0type = N'SCHEMA',
    @level0name = N'dbo',
    @level1type = N'TABLE',
    @level1name = N'Ansp',
    @level2type = N'COLUMN',
    @level2name = N'Postfach'
GO
EXEC sp_addextendedproperty @name = N'MS_Description',
    @value = N'Adress-Zusatz',
    @level0type = N'SCHEMA',
    @level0name = N'dbo',
    @level1type = N'TABLE',
    @level1name = N'Ansp',
    @level2type = N'COLUMN',
    @level2name = N'Zusatz'
GO
EXEC sp_addextendedproperty @name = N'MS_Description',
    @value = N'Straße mit Nr',
    @level0type = N'SCHEMA',
    @level0name = N'dbo',
    @level1type = N'TABLE',
    @level1name = N'Ansp',
    @level2type = N'COLUMN',
    @level2name = N'Strasse'
GO
EXEC sp_addextendedproperty @name = N'MS_Description',
    @value = N'Ort',
    @level0type = N'SCHEMA',
    @level0name = N'dbo',
    @level1type = N'TABLE',
    @level1name = N'Ansp',
    @level2type = N'COLUMN',
    @level2name = N'Ort'
GO
EXEC sp_addextendedproperty @name = N'MS_Description',
    @value = N'Region',
    @level0type = N'SCHEMA',
    @level0name = N'dbo',
    @level1type = N'TABLE',
    @level1name = N'Ansp',
    @level2type = N'COLUMN',
    @level2name = N'Region'
GO
EXEC sp_addextendedproperty @name = N'MS_Description',
    @value = N'Plz',
    @level0type = N'SCHEMA',
    @level0name = N'dbo',
    @level1type = N'TABLE',
    @level1name = N'Ansp',
    @level2type = N'COLUMN',
    @level2name = N'Plz'
GO
EXEC sp_addextendedproperty @name = N'MS_Description',
    @value = N'geändertes Template',
    @level0type = N'SCHEMA',
    @level0name = N'dbo',
    @level1type = N'TABLE',
    @level1name = N'Ansp',
    @level2type = N'COLUMN',
    @level2name = N'Template'
GO
EXEC sp_addextendedproperty @name = N'MS_Description',
    @value = N'vollständige Adresse',
    @level0type = N'SCHEMA',
    @level0name = N'dbo',
    @level1type = N'TABLE',
    @level1name = N'Ansp',
    @level2type = N'COLUMN',
    @level2name = N'Adresse'
GO
EXEC sp_addextendedproperty @name = N'MS_Description',
    @value = N'geänderte Adresse',
    @level0type = N'SCHEMA',
    @level0name = N'dbo',
    @level1type = N'TABLE',
    @level1name = N'Ansp',
    @level2type = N'COLUMN',
    @level2name = N'Changed'
GO

CREATE INDEX [idxAnspAdreId] ON [dbo].[Ansp] ([AdreId])
GO
EXEC sp_addextendedproperty @name = N'MS_Description',
    @value = N'Vorname',
    @level0type = N'SCHEMA',
    @level0name = N'dbo',
    @level1type = N'TABLE',
    @level1name = N'Ansp',
    @level2type = N'COLUMN',
    @level2name = N'Vorname'
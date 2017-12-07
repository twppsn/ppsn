CREATE TABLE [dbo].[Kund]
(
	[Id] BIGINT NOT NULL CONSTRAINT pkKundId PRIMARY KEY IDENTITY (1,1), 
	[KontId] BIGINT NOT NULL CONSTRAINT fkKundKontId REFERENCES dbo.Kont (Id), 
	[Name] NVARCHAR(100) NOT NULL, 
	[KurzName] NVARCHAR(25) NULL,
	[LiefNr] NVARCHAR(50) NULL, 
	[StIdentNr] VARCHAR(25) NULL, 
	[SteuerNr] VARCHAR(25) NULL, 
	[UstIdNr] CHAR(16) NULL, 
	[Inaktiv] SMALLDATETIME NULL,
	[Abc] CHAR NULL, 
	[KgrpId] BIGINT NULL CONSTRAINT fkKundKgrpId REFERENCES dbo.Kgrp (Id), 
	[Iban] CHAR(34) NULL, 
	[Bic] CHAR(11) NULL
)
GO
ALTER TABLE [dbo].[Kund] ENABLE CHANGE_TRACKING;
GO
EXEC sp_addextendedproperty @name = N'MS_Description',
    @value = N'FK zu Kontakt',
    @level0type = N'SCHEMA',
    @level0name = N'dbo',
    @level1type = N'TABLE',
    @level1name = N'Kund',
    @level2type = N'COLUMN',
    @level2name = N'KontId'
GO
EXEC sp_addextendedproperty @name = N'MS_Description',
    @value = N'Name des Kunden',
    @level0type = N'SCHEMA',
    @level0name = N'dbo',
    @level1type = N'TABLE',
    @level1name = N'Kund',
    @level2type = N'COLUMN',
    @level2name = N'Name'
GO
EXEC sp_addextendedproperty @name = N'MS_Description',
    @value = N'ext. Lieferantennummer',
    @level0type = N'SCHEMA',
    @level0name = N'dbo',
    @level1type = N'TABLE',
    @level1name = N'Kund',
    @level2type = N'COLUMN',
    @level2name = N'LiefNr'
GO
EXEC sp_addextendedproperty @name = N'MS_Description',
    @value = N'Steuerliche Identifikationsnummer',
    @level0type = N'SCHEMA',
    @level0name = N'dbo',
    @level1type = N'TABLE',
    @level1name = N'Kund',
    @level2type = N'COLUMN',
    @level2name = 'StIdentNr'
GO
EXEC sp_addextendedproperty @name = N'MS_Description',
    @value = N'Steuernummer',
    @level0type = N'SCHEMA',
    @level0name = N'dbo',
    @level1type = N'TABLE',
    @level1name = N'Kund',
    @level2type = N'COLUMN',
    @level2name = N'SteuerNr'
GO
EXEC sp_addextendedproperty @name = N'MS_Description',
    @value = N'Mehrwertsteuer-Identifikationsnummer',
    @level0type = N'SCHEMA',
    @level0name = N'dbo',
    @level1type = N'TABLE',
    @level1name = N'Kund',
    @level2type = N'COLUMN',
    @level2name = 'UstIdNr'
GO
EXEC sp_addextendedproperty @name = N'MS_Description',
    @value = N'Inaktiv seit',
    @level0type = N'SCHEMA',
    @level0name = N'dbo',
    @level1type = N'TABLE',
    @level1name = N'Kund',
    @level2type = N'COLUMN',
    @level2name = N'Inaktiv'
GO
EXEC sp_addextendedproperty @name = N'MS_Description',
    @value = N'Abc-Einteilung',
    @level0type = N'SCHEMA',
    @level0name = N'dbo',
    @level1type = N'TABLE',
    @level1name = N'Kund',
    @level2type = N'COLUMN',
    @level2name = N'Abc'
GO
EXEC sp_addextendedproperty @name = N'MS_Description',
    @value = N'FK zu Gruppe',
    @level0type = N'SCHEMA',
    @level0name = N'dbo',
    @level1type = N'TABLE',
    @level1name = N'Kund',
    @level2type = N'COLUMN',
    @level2name = N'KgrpId'
GO
EXEC sp_addextendedproperty @name = N'MS_Description',
    @value = N'Iban-Nummer',
    @level0type = N'SCHEMA',
    @level0name = N'dbo',
    @level1type = N'TABLE',
    @level1name = N'Kund',
    @level2type = N'COLUMN',
    @level2name = N'Iban'
GO
EXEC sp_addextendedproperty @name = N'MS_Description',
    @value = N'Bic-Nummer',
    @level0type = N'SCHEMA',
    @level0name = N'dbo',
    @level1type = N'TABLE',
    @level1name = N'Kund',
    @level2type = N'COLUMN',
    @level2name = N'Bic'
GO
EXEC sp_addextendedproperty @name = N'MS_Description',
    @value = N'Kurzname des Kunden, MatchCode',
    @level0type = N'SCHEMA',
    @level0name = N'dbo',
    @level1type = N'TABLE',
    @level1name = N'Kund',
    @level2type = N'COLUMN',
    @level2name = N'KurzName'
GO
EXEC sp_addextendedproperty @name = N'MS_Description',
    @value = N'PK',
    @level0type = N'SCHEMA',
    @level0name = N'dbo',
    @level1type = N'TABLE',
    @level1name = N'Kund',
    @level2type = N'COLUMN',
    @level2name = N'Id'
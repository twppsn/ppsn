CREATE TABLE [dbo].[Knst]
(
	[Id] BIGINT NOT NULL CONSTRAINT pkKnstId PRIMARY KEY IDENTITY (1,1),
	[Typ] CHAR(4) NOT NULL,
	[IsActive] BIT NOT NULL CONSTRAINT dfKnstIsActive DEFAULT  1,
	[Sync] DATETIME2 NOT NULL CONSTRAINT dfKnstSync DEFAULT SysUtcDateTime()
)

GO
EXEC sp_addextendedproperty @name = N'MS_Description',
    @value = N'PK',
    @level0type = N'SCHEMA',
    @level0name = N'dbo',
    @level1type = N'TABLE',
    @level1name = N'Knst',
    @level2type = N'COLUMN',
    @level2name = N'Id'
GO
EXEC sp_addextendedproperty @name = N'MS_Description',
    @value = N'Bezeichnung des Typs z.B. Land, Plzd',
    @level0type = N'SCHEMA',
    @level0name = N'dbo',
    @level1type = N'TABLE',
    @level1name = N'Knst',
    @level2type = N'COLUMN',
    @level2name = N'Typ'
GO
EXEC sp_addextendedproperty @name = N'MS_Description',
    @value = N'Konstante aktiv?',
    @level0type = N'SCHEMA',
    @level0name = N'dbo',
    @level1type = N'TABLE',
    @level1name = N'Knst',
    @level2type = N'COLUMN',
    @level2name = N'IsActive'
GO
EXEC sp_addextendedproperty @name = N'MS_Description',
    @value = N'Synchronisationszeit',
    @level0type = N'SCHEMA',
    @level0name = N'dbo',
    @level1type = N'TABLE',
    @level1name = N'Knst',
    @level2type = N'COLUMN',
    @level2name = N'Sync'
-- Mengeneinheiten
MERGE INTO dbo.Mein AS Target
USING (VALUES ('Stk'), ('kg'), ('m'), ('l'))
       AS Source (NeuName)
ON Target.Name = Source.NeuName
WHEN MATCHED THEN
    UPDATE SET Target.Name = Source.NeuName
WHEN NOT MATCHED BY TARGET THEN
    INSERT (Name) VALUES (NeuName);

-- Land
MERGE INTO dbo.Land AS Target
USING (VALUES ('Deutschland','Germany','DE','DEU','.de',49,'EU'), ('Österreich','Austria','AT','AUT','.at',43,'EU'), ('Schweiz','Schwitzerland','CH','CHE','.ch',41,null), ('Frankreich','France','FR','FRA','.fr',33,'EU'), 
('Russland','Russia','RU','RUS','.ru',7,null), ('Ungarn','Hungary','HU','HUN','.hu',null,null), ('Spanien','Spain','ES','ESP','.es',null,'EU'), ('Tschechien','Czech Republic','CZ','CZE','.cz',null,'EU'), ('Slowakei','Slovakia','SK','SVK','.sk',null,'EU'), 
('Italien','Italy','IT','ITL','.it',null,'EU'), ('Finnland','Finland','FI','FIN','.fi',null,'EU'))
       AS Source (NameDE, NameEN, Iso, Iso3, Tld, Vorwahl, Zone)
ON Target.Name = Source.NameDE
WHEN MATCHED THEN
    UPDATE SET Target.Name = Source.NameDE, Target.EnglishName = Source.NameEN, Target.Iso = Source.Iso, Target.Iso3 = Source.Iso3, Target.Tld = Source.Tld, Target.Vorwahl = Source.Vorwahl, Target.Zone = Source.Zone
WHEN NOT MATCHED BY TARGET THEN
    INSERT (Name, EnglishName, Iso, Iso3, Tld, Vorwahl, Zone) VALUES (Source.NameDE, Source.NameEN, Source.Iso, Source.Iso3, Source.Tld, Source.Vorwahl, Source.Zone);

-- Währungen
MERGE INTO dbo.Waeh AS Target
USING (VALUES ('Euro','€',1,'EUR',1), ('US-Dollar','$',1.0986,'USD',0), ('Pfund','£',0.9323,'GBP',0), ('Deutsch Mark','DM',2,'DEM',0))
       AS Source (Name, Symbol, Kurs, Iso, System)
ON Target.Name = Source.Name
WHEN MATCHED THEN
    UPDATE SET Target.Symbol = Source.Symbol, Target.Kurs = Source.Kurs, Target.Iso = Source.Iso, Target.System = Source.System
WHEN NOT MATCHED BY TARGET THEN
    INSERT (Name, Symbol, Kurs, Iso, System) VALUES (Source.Name, Source.Symbol, Source.Kurs, Source.Iso, Source.System);
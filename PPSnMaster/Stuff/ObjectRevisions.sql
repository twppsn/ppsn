CREATE VIEW [dbo].[ObjectRevisions]
	AS 
	SELECT 
			-- object
			k.Id, k.Guid, k.Nr, k.Typ, k.MimeType, k.CurRevId, k.HeadRevId, k.IsHidden,
			-- revision
			r.Id as RevId, r.ParentId as ParentRevId, r.IsDocumentText, r.IsDocumentDeflate, r.CreateDate, r.CreateUserId, r.Document, r.DocumentId, r.DocumentLink
		FROM dbo.ObjK k
			INNER JOIN dbo.ObjR r ON (k.Id = r.ObjkId)

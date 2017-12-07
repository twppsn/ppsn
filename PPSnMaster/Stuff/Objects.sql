CREATE VIEW [dbo].[Objects]
AS 
	SELECT
			o.Id
			, o.[Guid]
			, o.[Typ]
			, o.MimeType
			, o.Nr
			, o.IsRev
			, o.CurRevId
			, o.HeadRevId
			--, r.Id as RevId
			-- todo: test if aggregate function is faster?
			--, (SELECT CONCAT(t.[Key], ':', t.Class, ':', t.UserId, ':', t.SyncToken, '=', replace(t.Value, CHAR(10), '\n'), CHAR(10)) FROM dbo.ObjT t WHERE t.ObjKId = o.Id and t.ObjRId is null FOR XML PATH('')) as Tags
			--, (SELECT CONCAT(CASE ll.IsRemoved WHEN 0 THEN '+' ELSE '-' END, lo.Guid, ':', ll.OnDelete, CHAR(10)) FROM dbo.ObjL ll INNER JOIN dbo.ObjK lo ON (ll.LinkObjKId = lo.Id) WHERE ll.ParentObjKId = o.Id and ll.ParentObjRId is null) as LinksTo
			--, (SELECT CONCAT(CASE lr.IsRemoved WHEN 0 THEN '+' ELSE '-' END, lo.Guid, ':', lr.OnDelete, CHAR(10)) FROM dbo.ObjL lr INNER JOIN dbo.ObjK lo ON (lr.ParentObjKId = lo.Id) WHERE lr.LinkObjKId = o.Id and lr.LinkObjRId is null) as LinksFrom
		FROM dbo.ObjK o
			--LEFT OUTER JOIN dbo.ObjR r on (o.HeadRevId = r.Id)
			--LEFT OUTER JOIN dbo.ObjT t on (t.ObjKId = o.Id and t.ObjRId is null)
			--LEFT OUTER JOIN dbo.ObjL ll on (ll.ParentObjKId = o.Id and ll.ParentObjRId is null)
			--LEFT OUTER JOIN dbo.ObjL lr on (lr.ParentObjKId = o.Id and lr.ParentObjRId is null)
		--GROUP BY o.Id, o.Guid, o.Typ, o.Nr, o.IsRev

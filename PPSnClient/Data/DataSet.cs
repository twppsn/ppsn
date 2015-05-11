using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TecWare.PPSn.Data
{
	/////////////////////////////////////////////////////////////////////////////////
	///// <summary></summary>
	//public sealed class PpsDataSetClient : PPSnDataSet
	//{
	//	private static readonly XName xnTable = XName.Get("table");
	//	private static readonly XName xnMeta = XName.Get("meta");

	//	#region -- class PpsDataColumnMetaCollectionClient --------------------------------

	//	///////////////////////////////////////////////////////////////////////////////
	//	/// <summary></summary>
	//	private sealed class PpsDataColumnMetaCollectionClient : PPSnDataColumnClass.PPSnDataColumnMetaCollection
	//	{
	//		public PpsDataColumnMetaCollectionClient(XElement xMetaGroup)
	//		{
	//			AddMetaGroup(xMetaGroup, Add);
	//		} // ctor
	//	} // class PpsDataColumnMetaCollectionClient

	//	#endregion

	//	#region -- class PpsDataColumnClient ----------------------------------------------

	//	///////////////////////////////////////////////////////////////////////////////
	//	/// <summary></summary>
	//	private sealed class PpsDataColumnClient : PPSnDataColumnClass
	//	{
	//		private PpsDataColumnMetaCollectionClient metaInfo;

	//		public PpsDataColumnClient(PpsDataTableClientClass table, XElement xColumn)
	//			: base(table, xColumn.GetAttribute("name", String.Empty), LuaType.GetType(xColumn.GetAttribute("datatype", "object"), false, true).Type)
	//		{
	//			metaInfo = new PpsDataColumnMetaCollectionClient(xColumn);
	//		} // ctor

	//		public override PPSnDataColumnMetaCollection Meta { get { return metaInfo; } }
	//	} // class PpsDataColumnClient

	//	#endregion

	//	#region -- class PpsDataTableMetaCollectionClient ---------------------------------

	//	///////////////////////////////////////////////////////////////////////////////
	//	/// <summary></summary>
	//	private sealed class PpsDataTableMetaCollectionClient : PPSnDataTableClass.PPSnDataTableMetaCollection
	//	{
	//		public PpsDataTableMetaCollectionClient(XElement xMetaGroup)
	//		{
	//			AddMetaGroup(xMetaGroup, Add);
	//		} // ctor
	//	} // class PpsDataTableMetaCollectionClient

	//	#endregion

	//	#region -- class PpsDataTableClientClass ------------------------------------------

	//	///////////////////////////////////////////////////////////////////////////////
	//	/// <summary></summary>
	//	private sealed class PpsDataTableClientClass : PPSnDataTableClass
	//	{
	//		private PpsDataTableMetaCollectionClient metaInfo;

	//		public PpsDataTableClientClass(PpsDataSetClientClass dataset, XElement xTable)
	//			: base(xTable.GetAttribute("name", String.Empty))
	//		{
	//			foreach (XElement c in xTable.Elements())
	//				if (c.Name.LocalName == "column")
	//					AddColumn(new PpsDataColumnClient(this, c));
	//				else if (c.Name.LocalName == "meta")
	//					metaInfo = new PpsDataTableMetaCollectionClient(c);
	//		} // ctor

	//		public override PPSnDataTableMetaCollection Meta { get { return metaInfo; } }
	//	} // class PpsDataTableClientClass

	//	#endregion

	//	#region -- class PpsDataSetMetaCollectionClient -----------------------------------

	//	///////////////////////////////////////////////////////////////////////////////
	//	/// <summary></summary>
	//	private sealed class PpsDataSetMetaCollectionClient : PPSnDataSetClass.PPSnDataSetMetaCollection
	//	{
	//		public PpsDataSetMetaCollectionClient(XElement xMetaGroup)
	//		{
	//			AddMetaGroup(xMetaGroup, Add);
	//		} // ctor
	//	} // class PpsDataSetMetaCollectionClient

	//	#endregion

	//	#region -- class PpsDataSetClientClass --------------------------------------------

	//	///////////////////////////////////////////////////////////////////////////////
	//	/// <summary></summary>
	//	private class PpsDataSetClientClass : PPSnDataSetClass
	//	{
	//		private PpsDataSetMetaCollectionClient metaInfo;

	//		public PpsDataSetClientClass(XElement xSchema)
	//		{
	//			// Lade die Tabellen
	//			foreach (XElement c in xSchema.Elements())
	//			{
	//				if (c.Name == xnTable)
	//					Add(new PpsDataTableClientClass(this, c));
	//				else if (c.Name == xnMeta)
	//					metaInfo = new PpsDataSetMetaCollectionClient(c);
	//			}

	//			// Immer MetaDaten erzeugen
	//			if (metaInfo == null)
	//				metaInfo = new PpsDataSetMetaCollectionClient(new XElement("meta"));
	//		} // ctor

	//		public override PPSnDataSet CreateDataSet()
	//		{
	//			return new PpsDataSetClient(this);
	//		} // func CreateDataSet

	//		public override PPSnDataSetMetaCollection Meta { get { return metaInfo; } }
	//	} // class PpsDataSetClientClass

	//	#endregion

	//	private DEClient server;
	//	private string sPath;

	//	private PpsDataSetClient(PPSnDataSetClass datasetClass)
	//		: base(datasetClass)
	//	{
	//		this.server = null;
	//		this.sPath = null;
	//	} // ctor

	//	public PpsDataSetClient(DEClient server, string sPath)
	//		: base(LoadSchema(server, sPath))
	//	{
	//		this.server = server;
	//		this.sPath = sPath;
	//	} // ctor

	//	public void Load(string sArguments)
	//	{
	//		Read(server.GetXmlAsync(sPath + "?action=load&" + sArguments).Result.Root);
	//	} // proc Load

	//	private static PPSnDataSetClass LoadSchema(DEClient server, string sPath)
	//	{
	//		return LoadSchema(server.GetXmlAsync(sPath + "?action=schema").Result.Root);
	//	} // proc LoadSchema

	//	public static PPSnDataSetClass LoadSchema(XElement xSchema)
	//	{
	//		return new PpsDataSetClientClass(xSchema);
	//	} // func LoadSchema

	//	private static void AddMetaGroup(XElement xMetaGroup, Action<string, Func<Type>, object> add)
	//	{
	//		foreach (XElement c in xMetaGroup.Elements())
	//			add(c.Name.LocalName, () => LuaType.GetType(c.GetAttribute("datatype", "object"), lLateAllowed: false), c.Value);
	//	} // proc AddMetaGroup
	//} // class PpsDataSetClient
}

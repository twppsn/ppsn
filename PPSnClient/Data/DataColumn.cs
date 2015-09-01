using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using Neo.IronLua;
using TecWare.DES.Stuff;

namespace TecWare.PPSn.Data
{
	#region -- class PpsDataColumnMetaCollectionClient ----------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	internal sealed class PpsDataColumnMetaCollectionClient : PpsDataColumnDefinition.PpsDataColumnMetaCollection
	{
		public PpsDataColumnMetaCollectionClient(XElement xMetaGroup)
		{
			PpsDataHelperClient.AddMetaGroup(xMetaGroup, Add);
		} // ctor
	} // class PpsDataColumnMetaCollectionClient

	#endregion

	#region -- class PpsDataColumnDefinitionClient --------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public sealed class PpsDataColumnDefinitionClient : PpsDataColumnDefinition
	{
		private PpsDataColumnMetaCollectionClient metaInfo;

		public PpsDataColumnDefinitionClient(PpsDataTableDefinitionClient table, XElement xColumn)
			: base(table, xColumn.GetAttribute("name", (string)null), LuaType.GetType(xColumn.GetAttribute("datatype", "object"), lLateAllowed: false).Type)
		{
			metaInfo = new PpsDataColumnMetaCollectionClient(xColumn);
		} // ctor

		public override PpsDataColumnMetaCollection Meta => metaInfo;
	} // class PpsDataColumnDefinitionClient

	#endregion

	#region -- class PpsDataRelationColumnClientDefinition ------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public sealed class PpsDataRelationColumnClientDefinition : PpsDataRelationColumnDefinition
	{
		private PpsDataColumnMetaCollectionClient metaInfo;

		public PpsDataRelationColumnClientDefinition(PpsDataTableDefinitionClient table, XElement xRelation)
			:base(table, xRelation.GetAttribute("name", (string)null), xRelation.GetAttribute("relation", (string)null), table.ResolveColumn(xRelation))
		{
			metaInfo = new PpsDataColumnMetaCollectionClient(xRelation);
		} // ctor

		public override PpsDataColumnMetaCollection Meta => metaInfo;
	} // class PpsDataRelationColumnClientDefinition

	#endregion
}

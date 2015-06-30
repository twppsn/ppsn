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
	#region -- class PpsDataColumnClientDefinition --------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public sealed class PpsDataColumnClientDefinition : PpsDataColumnDefinition
	{
		#region -- class PpsDataColumnMetaCollectionClient --------------------------------

		///////////////////////////////////////////////////////////////////////////////
		/// <summary></summary>
		private sealed class PpsDataColumnMetaCollectionClient : PpsDataColumnMetaCollection
		{
			public PpsDataColumnMetaCollectionClient(XElement xMetaGroup)
			{
				PpsDataHelperClient.AddMetaGroup(xMetaGroup, Add);
			} // ctor
		} // class PpsDataColumnMetaCollectionClient

		#endregion

		private PpsDataColumnMetaCollectionClient metaInfo;

		public PpsDataColumnClientDefinition(PpsDataTableDefinitionClient table, XElement xColumn)
			: base(table, xColumn.GetAttribute("name", String.Empty), LuaType.GetType(xColumn.GetAttribute("datatype", "object"), lLateAllowed: false).Type)
		{
			metaInfo = new PpsDataColumnMetaCollectionClient(xColumn);
		} // ctor

		public override PpsDataColumnMetaCollection Meta { get { return metaInfo; } }
	} // class PpsDataColumnClientDefinition

	#endregion
}

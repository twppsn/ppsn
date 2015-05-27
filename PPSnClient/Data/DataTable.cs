using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using TecWare.DES.Stuff;

namespace TecWare.PPSn.Data
{
	#region -- class PpsDataTableClientDefinition ---------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public sealed class PpsDataTableClientDefinition : PpsDataTableDefinition
	{
		#region -- class PpsDataTableMetaCollectionClient ---------------------------------

		///////////////////////////////////////////////////////////////////////////////
		/// <summary></summary>
		private sealed class PpsDataTableMetaCollectionClient : PpsDataTableMetaCollection
		{
			public PpsDataTableMetaCollectionClient(XElement xMetaGroup)
			{
				PpsDataHelperClient.AddMetaGroup(xMetaGroup, Add);
			} // ctor
		} // class PpsDataTableMetaCollectionClient

		#endregion

		private PpsDataTableMetaCollectionClient metaInfo;

		public PpsDataTableClientDefinition(PpsDataSetClientDefinition dataset, XElement xTable)
			: base(xTable.GetAttribute("name", String.Empty))
		{
			foreach (XElement c in xTable.Elements())
			{
				if (c.Name.LocalName == "column")
					AddColumn(new PpsDataColumnClientDefinition(this, c));
				else if (c.Name.LocalName == "meta")
					metaInfo = new PpsDataTableMetaCollectionClient(c);
			}
		} // ctor

		public override PpsDataTableMetaCollection Meta { get { return metaInfo; } }
	} // class PpsDataTableClientClass

	#endregion
}

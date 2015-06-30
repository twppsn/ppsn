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
	public sealed class PpsDataTableDefinitionClient : PpsDataTableDefinition
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

		public PpsDataTableDefinitionClient(PpsDataSetDefinitionClient dataset, XElement xTable)
			: base(xTable.GetAttribute("name", String.Empty))
		{
			foreach (XElement c in xTable.Elements())
			{
                if (c.Name.LocalName == "column")
                    AddColumn(new PpsDataColumnClientDefinition(this, c));
                else if (c.Name.LocalName == "meta")
                    metaInfo = new PpsDataTableMetaCollectionClient(c);
                else
                    throw new NotSupportedException(
                        string.Format("Nicht unterstütztes Element, Name: '{0}', in der Datendefinition. \nBitte Definitionsdatei '*.sxml' korrigieren."
                        , c.Name.LocalName));
			}
		} // ctor

		public override PpsDataTableMetaCollection Meta { get { return metaInfo; } }
	} // class PpsDataTableClientClass

	#endregion
}

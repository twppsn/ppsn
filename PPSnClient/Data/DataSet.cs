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
	#region -- class PpsDataSetClientDefinition -----------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public sealed class PpsDataSetDefinitionClient : PpsDataSetDefinition
	{
		#region -- class PpsDataSetMetaCollectionClient -----------------------------------

		///////////////////////////////////////////////////////////////////////////////
		/// <summary></summary>
		private sealed class PpsDataSetMetaCollectionClient : PpsDataSetMetaCollection
		{
			public PpsDataSetMetaCollectionClient(XElement xMetaGroup)
			{
				PpsDataHelperClient.AddMetaGroup(xMetaGroup, Add);
			} // ctor
		} // class PpsDataSetMetaCollectionClient

		#endregion

		private PpsDataSetMetaCollectionClient metaInfo;

		public PpsDataSetDefinitionClient(XElement xSchema)
		{
			// Lade die Tabellen
			foreach (XElement c in xSchema.Elements())
			{
				if (c.Name == PpsDataHelperClient.xnTable)
					Add(new PpsDataTableDefinitionClient(this, c));
				else if (c.Name == PpsDataHelperClient.xnMeta)
					metaInfo = new PpsDataSetMetaCollectionClient(c);
			}

			// Immer MetaDaten erzeugen
			if (metaInfo == null)
				metaInfo = new PpsDataSetMetaCollectionClient(new XElement("meta"));
		} // ctor

		public override PpsDataSet CreateDataSet()
		{
			return new PpsDataSetClient(this);
		} // func CreateDataSet

		public override PpsDataSetMetaCollection Meta { get { return metaInfo; } }
	} // class PpsDataSetClientDefinition

	#endregion

	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public sealed class PpsDataSetClient : PpsDataSet
	{
        public UndoRedo UndoRedo { get; private set; }

		internal PpsDataSetClient(PpsDataSetDefinition datasetDefinition)
			: base(datasetDefinition)
		{
            UndoRedo = new UndoRedo();
		} // ctor
	} // class PpsDataSetClient
}

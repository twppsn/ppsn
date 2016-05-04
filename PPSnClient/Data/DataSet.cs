#region -- copyright --
//
// Licensed under the EUPL, Version 1.1 or - as soon they will be approved by the
// European Commission - subsequent versions of the EUPL(the "Licence"); You may
// not use this work except in compliance with the Licence.
//
// You may obtain a copy of the Licence at:
// http://ec.europa.eu/idabc/eupl
//
// Unless required by applicable law or agreed to in writing, software distributed
// under the Licence is distributed on an "AS IS" basis, WITHOUT WARRANTIES OR
// CONDITIONS OF ANY KIND, either express or implied. See the Licence for the
// specific language governing permissions and limitations under the Licence.
//
#endregion
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using Neo.IronLua;
using TecWare.DE.Stuff;

using static TecWare.PPSn.Data.PpsDataHelperClient;

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
				if (c.Name == xnTable)
					Add(new PpsDataTableDefinitionClient(this, c));
				else if (c.Name == xnMeta)
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
		internal PpsDataSetClient(PpsDataSetDefinition datasetDefinition)
			: base(datasetDefinition)
		{
		} // ctor
	} // class PpsDataSetClient
}

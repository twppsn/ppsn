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
using System.Xml.Linq;
using TecWare.DE.Stuff;

using static TecWare.PPSn.Data.PpsDataHelperClient;

namespace TecWare.PPSn.Data
{
	#region -- class PpsDataTableClientDefinition -------------------------------------

	/// <summary></summary>
	public class PpsDataTableDefinitionClient : PpsDataTableDefinition
	{
		#region -- class PpsDataTableMetaCollectionClient -----------------------------

		private sealed class PpsDataTableMetaCollectionClient : PpsDataTableMetaCollection
		{
			public PpsDataTableMetaCollectionClient()
			{
			} // ctor

			public PpsDataTableMetaCollectionClient(PpsDataTableMetaCollectionClient clone)
				: base(clone)
			{
			} // ctor

			public PpsDataTableMetaCollectionClient(XElement xMetaGroup)
			{
				PpsDataHelperClient.AddMetaGroup(xMetaGroup, Add);
			} // ctor
		} // class PpsDataTableMetaCollectionClient

		#endregion

		private PpsDataTableMetaCollectionClient metaInfo;

		private PpsDataTableDefinitionClient(PpsDataSetDefinitionClient dataset, PpsDataTableDefinitionClient clone)
			: base(dataset, clone)
		{
			this.metaInfo = new PpsDataTableMetaCollectionClient(clone.metaInfo);
		} // ctor

		/// <summary></summary>
		/// <param name="dataset"></param>
		/// <param name="xTable"></param>
		public PpsDataTableDefinitionClient(PpsDataSetDefinitionClient dataset, XElement xTable)
			: base(dataset, xTable.GetAttribute("name", String.Empty))
		{
			foreach (var c in xTable.Elements())
			{
				if (c.Name == xnColumn)
					AddColumn(new PpsDataColumnDefinitionClient(this, c));
				else if (c.Name == xnMeta)
					metaInfo = new PpsDataTableMetaCollectionClient(c);
				else // todo: warning
					throw new NotSupportedException($"Not supported element: {c.Name.LocalName}");
			}

			this.metaInfo = metaInfo ?? new PpsDataTableMetaCollectionClient();
		} // ctor

		/// <summary></summary>
		protected override void EndInit()
		{
			foreach (var c in Columns)
				c.EndInit();

			base.EndInit();
		} // proc EndInit

		/// <summary></summary>
		/// <param name="dataset"></param>
		/// <returns></returns>
		public override PpsDataTableDefinition Clone(PpsDataSetDefinition dataset)
			=> new PpsDataTableDefinitionClient((PpsDataSetDefinitionClient)dataset, this);

		/// <summary></summary>
		public override PpsDataTableMetaCollection Meta => metaInfo ?? PpsDataTableMetaCollection.Empty;
	} // class PpsDataTableDefinitionClient

	#endregion
}

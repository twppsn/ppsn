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
using TecWare.DE.Stuff;

using static TecWare.PPSn.Data.PpsDataHelperClient;

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
			: base(dataset, xTable.GetAttribute("name", String.Empty))
		{
			ParseTable(xTable);
		} // ctor

		internal void ParseTable(XElement xTable)
		{
			foreach (XElement c in xTable.Elements())
			{
				if (c.Name == xnColumn)
					AddColumn(new PpsDataColumnDefinitionClient(this, c));
				else if (c.Name == xnMeta)
					metaInfo = new PpsDataTableMetaCollectionClient(c);
				else // todo: warning
					throw new NotSupportedException(string.Format("Nicht unterstütztes Element, Name: '{0}', in der Datendefinition. \nBitte Definitionsdatei '*.sxml' korrigieren.", c.Name.LocalName));
			}
		} // func ParseTable

		internal PpsDataColumnDefinition ResolveColumn(XElement xColumn)
		{
			var tableName = xColumn.GetAttribute("table", (string)null);
			var columnName = xColumn.GetAttribute("column", (string)null);

			var table = DataSet.FindTable(tableName);
			if (table == null)
				throw new ArgumentException($"Table '{tableName}' not found.");

			var column = table.FindColumn(columnName);
			if (column == null)
				throw new ArgumentException($"Column '{columnName}' in '{tableName}' not found.");

			return column;
		} // func FindColumn

		public override PpsDataTableMetaCollection Meta { get { return metaInfo; } }
	} // class PpsDataTableClientClass

	#endregion
}

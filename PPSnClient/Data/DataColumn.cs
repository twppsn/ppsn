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

namespace TecWare.PPSn.Data
{
	#region -- class PpsDataColumnMetaCollectionClient ----------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	internal sealed class PpsDataColumnMetaCollectionClient : PpsDataColumnDefinition.PpsDataColumnMetaCollection
	{
		public PpsDataColumnMetaCollectionClient(PpsDataColumnMetaCollectionClient clone)
			: base(clone)
		{
		} // ctor

		public PpsDataColumnMetaCollectionClient(XElement xMetaGroup)
		{
			PpsDataHelperClient.AddMetaGroup(xMetaGroup, Add);
		} // ctor
	} // class PpsDataColumnMetaCollectionClient

	#endregion

	#region -- class PpsDataValueColumnDefinitionClient ---------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public sealed class PpsDataColumnDefinitionClient : PpsDataColumnDefinition
	{
		private readonly PpsDataColumnMetaCollectionClient metaInfo;
		private readonly Type dataType;

		private readonly string parentRelationName;
		private readonly string parentTableName;
		private readonly string parentColumn;

		private PpsDataColumnDefinitionClient(PpsDataTableDefinition table, PpsDataColumnDefinitionClient clone)
			: base(table, clone)
		{
			this.metaInfo = new PpsDataColumnMetaCollectionClient(clone.metaInfo);
			this.dataType = clone.dataType;

			this.parentRelationName = clone.parentRelationName;
			this.parentTableName = clone.parentTableName;
			this.parentColumn = clone.parentColumn;
		} // ctor

		public PpsDataColumnDefinitionClient(PpsDataTableDefinitionClient table, XElement xColumn)
			: base(table, xColumn.GetAttribute("name", (string)null), xColumn.GetAttribute("isPrimary", false), xColumn.GetAttribute("isIdentity", false))
		{
			this.metaInfo = new PpsDataColumnMetaCollectionClient(xColumn);
			this.dataType = LuaType.GetType(xColumn.GetAttribute("dataType", "object"), lateAllowed: false).Type;

			this.parentRelationName = xColumn.GetAttribute<string>("parentRelationName", null);
			this.parentTableName = xColumn.GetAttribute<string>("parentTable", null);
			this.parentColumn = xColumn.GetAttribute<string>("parentColumn", null);
		} // ctor
		
		public override PpsDataColumnDefinition Clone(PpsDataTableDefinition tableOwner)
			=> new PpsDataColumnDefinitionClient(tableOwner, this);

		protected override Type GetDataType()
			=> dataType;

		public override void EndInit()
		{
			if (parentRelationName != null)
			{
				var parentTable = Table.DataSet.FindTable(this.parentTableName);
				parentTable.AddRelation(parentRelationName, parentTable.Columns[parentColumn, true], this);
			}
			base.EndInit();
		} // proc EndInit

		public override PpsDataColumnMetaCollection Meta => metaInfo;
	} // class PpsDataColumnDefinitionClient

	#endregion
}

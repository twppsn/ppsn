﻿#region -- copyright --
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

namespace TecWare.PPSn.Data
{
	#region -- class PpsDataColumnMetaCollectionClient --------------------------------

	internal sealed class PpsDataColumnMetaCollectionClient : PpsDataColumnDefinition.PpsDataColumnMetaCollection
	{
		public PpsDataColumnMetaCollectionClient(PpsDataColumnDefinition column, PpsDataColumnMetaCollectionClient clone)
			: base(column, clone)
		{
		} // ctor

		public PpsDataColumnMetaCollectionClient(PpsDataColumnDefinition column, XElement xMetaGroup)
			: base(column)
		{
			PpsDataHelperClient.AddMetaGroup(xMetaGroup, Add);
		} // ctor
	} // class PpsDataColumnMetaCollectionClient

	#endregion

	#region -- class PpsDataValueColumnDefinitionClient -------------------------------

	/// <summary>Client site data column definition.</summary>
	public sealed class PpsDataColumnDefinitionClient : PpsDataColumnDefinition
	{
		private readonly PpsDataColumnMetaCollectionClient metaInfo;
		private readonly Type dataType;

		private readonly string parentRelationName;
		private readonly PpsRelationType parentRelationType;
		private readonly string parentTableName;
		private readonly string parentColumn;

		private PpsDataColumnDefinitionClient(PpsDataTableDefinition table, PpsDataColumnDefinitionClient clone)
			: base(table, clone)
		{
			this.metaInfo = new PpsDataColumnMetaCollectionClient(this, clone.metaInfo);
			this.dataType = clone.dataType;

			this.parentRelationName = clone.parentRelationName;
			this.parentRelationType = clone.parentRelationType;
			this.parentTableName = clone.parentTableName;
			this.parentColumn = clone.parentColumn;
		} // ctor

		/// <summary></summary>
		/// <param name="table"></param>
		/// <param name="xColumn"></param>
		public PpsDataColumnDefinitionClient(PpsDataTableDefinitionClient table, XElement xColumn)
			: base(table, xColumn.GetAttribute("name", (string)null), xColumn.GetAttribute("isPrimary", false), xColumn.GetAttribute("isIdentity", false))
		{
			this.metaInfo = new PpsDataColumnMetaCollectionClient(this, xColumn.Element("meta"));
			this.dataType = ((PpsDataSetDefinitionClient) table.DataSet).GetColumnType(xColumn.GetAttribute("dataType", "object"));

			this.parentRelationName = xColumn.GetAttribute<string>("parentRelationName", null);
			this.parentRelationType = xColumn.GetAttribute("parentRelationType", PpsRelationType.None);
			this.parentTableName = xColumn.GetAttribute<string>("parentTable", null);
			this.parentColumn = xColumn.GetAttribute<string>("parentColumn", null);
		} // ctor

		/// <summary></summary>
		/// <param name="tableOwner"></param>
		/// <returns></returns>
		public override PpsDataColumnDefinition Clone(PpsDataTableDefinition tableOwner)
			=> new PpsDataColumnDefinitionClient(tableOwner, this);

		/// <summary></summary>
		/// <returns></returns>
		protected override Type GetDataType()
			=> dataType;

		/// <summary></summary>
		public override void EndInit()
		{
			if (parentRelationName != null)
			{
				var parentTable = Table.DataSet.FindTable(this.parentTableName);
				parentTable.AddRelation(parentRelationName, parentRelationType, parentTable.Columns[parentColumn, true], this);
			}
			base.EndInit();
		} // proc EndInit

		/// <summary></summary>
		public override PpsDataColumnMetaCollection Meta => metaInfo;
	} // class PpsDataColumnDefinitionClient

	#endregion
}

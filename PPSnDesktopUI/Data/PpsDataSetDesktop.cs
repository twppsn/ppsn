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
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Xml.Linq;
using Neo.IronLua;

namespace TecWare.PPSn.Data
{
	#region -- class PpsDataTableDefinitionDesktop --------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public class PpsDataTableDefinitionDesktop : PpsDataTableDefinitionClient
	{
		public PpsDataTableDefinitionDesktop(PpsDataSetDefinitionClient dataset, XElement xTable)
			: base(dataset, xTable)
		{
		} // ctor

		public override PpsDataTable CreateDataTable(PpsDataSet dataset)
			=> new PpsDataTableDesktop(this, dataset);
	} // class PpsDataTableDefinitionDesktop

	#endregion

	#region -- class PpsDataSetDefinitionDesktop ----------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public class PpsDataSetDefinitionDesktop : PpsDataSetDefinitionClient
	{
		public PpsDataSetDefinitionDesktop(IPpsShell shell, string type, XElement xSchema)
			: base(shell, type, xSchema)
		{
		} // ctor

		protected override PpsDataTableDefinitionClient CreateDataTable(XElement c)
			=> new PpsDataTableDefinitionDesktop(this, c);
	} // class PpsDataSetDefinitionDesktop

	#endregion

	#region -- class PpsDataCollectionView ----------------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public class PpsDataCollectionView : ListCollectionView
	{
		private readonly IDisposable detachView;

		public PpsDataCollectionView(IPpsDataView dataTable) 
			: base(dataTable)
		{
			this.detachView = dataTable as IDisposable;
		} // ctor

		public override void DetachFromSourceCollection()
		{
			base.DetachFromSourceCollection();
			detachView?.Dispose();
		} // proc DetachFromSourceCollection

		public PpsDataRow Add(LuaTable values)
		{
			var row = DataView.NewRow(DataView.Table.GetDataRowValues(values), null);
			AddNewItem(row);
			return row;
		} // func Add

		public IPpsDataView DataView => (IPpsDataView)base.SourceCollection;
	} // class PpsDataCollectionView

	#endregion

	#region -- class PpsDataRelatedFilterDesktop ----------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public sealed class PpsDataRelatedFilterDesktop : PpsDataRelatedFilter, ICollectionViewFactory
	{
		public PpsDataRelatedFilterDesktop(PpsDataRow parentRow, PpsDataTableRelationDefinition relation) 
			: base(parentRow, relation)
		{
		} // ctor

		public ICollectionView CreateView()
			=> new PpsDataCollectionView(this);
	} // class PpsDataRelatedFilterDesktop

	#endregion

	#region -- class PpsDataTableDesktop ------------------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public class PpsDataTableDesktop : PpsDataTable, ICollectionViewFactory
	{
		public PpsDataTableDesktop(PpsDataTableDefinition tableDefinition, PpsDataSet dataset) 
			: base(tableDefinition, dataset)
		{
		} // ctor

		public ICollectionView CreateView()
			=> new PpsDataCollectionView(this);

		public override PpsDataFilter CreateRelationFilter(PpsDataRow row, PpsDataTableRelationDefinition relation)
			=> new PpsDataRelatedFilterDesktop(row, relation);
	} // class PpsDataTableDesktop

	#endregion
}

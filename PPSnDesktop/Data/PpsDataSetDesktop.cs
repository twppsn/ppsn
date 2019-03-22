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
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Windows.Data;
using System.Xml.Linq;
using Neo.IronLua;
using TecWare.DE.Data;
using TecWare.DE.Stuff;

namespace TecWare.PPSn.Data
{
	#region -- class PpsDataTableDefinitionDesktop ------------------------------------

	/// <summary></summary>
	public class PpsDataTableDefinitionDesktop : PpsDataTableDefinitionClient
	{
		/// <summary></summary>
		/// <param name="dataset"></param>
		/// <param name="xTable"></param>
		public PpsDataTableDefinitionDesktop(PpsDataSetDefinitionClient dataset, XElement xTable)
			: base(dataset, xTable)
		{
		} // ctor

		/// <summary></summary>
		/// <param name="dataset"></param>
		/// <returns></returns>
		public override PpsDataTable CreateDataTable(PpsDataSet dataset)
			=> new PpsDataTableDesktop(this, dataset);
	} // class PpsDataTableDefinitionDesktop

	#endregion

	#region -- interface IPpsObjectBasedDataSet ---------------------------------------

	/// <summary>Interface that needs to implement in the base dataset, to support the PpsObjectExtendedColumn.</summary>
	public interface IPpsObjectBasedDataSet
	{
		/// <summary>The dataset reads data.</summary>
		bool IsReading { get; }
		/// <summary>Returns the attached object to the dataset.</summary>
		IPpsObject Object { get; }
	} // interface IPpsObjectBasedDataSet

	#endregion

	#region -- class PpsObjectExtendedValue -------------------------------------------

	/// <summary>Object reference.</summary>
	public sealed class PpsObjectExtendedValue : PpsDataRowExtentedValue, IPpsDataRowGetGenericValue
	{
		private readonly IPpsObjectBasedDataSet dataset;
		private readonly PpsEnvironment environment;

		/// <summary></summary>
		/// <param name="row"></param>
		/// <param name="column"></param>
		public PpsObjectExtendedValue(PpsDataRow row, PpsDataColumnDefinition column)
			: base(row, column)
		{
			this.environment = PpsDataSetDefinitionDesktop.GetEnvironmentFromColumn(column);
			this.dataset = (row.Table.DataSet as IPpsObjectBasedDataSet) ?? throw new ArgumentException("Dataset does not implement IPpsObjectBasedDataSet.");
		} // ctor

		/// <summary></summary>
		/// <param name="x"></param>
		protected override void Read(XElement x)
		{
			// we do not load something
		} // proc Read

		/// <summary></summary>
		/// <param name="x"></param>
		protected override void Write(XElement x)
		{
			// should not get called, if isnull is true
			x.Add(new XElement("o", dataset.Object.Id));
		} // proc Write

		/// <summary>Get object id.</summary>
		public long Id => dataset.Object?.Id ?? 0;
		/// <summary>Get object instance.</summary>
		public object Value => dataset?.Object;

		/// <summary>Is the value null.</summary>
		public override bool IsNull => dataset.Object == null;
	} // class PpsObjectExtendedValue

	#endregion

	#region -- class PpsLinkedObjectExtendedValue -------------------------------------

	/// <summary>Referenz to a linked object.</summary>
	public sealed class PpsLinkedObjectExtendedValue : PpsDataRowObjectExtendedValue, IPpsDataRowExtendedEvents
	{
		private readonly PpsEnvironment environment;
		private readonly IPpsObjectBasedDataSet dataset; // optional

		private WeakReference<PpsObject> referencedObject = null;

		/// <summary></summary>
		/// <param name="row"></param>
		/// <param name="column"></param>
		public PpsLinkedObjectExtendedValue(PpsDataRow row, PpsDataColumnDefinition column)
			: base(row, column)
		{
			this.environment = PpsDataSetDefinitionDesktop.GetEnvironmentFromColumn(column);
			this.dataset = row.Table.DataSet as IPpsObjectBasedDataSet;
		} // ctor

		/// <summary>Write the value</summary>
		/// <param name="x"></param>
		protected override void Write(XElement x)
		{
			base.Write(x);

			// extra hint, for the object
			var tmp = (PpsObject)Value;
			if (tmp != null)
				x.Add(new XElement("g", tmp.Guid.ToString("D")));
		} // proc Write

		/// <summary></summary>
		/// <param name="x"></param>
		protected override void Read(XElement x)
		{
			base.Read(x);

			// check linked value, corrent id
			if (InternalValue != null && dataset != null)
			{
				var objectId = (long)InternalValue;
				if (objectId < 0)
				{
					var guidString = x.GetNode("g", (string)null); // there is no g, when the object comes from server
					if (guidString != null)
						base.SetGenericValue(((PpsObject)dataset.Object).Links.FindByGuid(new Guid(guidString))?.LinkToId, false);
					else
						base.SetGenericValue(((PpsObject)dataset.Object).Links.FindById(objectId)?.LinkToId, false);
				}
			}
		} // proc Read

		/// <summary></summary>
		/// <param name="propertyName"></param>
		/// <param name="oldValue"></param>
		/// <param name="newValue"></param>
		/// <param name="firePropertyChanged"></param>
		protected override void OnPropertyChanged(string propertyName, object oldValue, object newValue, bool firePropertyChanged)
		{
			if (dataset != null && propertyName == nameof(Value) && Row.IsCurrent)
			{
				// remove possible old link
				if (oldValue != null)
					((PpsObject)dataset.Object).Links.RemoveLink((long)oldValue, false);
				// add the new link
				((PpsObject)dataset.Object).Links.AppendLink((long)newValue);

			}
			base.OnPropertyChanged(propertyName, oldValue, newValue, firePropertyChanged);
		} // proc OnPropertyChanged

		/// <summary></summary>
		public void OnRowAdded()
		{
			if (dataset != null && !dataset.IsReading && InternalValue != null)
				((PpsObject)dataset.Object).Links.AppendLink((long)InternalValue);
		} // proc OnRowAdded

		/// <summary></summary>
		public void OnRowRemoved()
		{
			if (dataset != null && !dataset.IsReading && InternalValue != null)
				((PpsObject)dataset.Object).Links.RemoveLink((long)InternalValue);
		} // proc OnRowRemoved

		/// <summary></summary>
		/// <param name="newValue"></param>
		/// <param name="firePropertyChanged"></param>
		/// <returns></returns>
		protected override bool SetGenericValue(object newValue, bool firePropertyChanged)
		{
			// gets also called on undo/redo
			switch (newValue)
			{
				case null:
					{
						referencedObject = null;
						return base.SetGenericValue(null, firePropertyChanged);
					}
				case PpsObject o:
					{
						var oldValue = InternalValue;
						if (base.SetGenericValue(o.Id, firePropertyChanged))
						{
							referencedObject = new WeakReference<PpsObject>(o);
							return true;
						}
						else
						{
							if (referencedObject == null || !referencedObject.TryGetTarget(out var t))
								referencedObject = new WeakReference<PpsObject>(o);
							return false;
						}
					}
				case int idInt:
					return SetGenericValue(environment.GetObject(idInt, throwException: true), firePropertyChanged);
				case long idLong:
					return SetGenericValue(environment.GetObject(idLong, throwException: true), firePropertyChanged);
				default:
					throw new ArgumentException("Only long or PpsObject is allowed.", nameof(newValue));
			}
			;
		}

		/// <summary></summary>
		public override object Value
		{
			get
			{
				var v = InternalValue;
				if (v == null)
					return null;
				else if (referencedObject != null && referencedObject.TryGetTarget(out var obj))
					return obj;
				else
				{
					obj = environment.GetObject((long)v);
					referencedObject = new WeakReference<PpsObject>(obj);
					return obj;
				}
			}
		} // prop Value
	} // class PpsLinkedObjectExtendedValue

	#endregion

	#region -- class PpsMasterDataExtendedValue ---------------------------------------

	/// <summary>Extent column value, that references PpsMasterDataRow's.</summary>
	public sealed class PpsMasterDataExtendedValue : PpsDataRowObjectExtendedValue
	{
		private readonly PpsEnvironment environment;
		private readonly PpsMasterDataTable masterDataTable;

		private WeakReference<PpsMasterDataRow> referencedRow = null; // pointer to the actual row
		
		/// <summary></summary>
		/// <param name="row"></param>
		/// <param name="column"></param>
		public PpsMasterDataExtendedValue(PpsDataRow row, PpsDataColumnDefinition column)
			: base(row, column)
		{
			this.environment = PpsDataSetDefinitionDesktop.GetEnvironmentFromColumn(column);

			this.masterDataTable = environment.MasterData.GetTable(
				column.Meta.GetProperty<string>("refTable", null) 
					?? throw new ArgumentNullException("refTable", "Meta attribute refTable is not definied.")
			) ?? throw new ArgumentNullException("refTable");
		} // ctor

		/// <summary></summary>
		/// <param name="newValue"></param>
		/// <param name="firePropertyChanged"></param>
		/// <returns></returns>
		protected override bool SetGenericValue(object newValue, bool firePropertyChanged)
		{
			if (newValue == PpsDataRow.NotSet)
			{
				referencedRow = null;
				return base.SetGenericValue(newValue, firePropertyChanged);
			}
			switch (newValue)
			{
				case null:
					referencedRow = null;
					return base.SetGenericValue(null, firePropertyChanged);
				case int idInt:
					return SetGenericValue(masterDataTable.GetRowById(idInt, true), firePropertyChanged);
				case long idLong:
					return SetGenericValue(masterDataTable.GetRowById(idLong, true), firePropertyChanged);
				case PpsMasterDataRow o:
					if (base.SetGenericValue(o.RowId, firePropertyChanged)) // change change
					{
						referencedRow = new WeakReference<PpsMasterDataRow>(o);
						return true;
					}
					else // update cache
					{
						if (referencedRow == null || !referencedRow.TryGetTarget(out var t))
							referencedRow = new WeakReference<PpsMasterDataRow>(o);
						return false;
					}
				default:
					throw new ArgumentException("Only int, long or IDataRow is allowed.", nameof(newValue));
			}
		} // func SetGenericValue
		
		/// <summary>Get the row object.</summary>
		public override object Value
		{
			get
			{
				var v = InternalValue;
				if (v == null)
					return null;
				else if (referencedRow != null && referencedRow.TryGetTarget(out var row))
					return row;
				else
				{
					row = masterDataTable.GetRowById((long)v);
					referencedRow = new WeakReference<PpsMasterDataRow>(row);
					return row;
				}
			}
		} // prop Value
	} // class PpsMasterDataExtendedValue

	#endregion

	#region -- class PpsDataSetDefinitionDesktop --------------------------------------

	/// <summary>Desktop extensions.</summary>
	public class PpsDataSetDefinitionDesktop : PpsDataSetDefinitionClient
	{
		/// <summary></summary>
		/// <param name="environment"></param>
		/// <param name="schema"></param>
		/// <param name="xSchema"></param>
		public PpsDataSetDefinitionDesktop(PpsEnvironment environment, string schema, XElement xSchema)
			: base(environment, schema, xSchema)
		{
		} // ctor

		/// <summary></summary>
		/// <param name="c"></param>
		/// <returns></returns>
		protected override PpsDataTableDefinitionClient CreateDataTable(XElement c)
			=> new PpsDataTableDefinitionDesktop(this, c);

		/// <summary></summary>
		/// <returns></returns>
		public override PpsDataSet CreateDataSet()
			=> new PpsDataSetClient(this, (PpsEnvironment)Shell);

		/// <summary>Get client site column types.</summary>
		/// <param name="dataType">DataType name.</param>
		/// <returns>Client site column type.</returns>
		public override Type GetColumnType(string dataType)
		{
			if (String.Compare(dataType, "ppsObject", StringComparison.OrdinalIgnoreCase) == 0)
				return typeof(PpsObjectExtendedValue);
			else if (String.Compare(dataType, "ppsLinkObject", StringComparison.OrdinalIgnoreCase) == 0)
				return typeof(PpsLinkedObjectExtendedValue);
			else if (String.Compare(dataType, "ppsMasterData", StringComparison.OrdinalIgnoreCase) == 0)
				return typeof(PpsMasterDataExtendedValue);
			else
				return base.GetColumnType(dataType);
		} // func GetColumnType

		/// <summary>Get the base environment.</summary>
		public PpsEnvironment Environment => (PpsEnvironment)base.Shell;

		internal static PpsEnvironment GetEnvironmentFromColumn(PpsDataColumnDefinition column)
			=> ((PpsDataSetDefinitionDesktop)column.Table.DataSet).Environment ?? throw new ArgumentNullException("environment");
	} // class PpsDataSetDefinitionDesktop

	#endregion

	#region -- class PpsDataCollectionView --------------------------------------------

	/// <summary>Special collection view for PpsDataTable, that supports IDataRowEnumerable</summary>
	public class PpsDataCollectionView : ListCollectionView, IPpsDataRowViewFilter, IPpsDataRowViewSort
	{
		#region -- class DataRowEnumerator --------------------------------------------

		private sealed class DataRowEnumerator : IEnumerator<IDataRow>
		{
			private readonly IEnumerator enumerator;

			public DataRowEnumerator(IEnumerator enumerator)
			{
				this.enumerator = enumerator;
			} // ctor

			public void Dispose()
			{
				if (enumerator is IDisposable d)
					d.Dispose();
			} // proc Dispose

			public void Reset()
				=> enumerator.Reset();

			public bool MoveNext()
				=> enumerator.MoveNext();

			object IEnumerator.Current => enumerator.Current;
			public IDataRow Current => enumerator.Current as IDataRow;
		} // class DataRowEnumerator

		#endregion

		private readonly IDisposable detachView;
		private PpsDataFilterExpression filterExpression = null;

		/// <summary>Collection view for PpsDataTable's.</summary>
		/// <param name="dataTable"></param>
		public PpsDataCollectionView(IPpsDataView dataTable)
			: base(dataTable)
		{
			this.detachView = dataTable as IDisposable;
		} // ctor

		/// <summary>Detach view from CollectionView.</summary>
		public override void DetachFromSourceCollection()
		{
			base.DetachFromSourceCollection();
			detachView?.Dispose();
		} // proc DetachFromSourceCollection

		/// <summary>Implement a add method, that supports a LuaTable as argument.</summary>
		/// <param name="values">Values for the new row.</param>
		/// <returns>Added row.</returns>
		public PpsDataRow Add(LuaTable values)
		{
			var row = DataView.NewRow(DataView.Table.GetDataRowValues(values), null);
			AddNewItem(row);
			return row;
		} // func Add

		/// <summary>Implement a add method, that supports a LuaTable as argument.</summary>
		/// <param name="values">Values for the new row.</param>
		/// <returns>Added row.</returns>
		public PpsDataRow Add(IPropertyReadOnlyDictionary values)
		{
			var row = DataView.NewRow(DataView.Table.GetDataRowValues(values), null);
			AddNewItem(row);
			return row;
		} // func Add

		/// <summary>Auto Commit rows.</summary>
		/// <param name="args"></param>
		protected override void OnCollectionChanged(NotifyCollectionChangedEventArgs args)
		{
			base.OnCollectionChanged(args);
			// we do not want the new/edit
			if (IsAddingNew)
				CommitNew();
			else if (IsEditingItem)
				CommitEdit();
		} // proc OnCollectionChanged

		/// <summary>Apply a sort expression to the collection view.</summary>
		public IEnumerable<PpsDataOrderExpression> Sort
		{
			get => from s in SortDescriptions select new PpsDataOrderExpression(s.Direction != ListSortDirection.Ascending, s.PropertyName);
			set
			{
				SortDescriptions.Clear();
				if (value != null)
				{
					foreach (var s in value)
						SortDescriptions.Add(new SortDescription(s.Identifier, s.Negate ? ListSortDirection.Descending : ListSortDirection.Ascending));
				}

				RefreshOrDefer();
			}
		} // prop Sort

		private bool allowSetFilter = false;

		/// <summary>Can filter is always false.</summary>
		public override bool CanFilter => allowSetFilter;

		/// <summary>Filter expression</summary>
		public override Predicate<object> Filter
		{
			get => base.Filter;
			set
			{
				if (!allowSetFilter)
					throw new NotSupportedException(); 
				base.Filter = value;
			}
		} // prop Filter

		/// <summary>Apply a filter to the collection view.</summary>
		public PpsDataFilterExpression FilterExpression
		{
			get => filterExpression ?? PpsDataFilterExpression.True;
			set
			{
				if (filterExpression != value)
				{
					filterExpression = value;
					allowSetFilter = true;
					try
					{
						base.Filter = PpsDataFilterVisitorDataRow.CreateDataRowFilter<object>(filterExpression);
					}
					finally
					{
						allowSetFilter = false;
					}
				}
			}
		} // prop FilterExpression
		
		/// <summary>Parent row, of the current filter.</summary>
		public PpsDataRow Parent => (InternalList as PpsDataRelatedFilter)?.Parent;
		/// <summary>Get the DataView, that is filtered.</summary>
		public IPpsDataView DataView => (IPpsDataView)base.SourceCollection;
	} // class PpsDataCollectionView

	#endregion

	#region -- class PpsDataRelatedFilterDesktop --------------------------------------

	/// <summary>Implements the ICollectionViewFactory to create the PpsDataCollectionView</summary>
	public sealed class PpsDataRelatedFilterDesktop : PpsDataRelatedFilter, ICollectionViewFactory
	{
		/// <summary></summary>
		/// <param name="parentRow"></param>
		/// <param name="relation"></param>
		public PpsDataRelatedFilterDesktop(PpsDataRow parentRow, PpsDataTableRelationDefinition relation) 
			: base(parentRow, relation)
		{
		} // ctor

		/// <summary>Create the PpsDataCollectionView.</summary>
		/// <returns></returns>
		public ICollectionView CreateView()
			=> new PpsDataCollectionView(this);
	} // class PpsDataRelatedFilterDesktop

	#endregion

	#region -- class PpsDataTableDesktop ----------------------------------------------

	/// <summary>Implements the ICollectionViewFactory to create the PpsDataCollectionView</summary>
	public class PpsDataTableDesktop : PpsDataTable, ICollectionViewFactory
	{
		/// <summary></summary>
		/// <param name="tableDefinition"></param>
		/// <param name="dataset"></param>
		public PpsDataTableDesktop(PpsDataTableDefinition tableDefinition, PpsDataSet dataset) 
			: base(tableDefinition, dataset)
		{
		} // ctor

		/// <summary>Create the PpsDataCollectionView.</summary>
		/// <returns></returns>
		public ICollectionView CreateView()
			=> new PpsDataCollectionView(this);

		/// <summary>Create PpsDataRelatedFilterDesktop for the ICollectionViewFactory.</summary>
		/// <param name="row"></param>
		/// <param name="relation"></param>
		/// <returns></returns>
		public override PpsDataFilter CreateRelationFilter(PpsDataRow row, PpsDataTableRelationDefinition relation)
			=> new PpsDataRelatedFilterDesktop(row, relation);
	} // class PpsDataTableDesktop

	#endregion
}

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
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Xml.Linq;
using Neo.IronLua;
using TecWare.DE.Data;
using TecWare.DE.Stuff;

namespace TecWare.PPSn.Data
{
	#region -- interface IPpsActiveDataSetOwner ---------------------------------------

	/// <summary></summary>
	public interface IPpsActiveDataSetOwner
	{
		LuaTable Events { get; }
	} // interface IPpsActiveDataSetOwner

	/// <summary></summary>
	public class PpsActiveDataSetOwner : LuaTable, IPpsActiveDataSetOwner
	{
		LuaTable IPpsActiveDataSetOwner.Events => this;
	} // class PpsActiveDataSetOwner

	#endregion

	#region -- interface IPpsActiveDataSets -------------------------------------------

	public interface IPpsActiveDataSets : IReadOnlyCollection<PpsDataSetDesktop>
	{
		/// <summary>Register a schema source.</summary>
		/// <param name="schema">Name of the schema.</param>
		/// <param name="uri">Relative uri of the schema</param>
		/// <param name="datasetDefinitionType"></param>
		/// <returns><c>true</c>, if the registration is changed.</returns>
		bool RegisterDataSetSchema(string schema, string uri, Type datasetDefinitionType = null);
		/// <summary></summary>
		/// <param name="schema"></param>
		void UnregisterDataSetSchema(string schema);
		/// <summary>Returns the schema source.</summary>
		/// <param name="schema">Name of the schema.</param>
		/// <returns></returns>
		string GetDataSetSchemaUri(string schema);

		/// <summary>Returns a dataset definition for the schema (not registered, empty id).</summary>
		/// <param name="schema">Name of the schema</param>
		/// <returns>DataSet definition</returns>
		Task<PpsDataSetDefinitionDesktop> GetDataSetDefinitionAsync(string schema);

		/// <summary>Returns open datasets of a specific type.</summary>
		/// <param name="schema"></param>
		/// <typeparam name="T"></typeparam>
		/// <returns></returns>
		IEnumerable<T> GetKnownDataSets<T>(string schema = null)
			where T : PpsDataSetDesktop;
		
		/// <summary>Returns a list of registered definitions.</summary>
		IEnumerable<string> KnownSchemas { get; }
	} // interface IPpsActiveDataSets

	#endregion

	#region -- class PpsDataTableDefinitionDesktop ------------------------------------

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

	#region -- interface IPpsObjectBasedDataSet ---------------------------------------

	/// <summary>Interface that needs to implement in the base dataset, to support the PpsObjectExtendedColumn.</summary>
	public interface IPpsObjectBasedDataSet
	{
		/// <summary>The dataset reads data.</summary>
		bool IsReading { get; }
		/// <summary>Returns the attached object to the dataset.</summary>
		PpsObject Object { get; }
	} // interface IPpsObjectBasedDataSet

	#endregion

	#region -- class PpsObjectExtendedValue -------------------------------------------

	public sealed class PpsObjectExtendedValue : PpsDataRowExtentedValue, IPpsDataRowGetGenericValue
	{
		private readonly IPpsObjectBasedDataSet dataset;
		private readonly PpsEnvironment environment;

		public PpsObjectExtendedValue(PpsDataRow row, PpsDataColumnDefinition column)
			: base(row, column)
		{
			this.environment = PpsDataSetDefinitionDesktop.GetEnvironmentFromColumn(column);
			this.dataset = (row.Table.DataSet as IPpsObjectBasedDataSet) ?? throw new ArgumentException("Dataset does not implement IPpsObjectBasedDataSet.");
		} // ctor

		protected override void Read(XElement x)
		{
			// we do not load something
		} // proc Read

		protected override void Write(XElement x)
		{
			// should not get called, if isnull is true
			x.Add(new XElement("o", dataset.Object.Id));
		} // proc Write

		public long Id => dataset.Object?.Id ?? 0;
		public object Value => dataset?.Object;

		public override bool IsNull => dataset.Object == null;
	} // class PpsObjectExtendedValue

	#endregion

	#region -- class PpsLinkedObjectExtendedValue -------------------------------------

	public sealed class PpsLinkedObjectExtendedValue : PpsDataRowObjectExtendedValue, IPpsDataRowExtendedEvents
	{
		private readonly PpsEnvironment environment;
		private readonly IPpsObjectBasedDataSet dataset; // optional

		private WeakReference<PpsObject> referencedObject = null;

		public PpsLinkedObjectExtendedValue(PpsDataRow row, PpsDataColumnDefinition column)
			: base(row, column)
		{
			this.environment = PpsDataSetDefinitionDesktop.GetEnvironmentFromColumn(column);
			this.dataset = row.Table.DataSet as IPpsObjectBasedDataSet;
		} // ctor

		protected override void Write(XElement x)
		{
			base.Write(x);

			// extra hint, for the object
			var tmp = (PpsObject)Value;
			if (tmp != null)
				x.Add(new XElement("g", tmp.Guid.ToString("D")));
		} // proc Write

		protected override void Read(XElement x)
		{
			base.Read(x);

			// check linked value, corrent id
			if (InternalValue != null && dataset != null)
			{
				var objectId = (long)InternalValue;
				if (objectId < 0)
				{
					var guidString = x.GetNode("g", (string)null);
					if (guidString != null)
						base.SetGenericValue(dataset.Object.Links.FindByGuid(new Guid(guidString))?.LinkToId, false);
					else
						base.SetGenericValue(dataset.Object.Links.FindById(objectId)?.LinkToId, false);
				}
			}
		} // proc Read

		protected override void OnPropertyChanged(string propertyName, object oldValue, object newValue, bool firePropertyChanged)
		{
			if (dataset != null && propertyName == nameof(Value) && Row.IsCurrent)
			{
				// remove possible old link
				if (oldValue != null)
					dataset.Object.Links.RemoveLink((long)oldValue, false);
				// add the new link
				dataset.Object.Links.AppendLink((long)newValue);

			}
			base.OnPropertyChanged(propertyName, oldValue, newValue, firePropertyChanged);
		} // proc OnPropertyChanged

		public void OnRowAdded()
		{
			if (dataset != null && !dataset.IsReading && InternalValue != null)
				dataset.Object.Links.AppendLink((long)InternalValue);
		} // proc OnRowAdded

		public void OnRowRemoved()
		{
			if (dataset != null && !dataset.IsReading && InternalValue != null)
				dataset.Object.Links.RemoveLink((long)InternalValue);
		} // proc OnRowRemoved

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

	public sealed class PpsMasterDataExtendedValue : PpsDataRowObjectExtendedValue
	{
		private readonly PpsEnvironment environment;
		private readonly PpsMasterDataTable masterDataTable;

		private WeakReference<PpsMasterDataRow> referencedRow = null; // pointer to the actual row
		
		public PpsMasterDataExtendedValue(PpsDataRow row, PpsDataColumnDefinition column)
			: base(row, column)
		{
			this.environment = PpsDataSetDefinitionDesktop.GetEnvironmentFromColumn(column);

			this.masterDataTable = environment.MasterData.GetTable(
				column.Meta.GetProperty<string>("refTable", null) 
					?? throw new ArgumentNullException("refTable", "Meta attribute refTable is not definied.")
			) ?? throw new ArgumentNullException("refTable");
		} // ctor

		protected override bool SetGenericValue(object newValue, bool firePropertyChanged)
		{
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
					if (base.SetGenericValue(o.Key, firePropertyChanged)) // change change
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
					throw new ArgumentException("Only long or IDataRow is allowed.", nameof(newValue));
			}
		} // func SetGenericValue
		
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
					row = masterDataTable.GetRowById(v);
					referencedRow = new WeakReference<PpsMasterDataRow>(row);
					return row;
				}
			}
		} // prop Value
	} // class PpsMasterDataExtendedValue

	#endregion

	#region -- class PpsDataSetDefinitionDesktop ----------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public class PpsDataSetDefinitionDesktop : PpsDataSetDefinitionClient
	{
		public PpsDataSetDefinitionDesktop(PpsEnvironment environment, string schema, XElement xSchema)
			: base(environment, schema, xSchema)
		{
		} // ctor

		protected override PpsDataTableDefinitionClient CreateDataTable(XElement c)
			=> new PpsDataTableDefinitionDesktop(this, c);

		public override PpsDataSet CreateDataSet()
			=> new PpsDataSetDesktop(this, (PpsEnvironment)Shell);

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

		public PpsEnvironment Environment => (PpsEnvironment)base.Shell;

		internal static PpsEnvironment GetEnvironmentFromColumn(PpsDataColumnDefinition column)
			=> ((PpsDataSetDefinitionDesktop)column.Table.DataSet).Environment ?? throw new ArgumentNullException("environment");
	} // class PpsDataSetDefinitionDesktop

	#endregion

	#region -- class PpsDataCollectionView --------------------------------------------

	/// <summary>Special collection view for PpsDataTable</summary>
	public class PpsDataCollectionView : ListCollectionView, IDataRowEnumerable
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

		protected override void OnCollectionChanged(NotifyCollectionChangedEventArgs args)
		{
			base.OnCollectionChanged(args);
			// we do not want the new/edit
			if (IsAddingNew)
				CommitNew();
			else if (IsEditingItem)
				CommitEdit();
		} // proc OnCollectionChanged

		public IDataRowEnumerable ApplyOrder(IEnumerable<PpsDataOrderExpression> expressions, Func<string, string> lookupNative = null)
		{
			// update search expression
			SortDescriptions.Clear();
			foreach (var expr in expressions)
				SortDescriptions.Add(new SortDescription(expr.Identifier, expr.Negate ? ListSortDirection.Descending : ListSortDirection.Ascending));

			RefreshOrDefer();
			
			return this; // we do not create a new collectionview -> it should be unique for the current context
		} // func ApplyFilter

		public IDataRowEnumerable ApplyFilter(PpsDataFilterExpression expression, Func<string, string> lookupNative = null)
		{
			var currentParameter = ParameterExpression.Parameter(typeof(object), "#current");
			var rowParameter = Expression.Variable(typeof(IDataRow), "#row");
			var filterExpr = new PpsDataFilterVisitorDataRow(rowParameter, InternalList as IPpsDataView).CreateFilter(expression);

			var predicateExpr = Expression.Lambda<Predicate<object>>(
				Expression.Block(typeof(bool),
					new ParameterExpression[] { rowParameter },
					Expression.Assign(rowParameter, Expression.Convert(currentParameter, typeof(IDataRow))),
					filterExpr
				),
				currentParameter
			);
			
			this.Filter = predicateExpr.Compile();

			return this; // we do not create a new collectionview -> it should be unique for the current context
		} // func ApplyFilter

		public IDataRowEnumerable ApplyColumns(IEnumerable<PpsDataColumnExpression> columns) 
			=> throw new NotSupportedException(); // it is not allowed to touch columns

		IEnumerator<IDataRow> IEnumerable<IDataRow>.GetEnumerator()
			=> this.Cast<IDataRow>().GetEnumerator();

		public PpsDataRow Parent => (InternalList as PpsDataRelatedFilter)?.Parent;

		public IPpsDataView DataView => (IPpsDataView)base.SourceCollection;
	} // class PpsDataCollectionView

	#endregion

	#region -- class PpsDataRelatedFilterDesktop --------------------------------------

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

	#region -- class PpsDataSetDesktop --------------------------------------------------
	
	// IsLoaded-> Load, Unload should be done by this class, no external call allowed!
	public class PpsDataSetDesktop : PpsDataSetClient
	{
		private readonly object datasetOwnerLock = new object();
		private readonly List<IPpsActiveDataSetOwner> datasetOwner = new List<IPpsActiveDataSetOwner>(); // list with document owners

		#region -- Ctor/Dtor --------------------------------------------------------------

		public PpsDataSetDesktop(PpsDataSetDefinitionDesktop definition, PpsEnvironment environment)
			: base(definition, environment)
		{
		} // ctor

		public void RegisterOwner(IPpsActiveDataSetOwner owner)//<-- verschieben?
		{
			lock (datasetOwnerLock)
			{
				if (datasetOwner.IndexOf(owner) >= 0)
					throw new InvalidOperationException("Already registered.");

				if (datasetOwner.Count == 0)
					Environment.OnDataSetActivated(this);

				RegisterEventSink(owner.Events);
				datasetOwner.Add(owner);
			}
		} // proc RegisterOwner

		public void UnregisterOwner(IPpsActiveDataSetOwner owner)
		{
			lock (datasetOwnerLock)
			{
				var index = datasetOwner.IndexOf(owner);
				if (index == -1)
					throw new InvalidOperationException("Owner not registered.");

				datasetOwner.RemoveAt(index);
				UnregisterEventSink(owner.Events);

				if (datasetOwner.Count == 0)
					Environment.OnDataSetDeactivated(this);
			}
		} // proc Unregister

		#endregion

		/// <summary>Environment.</summary>
		public PpsEnvironment Environment => (PpsEnvironment)Shell;
	} // class PpsDataSetDesktop

	#endregion
}

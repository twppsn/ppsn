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
	#region -- class PpsDataSetId -------------------------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public class PpsDataSetId : IComparable<PpsDataSetId>, IEquatable<PpsDataSetId>
	{
		private readonly Guid guid;
		private readonly long index;

		public PpsDataSetId(Guid guid, long index)
		{
			this.guid = guid;
			this.index = index;
		} // ctor

		public override string ToString()
			=> $"DataSet: {guid:D}@{index:N0}";

		public override int GetHashCode()
			=> guid.GetHashCode() ^ index.GetHashCode();

		public override bool Equals(object other)
			=> Equals(other as PpsDataSetId);

		public bool Equals(PpsDataSetId other)
			=> (object)other != null && (Object.ReferenceEquals(this, other) || (other.guid == this.guid && other.index == this.index));

		public int CompareTo(PpsDataSetId other)
		{
			if ((object)other == null)
				return -1;

			var t = guid.CompareTo(other.Guid);
			return t == 0 ? index.CompareTo(other.index) : t;
		} // func CompareTo

		/// <summary>Client site Id, not the server id.</summary>
		public Guid Guid => guid;
		/// <summary>Pulled revision</summary>
		public long Index => index;

		public bool IsEmpty => guid == Guid.Empty && index <= 0;

		public static PpsDataSetId Empty { get; } = new PpsDataSetId(Guid.Empty, 0);

		public static bool operator ==(PpsDataSetId left, PpsDataSetId right)
			=> ((object)left == null && (object)right == null) || ((object)left != null && left.Equals(right));

		public static bool operator !=(PpsDataSetId left, PpsDataSetId right)
			=> ((object)left != null || (object)right != null) && ((object)left == null || !left.Equals(right));
	} // class PpsDataSetId

	#endregion

	#region -- interface IPpsActiveDataSetOwner -----------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public interface IPpsActiveDataSetOwner
	{
		LuaTable Events { get; }
	} // interface IPpsActiveDataSetOwner

	#endregion

	#region -- interface IPpsActiveDataSets ---------------------------------------------

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

		/// <summary>Parses the guid from the xml-representation</summary>
		/// <param name="xData"></param>
		/// <returns></returns>
		Guid GetGuidFromData(XElement xData, XName rootTable = null);

		/// <summary>Returns a dataset definition for the schema (not registered, empty id).</summary>
		/// <param name="schema">Name of the schema</param>
		/// <returns>DataSet definition</returns>
		Task<PpsDataSetDefinitionDesktop> GetDataSetDefinition(string schema);

		/// <summary>Creates a uninitialized dataset (registered).</summary>
		/// <param name="schema">Name of the schema.</param>
		/// <param name="id">Id for the registered dataset.</param>
		/// <returns>Registered dataset.</returns>
		Task<PpsDataSetDesktop> CreateEmptyDataSetAsync(string schema, PpsDataSetId id);

		/// <summary>Returns a list of registered definitions.</summary>
		IEnumerable<string> KnownSchemas { get; }

		/// <summary>Returns a currenty opened dataset.</summary>
		/// <param name="id">Id of the requested dataset.</param>
		/// <returns><c>null</c> or the active dataset.</returns>
		PpsDataSetDesktop this[PpsDataSetId id] { get; }
	} // interface IPpsActiveDataSets

	#endregion

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
		public PpsDataSetDefinitionDesktop(PpsEnvironment environment, string schema, XElement xSchema)
			: base(environment, schema, xSchema)
		{
		} // ctor

		protected override PpsDataTableDefinitionClient CreateDataTable(XElement c)
			=> new PpsDataTableDefinitionDesktop(this, c);

		public override PpsDataSet CreateDataSet()
			=> new PpsDataSetDesktop(this, (PpsEnvironment)Shell, PpsDataSetId.Empty);

		public virtual PpsDataSetDesktop CreateDataSet(PpsDataSetId id)
			=> new PpsDataSetDesktop(this, (PpsEnvironment) Shell, id);
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
			CommitNew();
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

	#region -- class PpsDataSetDesktop --------------------------------------------------

	public class PpsDataSetDesktop : PpsDataSetClient, INotifyPropertyChanged
	{
		public event PropertyChangedEventHandler PropertyChanged;

		private readonly PpsDataSetId dataSetId;

		private bool isDirty = false;             // is this document changed since the last dump

		private readonly object datasetOwnerLock = new object();
		private readonly List<IPpsActiveDataSetOwner> datasetOwner = new List<IPpsActiveDataSetOwner>(); // list with document owners

		#region -- Ctor/Dtor --------------------------------------------------------------

		public PpsDataSetDesktop(PpsDataSetDefinitionDesktop definition, PpsEnvironment environment, PpsDataSetId datasetId)
			: base(definition, environment)
		{
			this.dataSetId = datasetId;
		} // ctor

		public void RegisterOwner(IPpsActiveDataSetOwner owner)
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

		#region -- Dirty Flag -------------------------------------------------------------

		private void SetDirty()
		{
			if (!isDirty)
			{
				isDirty = true;
				OnPropertyChanged(nameof(IsDirty));
			}
		} // proc SetDirty

		public void ResetDirty()
		{
			if (!isDirty)
			{
				isDirty = false;
				OnPropertyChanged(nameof(IsDirty));
			}
		} // proc ResetDirty

		protected override void OnDataChanged()
		{
			base.OnDataChanged();
			SetDirty();
		} // proc OnDataChanged

		#endregion

		protected void OnPropertyChanged(string propertyName)
			=> PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

		/// <summary>Environment.</summary>
		public PpsEnvironment Environment => (PpsEnvironment)Shell;
		/// <summary>Id of the current dataset.</summary>
		public PpsDataSetId DataSetId => dataSetId;
		/// <summary>Is the current dataset changed.</summary>
		public bool IsDirty => isDirty;
	} // class PpsDataSetDesktop

	#endregion
}

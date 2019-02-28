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
using System.ComponentModel;
using System.Threading.Tasks;
using System.Xml.Linq;
using Neo.IronLua;
using TecWare.DE.Stuff;

using static TecWare.PPSn.Data.PpsDataHelperClient;

namespace TecWare.PPSn.Data
{
	#region -- class PpsDataSetDefinitionClient ---------------------------------------

	/// <summary>Client site dataset definition.</summary>
	public class PpsDataSetDefinitionClient : PpsDataSetDefinition
	{
		#region -- class PpsDataSetMetaCollectionClient -------------------------------

		private sealed class PpsDataSetMetaCollectionClient : PpsDataSetMetaCollection
		{
			public PpsDataSetMetaCollectionClient(XElement xMetaGroup)
			{
				PpsDataHelperClient.AddMetaGroup(xMetaGroup, Add);
			} // ctor
		} // class PpsDataSetMetaCollectionClient

		#endregion

		private readonly PpsShell shell;
		private readonly string schema;
		private PpsDataSetMetaCollectionClient metaInfo;

		/// <summary></summary>
		/// <param name="shell"></param>
		/// <param name="schema"></param>
		/// <param name="xSchema"></param>
		public PpsDataSetDefinitionClient(PpsShell shell, string schema, XElement xSchema)
		{
			this.shell = shell;
			this.schema = schema;

			// read definitions
			foreach (XElement c in xSchema.Elements())
			{
				if (c.Name == xnTable)
					Add(CreateDataTable(c));
				else if (c.Name == xnTag)
					Add(CreateAutoTagDefinition(c));
				else if (c.Name == xnMeta)
					metaInfo = new PpsDataSetMetaCollectionClient(c);
			}

			// create always a meta data collection
			if (metaInfo == null)
				metaInfo = new PpsDataSetMetaCollectionClient(new XElement("meta"));
		} // ctor

		/// <summary></summary>
		/// <param name="c"></param>
		/// <returns></returns>
		protected virtual PpsDataTableDefinitionClient CreateDataTable(XElement c)
			=> new PpsDataTableDefinitionClient(this, c);
		
		private PpsDataSetAutoTagDefinition CreateAutoTagDefinition(XElement x)
		{
			var tagName = x.GetAttribute("name", String.Empty);
			var tagMode = x.GetAttribute("mode", PpsDataSetAutoTagMode.First);
			var tableName = x.GetAttribute("tableName", String.Empty);
			var columnName = x.GetAttribute("columnName", String.Empty);

			return new PpsDataSetAutoTagDefinition(this, tagName, tableName, columnName, tagMode);
		} // func CreateAutoTagDefinition

		/// <summary></summary>
		/// <returns></returns>
		public override PpsDataSet CreateDataSet()
			=> new PpsDataSetClient(this, shell);

		/// <summary></summary>
		/// <param name="dataType"></param>
		/// <returns></returns>
		public virtual Type GetColumnType(string dataType)
		{
			if (String.Compare(dataType, "formular", StringComparison.OrdinalIgnoreCase) == 0)
				return typeof(PpsStaticCalculated);
			else if (String.Compare(dataType, "formatted", StringComparison.OrdinalIgnoreCase) == 0)
				return typeof(PpsFormattedStringValue);
			else
				return LuaType.GetType(dataType, lateAllowed: false).Type;
		} // func GetColumnType

		/// <summary></summary>
		public sealed override PpsTablePrimaryKeyType KeyType => PpsTablePrimaryKeyType.Local;

		/// <summary></summary>
		public string SchemaType => schema;

		/// <summary></summary>
		public PpsShell Shell => shell;
		/// <summary>Give access to the shell lua engine.</summary>
		public override Lua Lua => shell.Lua;

		/// <summary></summary>
		public override PpsDataSetMetaCollection Meta => metaInfo;
	} // class PpsDataSetDefinitionClient

	#endregion

	#region -- class PpsDataSetClient -------------------------------------------------

	/// <summary>DataSet for the client.</summary>
	public class PpsDataSetClient : PpsDataSet, INotifyPropertyChanged
	{
		#region -- class PpsDataSetTable ----------------------------------------------

		private sealed class PpsDataSetTable : LuaTable
		{
			private readonly PpsDataSetClient document;

			public PpsDataSetTable(PpsDataSetClient document)
			{
				this.document = document;
			} // ctor

			private object GetDocumentTable(string key)
				=> null;

			protected override object OnIndex(object key)
				=> base.OnIndex(key) ??
					GetDocumentTable(key as string) ??
					document.shell.GetValue(key);

			[LuaMember(nameof(Arguments))]
			public LuaTable Arguments => document.arguments;
		} // class PpsDocumentTable

		#endregion

		/// <summary></summary>
		public event PropertyChangedEventHandler PropertyChanged;

		private LuaTable arguments;
		private readonly PpsShell shell;

		private bool isDirty = false;             // is this document changed since the last dump

		#region -- Ctor/Dtor ----------------------------------------------------------

		/// <summary></summary>
		/// <param name="datasetDefinition"></param>
		/// <param name="shell"></param>
		public PpsDataSetClient(PpsDataSetDefinition datasetDefinition, PpsShell shell)
			: base(datasetDefinition)
		{
			this.shell = shell;
		} // ctor

		/// <summary></summary>
		/// <param name="key"></param>
		/// <returns></returns>
		protected override object GetEnvironmentValue(object key)
			=> shell?.GetValue(key);

		#endregion

		#region -- Dirty Flag ---------------------------------------------------------

		/// <summary>Mark dataset as dirty.</summary>
		public void SetDirty()
		{
			if (!isDirty)
			{
				isDirty = true;
				OnPropertyChanged(nameof(IsDirty));
			}
		} // proc SetDirty

		/// <summary>Reset dirty flag.</summary>
		public void ResetDirty()
		{
			if (isDirty)
			{
				isDirty = false;
				OnPropertyChanged(nameof(IsDirty));
			}
		} // proc ResetDirty

		/// <summary></summary>
		protected override void OnDataChanged()
		{
			base.OnDataChanged();
			SetDirty();
		} // proc OnDataChanged

		#endregion
		
		/// <summary>Initialize a new dataset</summary>
		/// <param name="arguments"></param>
		/// <returns></returns>
		public virtual async Task OnNewAsync(LuaTable arguments)
		{
			this.arguments = arguments;

			await InvokeEventHandlerAsync("OnNewAsync");
		} // proc OnNewAsync

		/// <summary></summary>
		/// <param name="arguments"></param>
		/// <returns></returns>
		public virtual async Task OnLoadedAsync(LuaTable arguments)
		{
			this.arguments = arguments;

			// call initalization hook
			await InvokeEventHandlerAsync("OnLoadedAsync");
		} // proc OnLoadedAsync

		/// <summary></summary>
		/// <param name="propertyName"></param>
		protected void OnPropertyChanged(string propertyName)
			=> PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

		/// <summary>Is the dataset initialized.</summary>
		public bool IsInitialized => arguments != null;

		/// <summary>Environment of the dataset.</summary>
		public PpsShell Shell => shell;
		/// <summary>Is the current dataset changed.</summary>
		public bool IsDirty => isDirty;
	} // class PpsDataSetClient

	#endregion
}

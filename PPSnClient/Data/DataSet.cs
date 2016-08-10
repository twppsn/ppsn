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
	#region -- class PpsDataSetDefinitionClient -----------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public class PpsDataSetDefinitionClient : PpsDataSetDefinition
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

		private readonly IPpsShell shell;
		private readonly string type;
		private PpsDataSetMetaCollectionClient metaInfo;

		public PpsDataSetDefinitionClient(IPpsShell shell, string type, XElement xSchema)
		{
			this.shell = shell;
			this.type = type;

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

		public override PpsDataSet CreateDataSet()
			=> new PpsDataSetClient(this, shell);

		public string ObjectType => type;

		public IPpsShell Shell => shell;

		public override PpsDataSetMetaCollection Meta { get { return metaInfo; } }
	} // class PpsDataSetDefinitionClient

	#endregion

	#region -- class PpsDataSetClient ---------------------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public class PpsDataSetClient : PpsDataSet
	{
		#region -- class PpsDataSetTable --------------------------------------------------

		///////////////////////////////////////////////////////////////////////////////
		/// <summary></summary>
		private sealed class PpsDataSetTable : LuaTable
		{
			private readonly PpsDataSetClient document;

			public PpsDataSetTable(PpsDataSetClient document)
			{
				this.document = document;
			} // ctor

			private object GetDocumentTable(string key)
			{
				return null;
			} // func GetDocumentTable

			protected override object OnIndex(object key)
			{
				return base.OnIndex(key) ??
					GetDocumentTable(key as string) ??
					document.shell.LuaLibrary.GetValue(key);
			} // func OnIndex

			[LuaMember(nameof(Arguments))]
			public LuaTable Arguments => document.arguments;
		} // class PpsDocumentTable

		#endregion

		private readonly IPpsShell shell;
		private readonly LuaTable clientScript;
		private readonly List<LuaTable> eventSinks;

		private LuaTable arguments;

		protected internal PpsDataSetClient(PpsDataSetDefinition datasetDefinition, IPpsShell shell)
			: base(datasetDefinition)
		{
			this.shell = shell;
			this.clientScript = new LuaTable();
			this.eventSinks = new List<LuaTable>();
		} // ctor

		public void RegisterEventSink(LuaTable eventSink)
		{
			eventSinks.Add(eventSink);
		} // proc RegisterEventSink

		public void UnregisterEventSink(LuaTable eventSink)
		{
			eventSinks.Remove(eventSink);
		} // proc UnregisterEventSink

		private LuaTable[] GetEventSinks()
			=> eventSinks.ToArray();
		
		private LuaResult InvokeLuaFunction(LuaTable t, string methodName, params object[] args)
		{
			var handler = t.GetMemberValue(methodName, lRawGet: true);
			if (Lua.RtInvokeable(handler))
				return new LuaResult(Lua.RtInvoke(handler, args));
			return LuaResult.Empty;
		} // func InvokeClientFunction
		
		private Task AsyncLua(LuaResult r)
			=> r[0] as Task ?? Task.FromResult<int>(0);

		private void InvokeEventHandler(string methodName, params object[] args)
		{
			// call the local function
			InvokeLuaFunction(clientScript, methodName, args);

			// call connected events
			foreach (var s in eventSinks)
				InvokeLuaFunction(s, methodName, args);
		} // proc InvokeEventHandler

		private async Task InvokeEventHandlerAsync(string methodName, params object[] args)
		{
			// call the local function
			await AsyncLua(InvokeLuaFunction(clientScript, methodName, args));

			// call connected events
			foreach (var s in GetEventSinks())
				await AsyncLua(InvokeLuaFunction(s, methodName, args));
		} // proc InvokeEventHandler

		/// <summary>Initialize a new dataset</summary>
		public virtual async Task OnNewAsync(LuaTable arguments)
		{
			this.arguments = arguments;

			// call initalization hook
			using (var trans = UndoSink?.BeginTransaction("Init"))
			{
				// create head
				var head = Tables["Head", true];
				var row = head.Add();
				row["Typ"] = ((PpsDataSetDefinitionClient)DataSetDefinition).ObjectType;
				row["Guid"] = Guid.NewGuid();

				await InvokeEventHandlerAsync("OnNewAsync");
				trans?.Commit();
			}
		} // proc OnNewAsync

		public virtual async Task OnLoadedAsync(LuaTable arguments)
		{
			this.arguments = arguments;

			// call initalization hook
			using (var trans = UndoSink?.BeginTransaction("Init"))
			{
				await InvokeEventHandlerAsync("OnLoadedAsync");
				trans?.Commit();
			}
		} // proc OnLoadedAsync

		public bool IsInitialized => arguments != null;
	} // class PpsDataSetClient

	#endregion
}

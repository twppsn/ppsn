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
using System.Dynamic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using Neo.IronLua;
using TecWare.DE.Networking;
using TecWare.DE.Server;
using TecWare.DE.Server.Http;
using TecWare.DE.Stuff;
using TecWare.DES.Data;
using TecWare.PPSn.Server.Data;
using TecWare.PPSn.Server.Sql;
using static TecWare.PPSn.Server.PpsStuff;

namespace TecWare.PPSn.Server
{
	#region -- class PpsFieldInfo -------------------------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public sealed class PpsFieldDescription : IPpsColumnDescription
	{
		private const string DisplayNameAttributeString = "displayName";
		private const string MaxLengthAttributeString = "maxLength";
		private const string DataTypeAttributeString = "dataType";

		private readonly string name;
		private readonly XElement xDefinition;
		private IPpsColumnDescription columnDescription = null;

		private readonly Lazy<string> displayName;
		private readonly Lazy<int> maxLength;
		private readonly Lazy<Type> dataType;

		public PpsFieldDescription(string name, XElement xDefinition)
		{
			this.name = name;
			this.xDefinition = xDefinition;

			displayName = new Lazy<string>(() => xDefinition.GetAttribute(DisplayNameAttributeString, null) ?? GetFieldAttribute(DisplayNameAttributeString, name));
			maxLength = new Lazy<int>(() => GetFieldAttribute(MaxLengthAttributeString, Int32.MaxValue));
			dataType = new Lazy<Type>(() => GetDataTypeFromAttribute() ?? GetFieldAttribute(DataTypeAttributeString, typeof(string)));
		} // ctor

		internal void SetColumnDescription(IPpsColumnDescription columnDescription)
		{
			this.columnDescription = columnDescription;
		} // ResolveColumnDescription

		private Type GetDataTypeFromAttribute()
		{
			var typeString = xDefinition.GetAttribute(DataTypeAttributeString, null);
			if (String.IsNullOrEmpty(typeString))
				return null;

			try
			{
				return LuaType.GetType(typeString, lateAllowed: false).Type;
			}
			catch
			{
				return typeof(string);
			}
		} // func GetDataTypeFromAttribute

		public T GetFieldAttribute<T>(string attributeName, T @default)
		{
			var f = xDefinition.Elements(xnFieldAttribute).FirstOrDefault(x => String.Compare(x.GetAttribute("name", String.Empty), attributeName, StringComparison.OrdinalIgnoreCase) == 0);
			if (f?.Value== null)
				return @default;

			try
			{
				if (typeof(T) == typeof(Type))
					return (T)(object)LuaType.GetType(f.Value, lateAllowed: false).Type;
				else
					return f.Value.ChangeType<T>();
			}
			catch
			{
				return @default;
			}
		} // func GetFieldAttribute
		
		public string Name => name;

		public string DisplayName => displayName.Value;

		public int MaxLength => columnDescription?.MaxLength ?? maxLength.Value;
		public Type DataType => columnDescription?.DataType ?? dataType.Value;
	} // class PpsColumnInfo

	#endregion

	#region -- class PpsViewDefinition --------------------------------------------------

	public sealed class PpsViewDescription
	{
		private readonly IPpsSelectorToken selectorToken;
		private readonly string displayName;
		
		// filter, sort

		public PpsViewDescription(IPpsSelectorToken selectorToken, string displayName)
		{
			this.selectorToken = selectorToken;
			this.displayName = displayName;
		} // ctor
				
		public string Name => selectorToken.Name;
		public string DisplayName => displayName;
		//public string SecurityToken => DEConfigItem.SecuritySys;

		public IPpsSelectorToken SelectorToken => selectorToken;
	} // class PpsViewDefinition

	#endregion

	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public partial class PpsApplication
	{
		#region -- class PpsViewDefinitionInit ----------------------------------------------

		///////////////////////////////////////////////////////////////////////////////
		/// <summary></summary>
		private sealed class PpsViewDescriptionInit
		{
			private readonly PpsDataSource source;
			private readonly string name;
			private readonly XElement xDefinition;
			
			public PpsViewDescriptionInit(PpsDataSource source, string name, XElement xDefinition)
			{
				this.source = source;
				this.name = name;
				this.xDefinition = xDefinition;
			} // ctor

			public async Task<PpsViewDescription> InitializeAsync()
			{
				var sourceDescription = xDefinition.Element(xnSource);
				if (sourceDescription == null)
					throw new DEConfigurationException(xDefinition, "source definition is missing.");

				var selectorToken = await source.CreateSelectorToken(name, sourceDescription);
				var view = new PpsViewDescription(selectorToken, xDefinition.GetAttribute("displayName", name));

				return view;
			} // proc InitializeAsync
		} // class PpsViewDefinitionInit

		#endregion

		private PpsSqlExDataSource mainDataSource;

		private Dictionary<string, PpsFieldDescription> fieldDescription = new Dictionary<string, PpsFieldDescription>(StringComparer.OrdinalIgnoreCase);
		private Dictionary<string, PpsViewDescription> viewController = new Dictionary<string, PpsViewDescription>(StringComparer.OrdinalIgnoreCase);

		#region -- Init/Done --------------------------------------------------------------

		private void InitData()
		{
			//mainDataSource = new PpsSqlExDataSource(this, "Data Source=Gurke,6444;Initial Catalog=PPSn01;Integrated Security=True");
		} // proc InitData

		private void BeginReadConfigurationData(IDEConfigLoading config)
		{
			// find mainDataSource
			var mainDataSourceName = config.ConfigNew.GetAttribute("mainDataSource", String.Empty);
			if (String.IsNullOrEmpty(mainDataSourceName))
				throw new DEConfigurationException(config.ConfigNew, "@mainDataSource is empty.");

			var newMainDataSource = this.UnsafeFind(mainDataSourceName);
			if (newMainDataSource == null)
				throw new DEConfigurationException(config.ConfigNew, String.Format("@mainDataSource '{0}' not found.", mainDataSourceName));
			if(!(newMainDataSource is PpsSqlExDataSource))
				throw new DEConfigurationException(config.ConfigNew, String.Format("@mainDataSource '{0}' is a unsupported data source.", mainDataSourceName));

			config.EndReadAction(() => mainDataSource = (PpsSqlExDataSource)newMainDataSource);
		} // proc BeginReadConfigurationData

		private void BeginEndConfigurationData(IDEConfigLoading config)
		{
			// register views, columns, ...
			// we add or overide elements, but there is no deletion -> reboot
			foreach (var xRegister in config.ConfigNew.Elements(xnRegister))
			{
				// evaluate the source, that is connected to the objects
				var sourceName = xRegister.GetAttribute("source", String.Empty);
				var source = String.IsNullOrEmpty(sourceName) ? null : GetDataSource(sourceName, true);

				foreach (var xNode in xRegister.Elements())
				{
					if (xNode.Name == xnField)
						RegisterField(source, xNode);
					else if (xNode.Name == xnView)
						RegisterView(source, xNode);
					else if (xNode.Name == "selector")
						throw new NotImplementedException();
					else
						throw new NotImplementedException();
				}
			}
		} // proc BeginEndConfigurationData

		private void DoneData()
		{
		} // proc DoneData

		#endregion

		private static string GetRegisterName(XElement x)
		{
			var name = x.GetAttribute("name", String.Empty);
			if (String.IsNullOrEmpty(name))
				throw new DEConfigurationException(x, "@name is empty.");
			return name;
		} // func GetRegisterName

		private void RegisterField(PpsDataSource source, XElement x)
		{
			var name = GetRegisterName(x);
			var fieldInfo = new PpsFieldDescription(name, x); // create generic field definition
			fieldDescription[name] = fieldInfo;
			if (source != null) // create a provider specific field
			{

				RegisterInitializationTask(10001, "Resolve columns", () =>
				{
					fieldInfo.SetColumnDescription(source.GetColumnDescription(name));
					return Task.CompletedTask;
				});
			}
		} // proc RegisterField

		public void RegisterView(IPpsSelectorToken selectorToken, string displayName = null)
		{
			RegisterView(new PpsViewDescription(selectorToken, displayName ?? selectorToken.Name));
		} // func RegisterView

		private void RegisterView(PpsDataSource source, XElement x)
		{
			var name = GetRegisterName(x);
			var cur = new PpsViewDescriptionInit(source, name, x);
			RegisterInitializationTask(10002, "Build views", async () => RegisterView(await cur.InitializeAsync()));
		} // func RegisterView

		private void RegisterView(PpsViewDescription view)
		{
			lock (viewController)
				viewController[view.Name] = view;
		} // func RegisterView

		public PpsDataSource GetDataSource(string name, bool throwException)
		{
			using (this.EnterReadLock())
				return (PpsDataSource)this.UnsafeChildren.FirstOrDefault(c => c is PpsDataSource && String.Compare(c.Name, name, StringComparison.OrdinalIgnoreCase) == 0);
		} // func GetDataSource

		public PpsFieldDescription GetFieldDescription(string name, bool throwException = true)
		{
			PpsFieldDescription fieldInfo;
			if (fieldDescription.TryGetValue(name, out fieldInfo))
				return fieldInfo;
			else if (throwException)
				throw new ArgumentOutOfRangeException(); // todo:
			else
				return null;
		} // func GetFieldDescription

		public PpsViewDescription GetViewDefinition(string name, bool throwException = true)
		{
			PpsViewDescription viewInfo;
			lock (viewController)
			{
				if (viewController.TryGetValue(name, out viewInfo))
					return viewInfo;
				else if (throwException)
					throw new ArgumentOutOfRangeException(); // todo:
				else
					return null;
			}
		} // func GetViewDefinition

		private void WriteDataRow(XmlWriter xml, IDataValues row, string[] columnNames)
		{
			xml.WriteStartElement("r");
			for (var i = 0; i < columnNames.Length; i++)
			{
				if (columnNames[i] != null)
				{
					var v = row[i];
					if (v != null)
						xml.WriteElementString(columnNames[i], v.ChangeType<string>());
				}
			}
			xml.WriteEndElement();
		} // proc WriteDataRow

		[DEConfigHttpAction("viewget", IsSafeCall = false)]
		private void HttpViewGetAction(IDEContext r)
		{
			// v=views,...&filter=list&sort=list&customFilter=?&customSort=&start&count
			// views => view,view2(c1+c2),view3(c3+c4)
			
			var startAt = 0;
			var count = Int32.MaxValue;

			var ctx = r.GetUser<IPpsPrivateDataContext>();

			var selector = ctx.CreateSelector(
				r.GetProperty<string>("v", null),
				r.GetProperty<string>("f", null),
				r.GetProperty<string>("o", null),
				true
			);

			// emit the selector
			using (var tw = r.GetOutputTextWriter(MimeTypes.Text.Xml))
			using (var xml = XmlWriter.Create(tw, GetSettings(tw)))
			{
				xml.WriteStartDocument();
				xml.WriteStartElement("view");

				// execute the complete statemet
				using (var enumerator = selector.GetEnumerator(startAt, count))
				{
					bool emitCurrentRow = false;
					
					// extract the columns, optional before the fetch operation
					var columnDefinition = enumerator as IDataColumns;
					if (columnDefinition == null)
					{
						if (enumerator.MoveNext())
						{
							emitCurrentRow = true;
							columnDefinition = enumerator.Current;
						}
						else
							count = 0; // no rows
					}

					// emit column description
					string[] columnNames = null;
					if (columnDefinition != null)
					{
						columnNames = new string[columnDefinition.ColumnCount];

						xml.WriteStartElement("fields");
						for (var i = 0; i < columnNames.Length; i++)
						{
							var nativeColumnName = columnDefinition.ColumnNames[i];
							var fieldDefinition = selector.GetFieldDescription(nativeColumnName);

							if (fieldDefinition == null)
							{
								columnNames[i] = null;
								continue;
							}
							else
							{
								columnNames[i] = nativeColumnName;

								new XElement(nativeColumnName,
									new XAttribute("type", LuaType.GetType(fieldDefinition.DataType).AliasOrFullName),
									new XAttribute("field", fieldDefinition.Name)
								).WriteTo(xml);
							}
						}
						xml.WriteEndElement();
					}

					// emit first row
					xml.WriteStartElement("rows");
					if (emitCurrentRow)
					{
						WriteDataRow(xml, enumerator.Current, columnNames);
						count--;
					}

					// emit all rows
					while (count > 0)
					{
						if (!enumerator.MoveNext())
							break;

						WriteDataRow(xml, enumerator.Current, columnNames);

						count--;
					}

					xml.WriteEndElement();
				}

				xml.WriteEndElement();
				xml.WriteEndDocument();
			}
		} // func HttpViewGetAction

		public PpsDataSource MainDataSource => mainDataSource;
	} // class PpsApplication
}

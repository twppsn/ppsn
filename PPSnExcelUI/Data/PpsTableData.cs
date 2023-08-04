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
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using TecWare.DE.Data;
using TecWare.DE.Stuff;
using TecWare.PPSn.Core.Data;

namespace TecWare.PPSn.Data
{
	#region -- enum PpsTableColumnType ------------------------------------------------

	/// <summary>Column type</summary>
	[Flags]
	public enum PpsTableColumnType
	{
		/// <summary>Unknown column: Expression is the name.</summary>
		User = 0 | ReadOnly,
		/// <summary>Data column: expression the full qualified column name.</summary>
		Data = 1,
		/// <summary>Formula column: Expression is the name.</summary>
		Formula = 2 | ReadOnly,
		ReadOnly = 0x100
	} // enum PpsTableColumnType

	#endregion

	#region -- interface IPpsTableColumn ----------------------------------------------

	public interface IPpsTableColumn
	{
		/// <summary>Display name of the column.</summary>
		string Name { get; }
		/// <summary>Type of the column.</summary>
		PpsTableColumnType Type { get; }
		/// <summary>Expression to descripe the column.</summary>
		string Expression { get; }
		/// <summary>Sort order</summary>
		bool? Ascending { get; }
	} // interface IPpsTableColumn

	#endregion

	#region -- interface IPpsTableData ------------------------------------------------

	/// <summary>Table data representation</summary>
	public interface IPpsTableData
	{
		/// <summary>Update the table data.</summary>
		/// <param name="source"></param>
		/// <param name="columns"></param>
		Task UpdateAsync(PpsListSource source, IEnumerable<IPpsTableColumn> columns, bool anonymize);

		/// <summary>Change the displayname of the table.</summary>
		string DisplayName { get; set; }

		/// <summary>Returns the current table source.</summary>
		PpsListSource Source { get; }
		/// <summary></summary>
		IEnumerable<IPpsTableColumn> Columns { get; }
		/// <summary>Defined Cells name </summary>
		IEnumerable<string> DefinedNames { get; }

		/// <summary>Is this an empty view.</summary>
		bool IsEmpty { get; }
	} // interface IPpsTableData

	#endregion

	#region -- class PpsListSource ----------------------------------------------------

	/// <summary>Source of data</summary>
	public abstract class PpsListSource
	{
		public const string EnvironmentNameTag = "env";
		public const string EnvironmentUriTag = "uri";

		private readonly IPpsShell shell;       // attached environment

		#region -- Ctor/Dtor ----------------------------------------------------------

		protected PpsListSource(IPpsShell shell)
		{
			this.shell = shell ?? throw new ArgumentNullException(nameof(shell));
		} // ctor

		#endregion

		#region -- Properties ---------------------------------------------------------

		protected abstract void GetPropertiesCore(Action<string, string> appendProperty);

		public IEnumerable<KeyValuePair<string, string>> GetProperties()
		{
			var propertySink = new List<KeyValuePair<string, string>>(16);
			var appendProperty = new Action<string, string>((k, v) => propertySink.Add(new KeyValuePair<string, string>(k, v)));

			appendProperty(EnvironmentNameTag, shell.Info.Name);
			appendProperty(EnvironmentUriTag, shell.Info.Uri.ToString());
			GetPropertiesCore(appendProperty);

			return propertySink;
		} // func GetProperties

		protected abstract bool IsChangedCore(IPropertyReadOnlyDictionary otherProperties);

		public bool IsChanged(IPropertyReadOnlyDictionary otherProperties)
		{
			if (!otherProperties.TryGetProperty<Uri>(EnvironmentUriTag, out var shellUri)
				|| Uri.Compare(shellUri, shell.Info.Uri, UriComponents.Scheme | UriComponents.Host | UriComponents.Port | UriComponents.Path, UriFormat.Unescaped, StringComparison.OrdinalIgnoreCase) != 0)
				return true;

			if (IsChangedCore(otherProperties))
				return true;

			return false;
		} // func IsChanged

		#endregion

		public abstract IEnumerable<IDataRow> GetData(IEnumerable<PpsDataColumnExpression> columns, PpsDataOrderExpression[] order, IPropertyReadOnlyDictionary variables);

		#region -- TryParse -----------------------------------------------------------

		public static bool TryParse(Func<string, Uri, IPpsShell> findShell, IPropertyReadOnlyDictionary properties, out PpsListSource listSource)
		{
			listSource = null;

			// find environment
			if (!properties.TryGetProperty<string>(EnvironmentNameTag, out var shellName))
				return false;
			if (!properties.TryGetProperty<Uri>(EnvironmentUriTag, out var shellUri))
				return false;

			var env = findShell(shellName, shellUri);
			if (env == null)
				return false;

			// find source
			listSource = PpsListViewSource.TryParseView(env, properties) ?? PpsListRequestSource.TryParseRequest(env, properties);
			return listSource != null;
		} // func TryParse

		#endregion

		public IPpsShell Shell => shell;
	} // class PpsListSource

	#endregion

	#region -- class PpsListViewSource ------------------------------------------------

	public sealed class PpsListViewSource : PpsListSource
	{
		private const string viewTag = "view";
		private const string filterTag = "filter";

		private readonly string views;  // view or views
		private readonly string filter; // uses placeholder for cells

		public PpsListViewSource(IPpsShell shell, string views = null, string filterExpr = null)
			: base(shell)
		{
			this.views = views;
			this.filter = filterExpr ?? String.Empty;
		} // ctor

		protected override void GetPropertiesCore(Action<string, string> appendProperty)
		{
			appendProperty(viewTag, views);
			if (!String.IsNullOrEmpty(filter))
				appendProperty(filterTag, filter);
		} // func GetPropertiesCore

		private bool IsFilterChanged(string otherFilterExpr, string filterExpr)
		{
			if (String.IsNullOrWhiteSpace(otherFilterExpr))
				otherFilterExpr = String.Empty;
			if (String.IsNullOrWhiteSpace(filterExpr))
				filterExpr = String.Empty;

			return otherFilterExpr != filterExpr;
		} // func IsFilterChanged

		protected override bool IsChangedCore(IPropertyReadOnlyDictionary otherProperties)
		{
			if (!otherProperties.TryGetProperty<string>(viewTag, out var otherViewId)
				|| otherViewId != views)
				return true;

			if (!otherProperties.TryGetProperty<string>(filterTag, out var otherFilterExpr)
				|| IsFilterChanged(otherFilterExpr, filter))
				return true;

			return false;
		} // func IsChangedCore

		public override IEnumerable<IDataRow> GetData(IEnumerable<PpsDataColumnExpression> columnExpressions, PpsDataOrderExpression[] order, IPropertyReadOnlyDictionary variables)
		{
			var request = new PpsDataQuery(views)
			{
				Columns = columnExpressions.ToArray(),
				Filter = PpsDataFilterExpression.Parse(filter, CultureInfo.CurrentUICulture, PpsDataFilterParseOption.AllowFields | PpsDataFilterParseOption.AllowVariables).Reduce(variables),
				Order = order,
				AttributeSelector = "*,V.*,Xl.*"
			};
			return Shell.GetViewData(request);
		} // func GetEnumerator

		/// <summary>View or views to select.</summary>
		public string Views => views;
		/// <summary>Filter expression, that still uses variables.</summary>
		public string Filter => filter;

		internal static PpsListSource TryParseView(IPpsShell shell, IPropertyReadOnlyDictionary properties)
			=> properties.TryGetProperty<string>(viewTag, out var views)
				? new PpsListViewSource(shell, views, properties.GetProperty(filterTag, String.Empty))
				: null;
	} // class PpsListViewSource

	#endregion

	#region -- class PpsRequestSource -------------------------------------------------

	public sealed class PpsListRequestSource : PpsListSource
	{
		private const string requestTag = "request";

		private readonly string request;
		private readonly string arguments;

		public PpsListRequestSource(IPpsShell shell, string request, string arguments)
			: base(shell)
		{
			this.request = request ?? throw new ArgumentNullException(nameof(request));
			this.arguments = arguments ?? throw new ArgumentNullException(nameof(arguments));
		} // ctor

		protected override void GetPropertiesCore(Action<string, string> appendProperty)
		{
			appendProperty(requestTag, request);
			//sink.Add(new KeyValuePair<string, string>(arguments, arguments));
		} // func GetPropertiesCore

		protected override bool IsChangedCore(IPropertyReadOnlyDictionary otherProperties)
			=> throw new NotImplementedException();

		public override IEnumerable<IDataRow> GetData(IEnumerable<PpsDataColumnExpression> columns, PpsDataOrderExpression[] order, IPropertyReadOnlyDictionary variables)
		{
			throw new NotImplementedException();
		} // func GetData

		internal static PpsListSource TryParseRequest(IPpsShell shell, IPropertyReadOnlyDictionary properties)
			=> properties.TryGetProperty<string>(requestTag, out var request)
				? new PpsListRequestSource(shell, request, null)
				: null;
	} // class PpsListRequestSource

	#endregion
}

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
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
using Neo.IronLua;
using TecWare.DE.Data;
using TecWare.DE.Stuff;
using TecWare.PPSn.Core.Data;
using TecWare.PPSn.Data;

namespace TecWare.PPSn.Data
{
	#region -- class PpsViewJoinDefinition --------------------------------------------

	/// <summary></summary>
	public sealed class PpsViewJoinDefinition
	{
		private readonly PpsViewDefinition view;
		private readonly string alias;
		private readonly PpsDataJoinType type;

		public PpsViewJoinDefinition(PpsViewDefinition view, string alias, PpsDataJoinType type)
		{
			this.view = view ?? throw new ArgumentNullException(nameof(view));
			this.alias = alias ?? String.Empty;
			this.type = type;

			Key = new PpsDataJoinStatement[0];
		} // ctor

		public bool IsCompatibleAlias(string tableAlias)
		{
			if (String.IsNullOrEmpty(tableAlias))
				return !HasAlias;

			var pos = tableAlias.IndexOf('_');
			if (pos >= 0)
				tableAlias = tableAlias.Substring(0, pos);

			if (alias.Length == 0) // no alias
				return true;

			return String.Compare(alias, tableAlias, StringComparison.OrdinalIgnoreCase) == 0;
		} // func IsCompatibleAlias

		/// <summary>View to join to</summary>
		public PpsViewDefinition View => view;
		/// <summary>Alias for this view, if a view is joined more than ones.</summary>
		public string Alias => alias;
		/// <summary>Join type</summary>
		public PpsDataJoinType Type => type;
		/// <summary>Is alias is set.</summary>
		public bool HasAlias => alias.Length > 0;
		/// <summary>Display name for the user.</summary>
		public string DisplayName => HasAlias ? view.DisplayName + " (" + alias + ")" : view.DisplayName;
		
		/// <summary>Statement key, for internal use.</summary>
		internal PpsDataJoinStatement[] Key { get; }
	} // class PpsViewJoinDefinition

	#endregion

	#region -- class TableInfo --------------------------------------------------------

	public sealed class PpsViewDefinition : IDataColumns
	{
		private readonly PpsViewDictionary owner;
		private string displayName = null;

		private readonly List<SimpleDataColumn> columns = new List<SimpleDataColumn>();
		private readonly List<PpsViewJoinDefinition> joins = new List<PpsViewJoinDefinition>();

		internal PpsViewDefinition(PpsViewDictionary owner, string viewId)
		{
			this.owner = owner ?? throw new ArgumentNullException(nameof(owner));
			ViewId = viewId ?? throw new ArgumentNullException(nameof(viewId));
		} // ctor

		public override string ToString() 
			=> ViewId;

		internal void RefreshData(XElement x)
		{
			columns.Clear();
			joins.Clear();

			foreach (var cur in x.Elements())
			{
				switch (cur.Name.LocalName)
				{
					case "attribute":
						// todo: properties, e.g. write back logic
						break;
					case "filter":
						// todo: well known filter
						break;
					case "order":
						// todo: well known filter
						break;
					case "join": // references
						if (owner.TryGetValue(cur.GetAttribute("view", null), out var viewInfo))
							joins.Add(new PpsViewJoinDefinition(viewInfo, cur.GetAttribute("alias", null), cur.GetAttribute("type", PpsDataJoinType.Inner)));
						break;
					case "field": // column
						if (TryParseColumn(cur, out var col))
							columns.Add(col);
						break;
				}
			}
		} // proc UpdateData

		private bool TryParseColumn(XElement cur, out SimpleDataColumn col)
		{
			var name = cur.GetAttribute("name", null);
			if (String.IsNullOrEmpty(name))
				goto Error;

			var type = LuaType.GetType(cur.GetAttribute("type", null), lateAllowed: false);

			var attributes = new PropertyDictionary();

			foreach (var xAttr in cur.Elements("attribute"))
			{
				if (TryParseAttribute(xAttr, out var attr))
					attributes.SetProperty(attr);
			}

			col = new SimpleDataColumn(name, type, attributes);
			return true;

			Error:
			col = null;
			return false;
		} // func TryParseColumn

		private bool TryParseAttribute(XElement xAttr, out PropertyValue attr)
		{
			var name = xAttr.GetAttribute("name", null);
			if (String.IsNullOrEmpty(name))
				goto Error;

			var type = LuaType.GetType(xAttr.GetAttribute("dataType", null), lateAllowed: false);
			attr = new PropertyValue(name, type, xAttr.Value);
			return true;

			Error:
			attr = null;
			return false;
		} // func TryParseAttribute

		public IEnumerable<PpsViewJoinDefinition> GetAllJoins()
		{
			foreach (var j in joins)
				yield return j;

			foreach (var view in owner.Values)
			{
				if (view != this)
				{
					foreach (var j in view.Joins)
					{
						if (j.View == this)
						{
							var rtype = j.Type == PpsDataJoinType.Right
								? PpsDataJoinType.Left
								: (j.Type == PpsDataJoinType.Left ? PpsDataJoinType.Right : PpsDataJoinType.Inner);
							yield return new PpsViewJoinDefinition(view, j.Alias, rtype);
						}
					}
				}
			}
		} // func GetJoins

		public string ViewId { get; }

		public string DisplayName { get => displayName ?? ViewId; internal set { displayName = value; } }
		public string Description { get; internal set; }

		public IReadOnlyList<PpsViewJoinDefinition> Joins => joins;

		public IReadOnlyList<IDataColumn> Columns => columns;
	} // class PpsViewDefinition

	#endregion

	#region -- class PpsViewDictionary ------------------------------------------------

	/// <summary></summary>
	public sealed class PpsViewDictionary : IReadOnlyDictionary<string, PpsViewDefinition>
	{
		private readonly PpsEnvironment environment;
		private readonly Dictionary<string, PpsViewDefinition> views = new Dictionary<string, PpsViewDefinition>(StringComparer.OrdinalIgnoreCase);

		#region -- Ctor/Dtor ----------------------------------------------------------

		public PpsViewDictionary(PpsEnvironment environment)
		{
			this.environment = environment ?? throw new ArgumentNullException(nameof(environment));
		} // ctor

		#endregion

		#region -- RefreshList --------------------------------------------------------

		public async Task RefreshAsync()
		{
			// fetch all tables from the server
			var list = new PpsDataQuery("bi.reports")
			{
				Columns = new PpsDataColumnExpression[]
				{
					new PpsDataColumnExpression("ReportId"),
					new PpsDataColumnExpression("DisplayName"),
					new PpsDataColumnExpression("Description")
				},
				Filter = PpsDataFilterExpression.Compare("Type", PpsDataFilterCompareOperator.Equal, "table")
			};

			using (var r = environment.GetViewData(list).GetEnumerator())
			{
				var columns = await Task.Run(() => new SimpleDataColumns(((IDataColumns)r).Columns.ToArray()));
				
				var reportIndex = columns.FindColumnIndex("ReportId", true);
				var displayNameIndex = columns.FindColumnIndex("DisplayName", true);
				var descriptionIndex = columns.FindColumnIndex("Description", true);

				while (await Task.Run(new Func<bool>(r.MoveNext)))
				{
					var viewId = r.GetValue<string>(reportIndex, null);

					lock (views)
					{
						if (!views.TryGetValue(viewId, out var tableInfo))
							views.Add(viewId, tableInfo = new PpsViewDefinition(this, viewId));
						tableInfo.DisplayName = r.GetValue<string>(displayNameIndex, null);
						tableInfo.Description = r.GetValue<string>(descriptionIndex, null);
					}
				}
			}

			await RefreshViewDefinitionAsync(this.Values);
		} // func RefreshAsync

		private async Task RefreshViewDefinitionAsync(IEnumerable< PpsViewDefinition> views)
		{
			var viewList = String.Join(",", views.Select(c => c.ViewId));
			if (String.IsNullOrEmpty(viewList))
				return;

			// fetch info
			var xResult = await environment.GetXmlData("bi/?action=tableinfo&v=" + viewList+ "&a=*,doc.description");

			// update views
			foreach (var x in xResult.Elements("table"))
			{
				var viewId = x.GetAttribute("name", null);
				if (TryGetValue(viewId, out var viewInfo))
					viewInfo.RefreshData(x);
			}
		} // func RefreshViewDefinitionAsync

		#endregion

		#region -- Dictionary Implementation ------------------------------------------

		public bool TryGetValue(string key, out PpsViewDefinition value)
		{
			lock (views)
				return views.TryGetValue(key, out value);
		} // func TryGetValue

		public bool ContainsKey(string key)
		{
			lock (views)
				return views.ContainsKey(key);
		} // func ContainsKey

		public IEnumerator<KeyValuePair<string, PpsViewDefinition>> GetEnumerator()
		{
			lock (views)
			{
				foreach (var cur in views)
					yield return cur;
			}
		} // func GetEnumerator

		IEnumerator IEnumerable.GetEnumerator()
			=> GetEnumerator();

		public PpsViewDefinition this[string key]
		{
			get
			{
				lock (views)
					return views[key];
			}
		} // prop this

		public IEnumerable<string> Keys
		{
			get
			{
				lock (views)
				{
					foreach (var k in views.Keys)
						yield return k;
				}
			}
		} // prop Keys

		public IEnumerable<PpsViewDefinition> Values
		{
			get
			{
				lock (views)
				{
					foreach (var v in views.Values)
						yield return v;
				}
			}
		} // prop Keys

		public int Count
		{
			get
			{
				lock (views)
					return views.Count;
			}
		} // prop Count

		#endregion
	} // class PpsViewDictionary

	#endregion
}

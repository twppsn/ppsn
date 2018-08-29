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
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using TecWare.DE.Data;
using TecWare.PPSn;
using TecWare.PPSn.Data;

namespace PPSnExcel
{
	internal partial class TableInsertForm : Form
	{
		private readonly PpsEnvironment env;

		private readonly Dictionary<string, TableInfo> availeTables = new Dictionary<string, TableInfo>(StringComparer.OrdinalIgnoreCase); // list of all server tables

		//private readonly string searchText;
		private readonly SelectedTable selectedTable = null;

		public TableInsertForm(PpsEnvironment env)
		{
			this.env = env ?? throw new ArgumentNullException(nameof(env));

			InitializeComponent();

			env.Spawn(RefreshAvailableTablesAsync);
		} // ctor

		private async Task RefreshAvailableTablesAsync()
		{
			var list = new PpsShellGetList("bi.reports")
			{
				Columns = new PpsDataColumnExpression[]
				{
					new PpsDataColumnExpression("ReportId"),
					new PpsDataColumnExpression("DisplayName"),
					new PpsDataColumnExpression("Description")
				},
				Filter = PpsDataFilterExpression.Compare("Type", PpsDataFilterCompareOperator.Equal, "table")
			};

			using (var r = env.GetViewData(list).GetEnumerator())
			{
				var reportIndex = r.FindColumnIndex("ReportId", true);
				var displayNameIndex = r.FindColumnIndex("DisplayName", true);
				var descriptionIndex = r.FindColumnIndex("Description", true);

				while (await Task.Run(new Func<bool>(r.MoveNext)))
				{
					var viewId = r.GetValue<string>(reportIndex, null);

					lock (availeTables)
					{
						if (!availeTables.TryGetValue(viewId, out var tableInfo))
							availeTables.Add(viewId, tableInfo = new TableInfo(viewId));

						tableInfo.DisplayName = r.GetValue<string>(displayNameIndex, null);
						tableInfo.Description = r.GetValue<string>(descriptionIndex, null);
					}
				}
			}

			await env.InvokeAsync(UpdateTreeViewUI);
		} // proc RefreshAvailableTablesAsync 

		private void UpdateTreeViewUI()
		{
			if (selectedTable != null) // table selected
			{
				tableTree.Nodes.Clear();
			}
			else
			{
			}
		} // proc UpdateTreeViewUI

		public string ReportName { get; private set; } = null;
		public PpsListMapping ReportSource { get; private set; } = null;
	} // class TableInsertForm

	#region -- class TableInfo --------------------------------------------------------

	internal class TableInfo : IDataColumns
	{
		private readonly List<SimpleDataColumn> columns = new List<SimpleDataColumn>();

		public TableInfo(string viewId)
		{
			ViewId = viewId ?? throw new ArgumentNullException(nameof(viewId));
		} // ctor

		public string ViewId { get; }

		public string DisplayName { get; set; }
		public string Description { get; set; }

		public IReadOnlyList<IDataColumn> Columns => columns;
	} // class TableInfo

	#endregion

	#region -- class SelectedTable ----------------------------------------------------

	internal sealed class SelectedTable
	{
	} // class SelectedTable

	#endregion
}

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
		public TableInsertForm(PpsEnvironment env)
		{
			InitializeComponent();
		} // ctor

		private Task RefreshAvailableTablesAsync()
			=> Task.CompletedTask;

		public string ReportName { get; private set; } = null;
		public PpsListMapping ReportSource { get; private set; } = null;
	} // class TableInsertForm

	#region -- class TableInfo --------------------------------------------------------

	internal class TableInfo : IDataColumns
	{
		private readonly string viewId;
		private readonly string displayName;
		private readonly List<SimpleDataColumn> columns = new List<SimpleDataColumn>();

		public IReadOnlyList<IDataColumn> Columns => columns;
	} // class TableInfo

	#endregion

	internal sealed class TableSelect
	{
		private readonly TableInfo table;
		private string onStatement;
	}

	internal class TableJoinExpression : PpsDataJoinExpression<TableInfo>
	{
		protected override string CreateOnStatement(PpsTableExpression left, PpsDataJoinType joinOp, PpsTableExpression right) => throw new NotImplementedException();
		protected override TableInfo ResolveTable(string tableName) => throw new NotImplementedException();
	}



}

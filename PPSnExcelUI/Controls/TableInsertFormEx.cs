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
using System.Windows.Forms;
using TecWare.PPSn.Data;

namespace TecWare.PPSn.Controls
{
	internal partial class TableInsertFormEx : Form
	{
		private readonly IPpsShell shell;
		private IPpsTableData currentData = null;
		private readonly PpsTableTextData tableTextData;

		public TableInsertFormEx(IPpsShell shell)
		{
			this.shell = shell ?? throw new ArgumentNullException(nameof(shell));

			InitializeComponent();

			tableTextData = new PpsTableTextData();

			displayNameText.DataBindings.Add(new Binding(nameof(TextBox.Text), tableTextData, nameof(PpsTableTextData.DisplayName), true, DataSourceUpdateMode.OnPropertyChanged));
			viewsText.DataBindings.Add(new Binding(nameof(TextBox.Text), tableTextData, nameof(PpsTableTextData.Views), true, DataSourceUpdateMode.OnPropertyChanged));
			filterText.DataBindings.Add(new Binding(nameof(TextBox.Text), tableTextData, nameof(PpsTableTextData.Filter), true, DataSourceUpdateMode.OnPropertyChanged));
			columnsText.DataBindings.Add(new Binding(nameof(TextBox.Text), tableTextData, nameof(PpsTableTextData.Columns), true, DataSourceUpdateMode.OnPropertyChanged));
		} // ctor

		public void LoadData(IPpsTableData tableData)
		{
			Text = TableInsertForm.GetEditTitle(tableData);
			refreshButton.Text = TableInsertForm.GetOkTitle(tableData);

			// load content
			currentData = tableData;
			tableTextData.Load(tableData);
		} // proc LoadData

		private async void refreshButton_Click(object sender, EventArgs e)
		{
			if (String.IsNullOrWhiteSpace(tableTextData.Views))
			{
				MessageBox.Show(this, "Kein View gewählt.");
				return;
			}
			if (String.IsNullOrWhiteSpace(tableTextData.Columns))
			{
				MessageBox.Show(this, "Keine Spalte gewählt.");
				return;
			}

			Enabled = false;
			try
			{
				var t = (IPpsTableData)tableTextData;
				currentData.DisplayName = t.DisplayName;
				
				await currentData.UpdateAsync(t.Views, t.Filter, t.Columns, false);

				DialogResult = DialogResult.OK;
			}
			catch (Exception ex)
			{
				shell.ShowException(ex);
			}
			finally
			{
				Enabled = true;
			}
		} // event refreshButton_Click
	}
}

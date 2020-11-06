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
using System.Data;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using TecWare.DE.Stuff;
using TecWare.PPSn;
using TecWare.PPSn.Core.Data;
using TecWare.PPSn.Data;

namespace PPSnExcel
{
	internal partial class ReportInsertForm : Form
	{
		private readonly IPpsShell shell;
		private Matrix scaleMatrix = new Matrix(1.0f, 0.0f, 0.0f, 1.0f, 0.0f, 0.0f);

		public ReportInsertForm(IPpsShell shell)
		{
			this.shell = shell ?? throw new ArgumentNullException(nameof(shell));

			InitializeComponent();

			// run in background
			RefreshViewAsync().Spawn(shell);
		} // ctor

		protected override void ScaleControl(SizeF factor, BoundsSpecified specified)
		{
			base.ScaleControl(factor, specified);

			scaleMatrix = new Matrix(factor.Width, 0.0f, 0.0f, factor.Height, 0.0f, 0.0f);
			dv.RowTemplate.Height = scaleMatrix.TransformX(38);
		} // proc ScaleControl

		protected override void OnClosing(CancelEventArgs e)
		{
			if (DialogResult == DialogResult.OK)
				e.Cancel = ReportId == null;
			base.OnClosing(e);
		} // proc OnClosing

		private async Task RefreshViewAsync()
		{
			var dt = await shell.GetViewDataAsync(
				new PpsDataQuery("bi.reports")
				{
					Columns = new PpsDataColumnExpression[]
					{
						new PpsDataColumnExpression("Type"),
						new PpsDataColumnExpression("ReportId"),
						new PpsDataColumnExpression("DisplayName"),
						new PpsDataColumnExpression("Description")
					}
				}
			);

			await shell.GetService<IPpsUIService>(true).RunUI(() => RefreshViewUI(dt));
		} // proc RefreshViewAsync

		private void RefreshViewUI(DataTable dt)
		{
			if (IsDisposed)
				return;

			bsReports.DataSource = dt;
			UpdateFilter(txtFilter.Text);
		} // proc RefreshViewUI

		private void UpdateFilter(string filterText)
		{
			var fragments = filterText.Split(new char[] { ' ', '*', '\'' }, StringSplitOptions.RemoveEmptyEntries);
			if (fragments.Length == 0)
				bsReports.Filter = String.Empty;
			else
			{
				var sb = new StringBuilder();
				foreach (var cur in fragments)
				{
					if (sb.Length > 0)
						sb.Append(" AND ");
					sb.Append("(DisplayName LIKE '*" + cur + "*' OR Description LIKE '*" + cur + "*')");
				}
				bsReports.Filter = sb.ToString();
			}
		} // proc UpdateFilter

		private void UpdateResult(DataRowView row)
		{
			if (row == null)
			{
				ReportName = null;
				ReportName = null;
				ReportId = null;

				cmdOk.Enabled = false;
			}
			else
			{
				ReportId = (string)row["ReportId"];
				ReportType = (string)row["Type"];
				ReportName = row["DisplayName"] as string ?? ReportId;

				cmdOk.Enabled = true;
			}
		} // proc UpdateResult

		private static Image GetTypeImage(string type)
		{
			switch (type)
			{
				case "table":
					return Properties.Resources.TableTypeImage;
				case "xlsx":
				default:
					return Properties.Resources.ReportTypeImage;
			}
		} // func GetTypeImage

		private void txtFilter_TextChanged(object sender, EventArgs e)
		{
			UpdateFilter(txtFilter.Text);
		} // event txtFilter_TextChanged

		private void dv_CellPainting(object sender, DataGridViewCellPaintingEventArgs e)
		{
			if (e.RowIndex < 0)
				return;

			if (dv.Rows[e.RowIndex].DataBoundItem is DataRowView v)
			{
				e.Paint(e.ClipBounds, DataGridViewPaintParts.Background | DataGridViewPaintParts.SelectionBackground);
				if (e.ColumnIndex == 0)
				{
					var displayName = v["DisplayName"] as string;
					var description = v["Description"] as string;
					var typeImage = GetTypeImage(v["Type"] as string);
					if (String.IsNullOrEmpty(displayName))
						displayName = (string)v["ReportId"];

					var g = e.Graphics;
					using (var fmt = new StringFormat(StringFormatFlags.NoWrap) { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter })
					using (var br = new SolidBrush((e.State & DataGridViewElementStates.Selected) != 0 ? e.CellStyle.SelectionForeColor : e.CellStyle.ForeColor))
					using (var fnt = new Font(Font, FontStyle.Bold))
					{
						var pts = new Point[]
						{
							new Point(3, 3),
							new Point(32, 32),
							new Point(40, 3),
							new Point(6,6)
						};

						scaleMatrix.TransformPoints(pts);

						var rc = new Rectangle(e.CellBounds.Left + pts[0].X, e.CellBounds.Top + pts[0].Y,
							pts[1].X, pts[1].Y
						);
						if (typeImage != null)
							g.DrawImage(typeImage, rc);

						rc = new Rectangle(
							e.CellBounds.Left + pts[2].X,
							e.CellBounds.Top + pts[2].Y,
							e.CellBounds.Width - pts[3].X,
							(e.CellBounds.Height - pts[3].Y) / 2
						);
						g.DrawString(displayName, fnt, br, rc, fmt);

						rc.Y = rc.Top + rc.Height;
						g.DrawString(description, Font, br, rc, fmt);
					}
				}
			}

			e.Handled = true;
		} // proc dv_CellPainting

		private void dv_CellDoubleClick(object sender, DataGridViewCellEventArgs e)
		{
			if (e.RowIndex >= 0)
				DialogResult = DialogResult.OK;
		} // event dv_CellDoubleClick

		private void dv_SelectionChanged(object sender, EventArgs e)
		{
			if (dv.SelectedRows.Count > 0)
				UpdateResult(dv.SelectedRows[0].DataBoundItem as DataRowView);
			else
				UpdateResult(null);
		} // event dv_SelectionChanged

		public string ReportName { get; private set; } = null;
		public string ReportId { get; private set; } = null;
		public string ReportType { get; private set; } = null;
	} // class ReportInsertForm
}

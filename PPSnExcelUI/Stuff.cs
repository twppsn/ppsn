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
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using TecWare.DE.Data;
using TecWare.DE.Stuff;
using TecWare.PPSn.Controls;
using TecWare.PPSn.Data;

namespace TecWare.PPSn
{
	#region -- class ExcelException ---------------------------------------------------

	/// <summary></summary>
	public class ExcelException : Exception
	{
		/// <summary></summary>
		/// <param name="message"></param>
		public ExcelException(string message)
			: base(message)
		{
		} // ctor
	} // class ExcelException

	#endregion

	public static class XlProcs
	{
		private static readonly Regex lineProperties = new Regex(@"^\s*@@(?<k>\w+)\s*\=\s*(?<v>.*)\s*$", RegexOptions.Compiled);
		private static readonly Regex lineSplitter = new Regex(@"\r\n|\r|\n", RegexOptions.Compiled);

		public static string UpdateProperties(string comment, params KeyValuePair<string, string>[] args)
		{
			var sb = new StringBuilder();

			void AppendKeyValue(KeyValuePair<string, string> kv)
			{
				if (!String.IsNullOrEmpty(kv.Value))
					sb.Append("@@").Append(kv.Key).Append('=').AppendLine(kv.Value.Replace("\n", "\\n"));
			} // proc AppendKeyValue

			var updated = new bool[args.Length];
			for (var i = 0; i < updated.Length; i++)
				updated[i] = false;

			foreach (var l in lineSplitter.Split(comment ?? String.Empty))
			{
				var m = lineProperties.Match(l);
				if (m.Success)
				{
					var k = m.Groups["k"].Value;
					var i = Array.FindIndex(args, kv => kv.Key == k);
					if (i == -1)
						sb.AppendLine(l);
					else
					{
						updated[i] = true;
						AppendKeyValue(args[i]);
					}
				}
				else
					sb.AppendLine(l);
			}

			for (var i = 0; i < updated.Length; i++)
			{
				if (!updated[i])
					AppendKeyValue(args[i]);
			}

			return sb.ToString();
		} // func UpdateProperties

		public static IEnumerable<KeyValuePair<string, string>> GetLineProperties(string comment)
		{
			foreach (var l in lineSplitter.Split(comment ?? String.Empty))
			{
				var m = lineProperties.Match(l);
				if (m.Success)
					yield return new KeyValuePair<string, string>(m.Groups["k"].Value, m.Groups["v"].Value);
			}
		} // func GetLineProperties

		#region -- Transform - dpi ----------------------------------------------------

		public static int TransformX(this Matrix matrix, int x)
			=> TransformPoint(matrix, new Point(x, 0)).X;

		public static int TransformY(this Matrix matrix, int y)
			=> TransformPoint(matrix, new Point(0, y)).Y;

		public static Point TransformPoint(this Matrix matrix, Point pt)
		{
			var pts = new Point[] { pt };
			matrix.TransformPoints(pts);
			return pts[0];
		} // proc TransformPoint

		public static Rectangle TransformRect(this Matrix matrix, Rectangle rc)
		{
			var pts = new Point[] { rc.Location, new Point(rc.Right, rc.Bottom) };
			matrix.TransformPoints(pts);
			return new Rectangle(pts[0].X, pts[0].Y, pts[1].X - pts[0].X, pts[1].Y - pts[0].Y);
		} // proc TransformPoint

		#endregion

		#region -- Dialogs ------------------------------------------------------------

		public static void ShowException(this IWin32Window owner, ExceptionShowFlags flags, Exception exception, string alternativeMessage = null)
		{
			var unpackedException = exception.UnpackException();

			if ((flags & ExceptionShowFlags.Warning) != 0)
				MessageBox.Show(owner, alternativeMessage ?? unpackedException.Message, "Hinweis", MessageBoxButtons.OK, MessageBoxIcon.Warning);
			else if (unpackedException is ExcelException excelException)
				MessageBox.Show(owner, alternativeMessage ?? excelException.Message, "Fehler", MessageBoxButtons.OK, MessageBoxIcon.Error);
			else
			{
				using (var exceptionDialog = new ExceptionDialog())
				{
					exceptionDialog.SetData(alternativeMessage, unpackedException, false);
					exceptionDialog.ShowDialog(owner);
				}
			}
		} // proc ShowException

		private static string GetMessageTitle(MessageBoxIcon icon)
		{
			switch (icon)
			{
				case MessageBoxIcon.Information:
					return "Information";
				case MessageBoxIcon.Warning:
					return "Warnung";
				case MessageBoxIcon.Question:
					return "Frage";
				case MessageBoxIcon.Error:
					return "Fehler";
				default:
					return icon.ToString();
			}
		} // func GetMessageTitle

		public static DialogResult ShowMessage(this IWin32Window owner, string message, MessageBoxIcon icon = MessageBoxIcon.Information, MessageBoxButtons buttons = MessageBoxButtons.OK, MessageBoxDefaultButton defaultButton = MessageBoxDefaultButton.Button1)
			=> MessageBox.Show(owner, message, GetMessageTitle(icon), buttons, icon, defaultButton);

		#endregion

		public static PpsDataOrderExpression ToOrder(this IPpsTableColumn column)
			=> column.Ascending.HasValue ? new PpsDataOrderExpression(!column.Ascending.Value, column.Expression) : null;

		public static IEnumerable<PpsDataOrderExpression> ToOrder(this IEnumerable<IPpsTableColumn> columns)
			=> columns?.Select(ToOrder).Where(c => c != null);

		public static IEnumerable<PpsDataOrderExpression> ToOrder(this IPpsTableData tableData)
			=> ToOrder(tableData.Columns);

		public static PpsDataColumnExpression ToColumn(this IPpsTableColumn column)
			=> new PpsDataColumnExpression(column.Expression);

		public static bool GetIsNullable(this IDataColumn col)
			=> col.Attributes.TryGetProperty<bool>("nullable", out var tmp) && tmp;
	} // class XlProcs
}

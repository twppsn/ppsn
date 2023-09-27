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
using TecWare.DE.Stuff;
using TecWare.PPSn.Controls;
using TecWare.PPSn.Data;

namespace TecWare.PPSn
{
	#region -- class PpsWinShell ------------------------------------------------------

	public static partial class PpsWinShell
	{
		#region -- Dialogs ------------------------------------------------------------

		public static void ShowException(this IWin32Window owner, PpsExceptionShowFlags flags, Exception exception, string alternativeMessage = null)
		{
			var unpackedException = exception.GetInnerException();

			if ((flags & PpsExceptionShowFlags.Warning) != 0)
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

		public static MessageBoxIcon GetIconFromImage(PpsImage image)
		{
			switch (image)
			{
				case PpsImage.Error:
					return MessageBoxIcon.Error;
				case PpsImage.Warning:
					return MessageBoxIcon.Warning;
				case PpsImage.Question:
					return MessageBoxIcon.Question;
				default:
					return MessageBoxIcon.Information;
			}
		} // GetIconFromImage

		public static MessageBoxButtons GetMessageButtons(string[] buttons)
		{
			if (buttons.Length == 1 && buttons[0] == "Ok")
				return MessageBoxButtons.OK;
			else if (buttons.Length == 2 && buttons[0] == "Ok" && buttons[1] == "Cancel")
				return MessageBoxButtons.OKCancel;
			else if (buttons.Length == 2 && buttons[0] == "Yes" && buttons[1] == "No")
				return MessageBoxButtons.YesNo;
			else
				throw new NotImplementedException();
		} // func GetMessageButtons

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

		public static DialogResult ShowMessage(this IWin32Window owner, object message, MessageBoxIcon icon = MessageBoxIcon.Information, MessageBoxButtons buttons = MessageBoxButtons.OK, MessageBoxDefaultButton defaultButton = MessageBoxDefaultButton.Button1)
			=> MessageBox.Show(owner, message.ToString(), GetMessageTitle(icon), buttons, icon, defaultButton);

		public static int ShowMessage(IWin32Window owner, object message, PpsImage image, string[] buttons)
		{
			switch (ShowMessage(owner, message, GetIconFromImage(image), GetMessageButtons(buttons)))
			{
				case DialogResult.Yes:
				case DialogResult.OK:
					return 0;
				case DialogResult.No:
				case DialogResult.Cancel:
					return 1;
				default:
					return -1;
			}
		} // proc ShowMessage

		public static IPpsShellInfo CreateNewShellDialog(IWin32Window owner)
			=> NewShellForm.CreateNew(owner);

		#endregion

		#region -- EditTable ----------------------------------------------------------

		/// <summary>Edit the current table</summary>
		/// <param name="shell"></param>
		/// <param name="table"></param>
		/// <param name="owner"<
		/// <param name="extended"></param>
		/// <returns><c>true</c>, if data was refreshed.</returns>
		public static bool EditTable(this IPpsShell shell, IPpsTableData table, bool extended)
		{
			if (table == null)
				throw new ArgumentNullException(nameof(table));

			if (extended)
			{
				using (var frm = new TableInsertFormEx(shell))
				{
					frm.LoadData(table);
					return frm.ShowDialog(shell.GetService<IWin32Window>()) == DialogResult.OK;
				}
			}
			else
			{
				using (var frm = new TableInsertForm(shell))
				{
					frm.LoadData(table);
					return frm.ShowDialog(shell.GetService<IWin32Window>()) == DialogResult.OK;
				}
			}
		} // void EditTable

		#endregion
	} // class PpsWinShell

	#endregion
}

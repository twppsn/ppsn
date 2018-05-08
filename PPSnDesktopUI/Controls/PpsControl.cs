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
using System.Windows.Input;

namespace TecWare.PPSn.Controls
{
	#region -- interface IPpsNullableControl ------------------------------------------

	/// <summary></summary>
	public interface IPpsNullableControl
	{
		/// <summary>Clear the value.</summary>
		void Clear();
		/// <summary>Can the value cleared.</summary>
		bool CanClear { get; }
		/// <summary>Is the field nullable.</summary>
		bool IsNullable { get; }
	} // interface IPpsNullableControl

	#endregion

	#region -- class PpsControlCommands -----------------------------------------------

	/// <summary></summary>
	public static class PpsControlCommands
	{
		/// <summary>The Command empties the TextBox</summary>
		public static readonly RoutedCommand ClearCommand = new RoutedUICommand("Clear", "Clear", typeof(PpsControlCommands));
		/// <summary></summary>
		public static readonly RoutedCommand SelectCommand = new RoutedUICommand("Select", "Select", typeof(PpsControlCommands));

		/// <summary></summary>
		/// <param name="type"></param>
		public static void RegisterClearCommand(Type type)
		{
			CommandManager.RegisterClassCommandBinding(
				type,
				new CommandBinding(
					ClearCommand,
					ClearExecuted,
					ClearCanExecute
				)
			);
		} // func RegisterClearCommand

		private static void ClearExecuted(object sender, ExecutedRoutedEventArgs e)
		{
			if (e.Source is IPpsNullableControl nc)
				nc.Clear();
		} // event ClearExecuted

		private static void ClearCanExecute(object sender, CanExecuteRoutedEventArgs e)
		{
			if (e.Source is IPpsNullableControl nc)
			{
				e.CanExecute = nc.CanClear;
				e.Handled = true;
			}
		} // event ClearCanExecute
	} // class PpsControlCommands

	#endregion
}

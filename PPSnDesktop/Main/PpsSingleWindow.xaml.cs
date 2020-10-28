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
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Neo.IronLua;
using TecWare.PPSn.Controls;
using TecWare.PPSn.UI;

namespace TecWare.PPSn.Main
{
	/// <summary>Single Window to show one pane.</summary>
	internal partial class PpsSingleWindow : PpsWindow, IPpsWindowPaneManager
	{
		private readonly static DependencyPropertyKey isDialogModePropertyKey = DependencyProperty.RegisterReadOnly(nameof(IsDialogMode), typeof(bool), typeof(PpsSingleWindow), new FrameworkPropertyMetadata(false));
		/// <summary>Dialog mode of the window.</summary>
		public readonly static DependencyProperty IsDialogModeProperty = isDialogModePropertyKey.DependencyProperty;

		/// <summary></summary>
		/// <param name="shell"></param>
		/// <param name="dialogMode"></param>
		public PpsSingleWindow(IPpsShell shell, bool dialogMode)
			: base(shell)
		{
			this.IsDialogMode = dialogMode;

			InitializeComponent();

			if (dialogMode)
			{
				if (Owner != null)
					WindowStartupLocation = WindowStartupLocation.CenterOwner;
				else
					WindowStartupLocation = WindowStartupLocation.CenterScreen;

				if (Height > SystemParameters.VirtualScreenHeight)
					Height = SystemParameters.VirtualScreenHeight - 75;
				if (Width > SystemParameters.VirtualScreenWidth)
					Width = SystemParameters.VirtualScreenWidth - 75;
			}
			else
			{
				if (Owner != null)
					WindowStartupLocation = WindowStartupLocation.CenterOwner;
				else
					WindowStartupLocation = WindowStartupLocation.CenterScreen;

				if (Height > SystemParameters.VirtualScreenHeight)
					Height = SystemParameters.VirtualScreenHeight - 75;
				if (Width > SystemParameters.VirtualScreenWidth)
					Width = SystemParameters.VirtualScreenWidth - 75;
			}
		} // ctor

		private void Close_Executed(object sender, ExecutedRoutedEventArgs e)
			=> Close();

		private IPpsWindowPane FindOpenPane(Type paneType, LuaTable arguments)
			=> CurrentPane != null && CurrentPane.EqualPane(paneType, arguments) ? CurrentPane : null;

		bool IPpsWindowPaneManager.ActivatePane(IPpsWindowPane pane)
			=> pane == CurrentPane && Activate();

		IPpsWindowPane IPpsWindowPaneManager.FindOpenPane(Type paneType, LuaTable arguments)
			=> FindOpenPane(paneType, arguments);

		/// <summary>Open pane within the window.</summary>
		/// <param name="paneType"></param>
		/// <param name="newPaneMode"></param>
		/// <param name="arguments"></param>
		/// <returns></returns>
		public Task<IPpsWindowPane> OpenPaneAsync(Type paneType, PpsOpenPaneMode newPaneMode = PpsOpenPaneMode.Default, LuaTable arguments = null)
		{
			switch (newPaneMode)
			{
				case PpsOpenPaneMode.Default:
				case PpsOpenPaneMode.ReplacePane:
					{
						var pane = FindOpenPane(paneType, arguments);
						return pane == null
							? paneHost.LoadAsync(this, paneType, arguments ?? new LuaTable())
							: Task.FromResult(pane);
					}
				case PpsOpenPaneMode.NewPane:
					return this.GetControlService<IPpsMainWindowService>(true).OpenPaneAsync(paneType, PpsOpenPaneMode.NewSingleWindow, arguments);
				default:
					return this.GetControlService<IPpsMainWindowService>(true).OpenPaneAsync(paneType, newPaneMode, arguments);
			}
		} // func OpenPaneAsync

		IEnumerable<IPpsWindowPane> IPpsWindowPaneManager.Panes
		{
			get
			{
				if (CurrentPane != null)
					yield return CurrentPane;
			}
		} // prop Panes

		private IPpsWindowPane CurrentPane => paneHost.Pane;

		/// <summary>Is the the single window in dialog mode?</summary>
		public bool IsDialogMode { get => (bool)GetValue(IsDialogModeProperty); private set => SetValue(isDialogModePropertyKey, value); }
	} // class PpsSingleWindow
}

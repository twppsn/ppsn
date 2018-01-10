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
using Neo.IronLua;

namespace TecWare.PPSn.UI
{
	/// <summary></summary>
	public partial class PpsSingleWindow : PpsWindow, IPpsWindowPaneManager
	{
		private readonly static DependencyPropertyKey isDialogModePropertyKey = DependencyProperty.RegisterReadOnly(nameof(IsDialogMode), typeof(bool), typeof(PpsSingleWindow), new FrameworkPropertyMetadata(false));
		/// <summary></summary>
		public readonly static DependencyProperty IsDialogModeProperty = isDialogModePropertyKey.DependencyProperty;

		private readonly PpsEnvironment environment;
		private IPpsWindowPane currentPane = null;
		
		public PpsSingleWindow(PpsEnvironment environment, bool dialogMode)
		{
			this.environment = environment;
			this.IsDialogMode = dialogMode;

			InitializeComponent();

			if (dialogMode)
			{
				WindowStartupLocation = WindowStartupLocation.CenterOwner;
			}
			else
			{

			}
		} // ctor

		private IPpsWindowPane FindOpenPane(Type paneType, LuaTable arguments)
			=> currentPane != null && currentPane.EqualPane(paneType, arguments) ? currentPane : null;

		bool IPpsWindowPaneManager.ActivatePane(IPpsWindowPane pane)
				=> pane == currentPane ? Activate() : false;

		IPpsWindowPane IPpsWindowPaneManager.FindOpenPane(Type paneType, LuaTable arguments)
			=> FindOpenPane(paneType, arguments);

		Type IPpsWindowPaneManager.GetPaneType(PpsWellknownType wellknownType)
			=> ((PpsMainEnvironment)environment).GetPaneType(wellknownType);

		private async Task<IPpsWindowPane> CreatePaneAsync(Type paneType, LuaTable arguments)
		{
			arguments = arguments ?? new LuaTable();

			var pane = this.CreateEmptyPane(paneType);
			await pane.LoadAsync(arguments);

			currentPane = pane;
			DataContext = currentPane;

			return pane;
		} // func CreatePaneAsync

		/// <summary></summary>
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
					var pane = FindOpenPane(paneType, arguments);
					return pane == null ? CreatePaneAsync(paneType, arguments) : Task.FromResult(pane);
				case PpsOpenPaneMode.NewPane:
					return ((PpsMainEnvironment)environment).OpenPaneAsync(paneType, PpsOpenPaneMode.NewSingleWindow, arguments);
				default:
					return ((PpsMainEnvironment)environment).OpenPaneAsync(paneType, newPaneMode, arguments);
			}
		} // func OpenPaneAsync

		IEnumerable<IPpsWindowPane> IPpsWindowPaneManager.Panes
		{
			get
			{
				if (currentPane != null)
					yield return currentPane;
			}
		} // prop Panes

		/// <summary>Is the the single window in dialog mode?</summary>
		public bool IsDialogMode { get => (bool)GetValue(IsDialogModeProperty); private set => SetValue(isDialogModePropertyKey, value); }
	} // class PpsSingleWindow
}

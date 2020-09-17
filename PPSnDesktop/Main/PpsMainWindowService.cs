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
using System.Threading.Tasks;
using System.Windows;
using Neo.IronLua;
using TecWare.PPSn.UI;

namespace TecWare.PPSn.Main
{
	#region -- interface IPpsMainWindow -----------------------------------------------

	/// <summary>Interface to the main window.</summary>
	public interface IPpsMainWindow : IPpsWindowPaneManager
	{
		/// <summary>Title of the main window.</summary>
		string Title { get; }
		/// <summary>Index of the main window</summary>
		int WindowIndex { get; }
	} // interface IPpsMainWindow

	#endregion

	#region -- interface IPpsMainWindowService ----------------------------------------

	/// <summary>Global main window service.</summary>
	public interface IPpsMainWindowService : IPpsWindowPaneManager, IPpsShellService
	{
		/// <summary>Return a pane manager</summary>
		/// <returns></returns>
		IPpsWindowPaneManager GetDefaultPaneManager();

		/// <summary>Get a main window by index.</summary>
		/// <param name="index"></param>
		/// <returns></returns>
		IPpsMainWindow GetMainWindow(int index);

		/// <summary>Get all main windows.</summary>
		IEnumerable<IPpsMainWindow> MainWindows { get; }
		/// <summary>Get all child pane manager</summary>
		IEnumerable<IPpsWindowPaneManager> PaneManagers { get; }
	} // interface IPpsGlobalWindowPaneManager

	#endregion

	#region -- class PpsMainWindowService ---------------------------------------------

	[
	PpsService(typeof(IPpsWindowPaneManager)),
	PpsService(typeof(IPpsMainWindowService)),
	PpsLazyService()
	]
	internal sealed class PpsMainWindowService : IPpsMainWindowService, IPpsWindowPaneManager, IPpsShellService
	{
		private readonly IPpsShell shell;

		public PpsMainWindowService(IPpsShell shell)
		{
			this.shell = shell ?? throw new ArgumentNullException(nameof(shell));
		} // ctor

		#region -- Pane Manager -------------------------------------------------------

		private PpsMainWindow CreateMainWindow()
		{
			// find a free window index
			var freeIndex = 0;
			while (GetMainWindow(freeIndex) != null)
				freeIndex++;

			var window = new PpsMainWindow(shell, freeIndex);
			window.Show();
			return window;
		} // proc CreateMainWindow

		public IPpsMainWindow GetMainWindow(int index)
		{
			foreach (var c in Application.Current.Windows)
			{
				if (c is IPpsMainWindow w && w.WindowIndex == index)
					return w;
			}
			return null;
		} // func GetMainWindow

		public IEnumerable<IPpsMainWindow> MainWindows
		{
			get
			{
				foreach (var c in Application.Current.Windows)
				{
					if (c is IPpsMainWindow w && ReferenceEquals(w.Shell, shell))
						yield return w;
				}
			}
		} // prop MainWindows

		public IEnumerable<IPpsWindowPaneManager> PaneManagers
		{
			get
			{
				foreach (var c in Application.Current.Windows)
				{
					if (c is IPpsWindowPaneManager w && ReferenceEquals(w.Shell, shell))
						yield return w;
				}
			}
		} // prop PaneManagers

		#endregion

		#region -- IPpsWindowPaneManager - members ------------------------------------

		private IEnumerable<IPpsWindowPane> GetAllWindowPanes()
			=> from pm in PaneManagers from p in pm.Panes select p;

		PpsShellWpf IPpsWindowPaneManager._Shell => null;

		public bool ActivatePane(IPpsWindowPane pane)
			=> pane?.PaneHost.PaneManager.ActivatePane(pane) ?? false;

		public IPpsWindowPane FindOpenPane(Type paneType, LuaTable arguments = null)
		{
			foreach (var p in Panes)
			{
				if (p.EqualPane(paneType, arguments))
					return p;
			}
			return null;
		} // func FindOpenPane

		public async Task<IPpsWindowPane> OpenPaneAsync(Type paneType, PpsOpenPaneMode newPaneMode = PpsOpenPaneMode.Default, LuaTable arguments = null)
		{
			// find pane
			var pane = FindOpenPane(paneType, arguments);
			if (pane == null)
			{
				// open pane
				switch (newPaneMode)
				{
					case PpsOpenPaneMode.Default:
					case PpsOpenPaneMode.NewMainWindow:
						{
							var window = CreateMainWindow();
							return await window.OpenPaneAsync(paneType, PpsOpenPaneMode.NewPane, arguments);
						}
					case PpsOpenPaneMode.NewSingleWindow:
						{
							var window = new PpsSingleWindow(shell, false);
							window.Show();
							return await window.OpenPaneAsync(paneType, PpsOpenPaneMode.NewPane, arguments);
						}
					case PpsOpenPaneMode.NewSingleDialog:
						{
							var window = new PpsSingleWindow(shell, false);
							if (arguments?.GetMemberValue("DialogOwner") is Window dialogOwner)
								window.Owner = dialogOwner;
							var dlgPane = await window.OpenPaneAsync(paneType, PpsOpenPaneMode.Default, arguments);
							var r = window.ShowDialog();
							if (arguments != null && r.HasValue)
								arguments["DialogResult"] = r;
							return dlgPane;
						}
					default:
						throw new ArgumentOutOfRangeException(nameof(newPaneMode), newPaneMode, $"Only {nameof(PpsOpenPaneMode.Default)}, {nameof(PpsOpenPaneMode.NewMainWindow)} and {nameof(PpsOpenPaneMode.NewSingleWindow)} is allowed.");
				}
			}
			else
			{
				ActivatePane(pane);
				return pane;
			}
		} // func OpenPaneAsync


		public IPpsWindowPaneManager GetDefaultPaneManager()
		{
			// get the current pane manager
			var w = PaneManagers.FirstOrDefault(c => c.IsActive);
			// or self
			return w ?? this;
		} // func GetDefaultPaneManager

		public object GetService(Type serviceType)
			=> shell.GetService(serviceType);

		#endregion

		/// <summary>Is never the active pane manager.</summary>
		public bool IsActive => false;
		public IEnumerable<IPpsWindowPane> Panes => GetAllWindowPanes();

		public IPpsShell Shell => shell;
	} // class PpsMainWindowService

	#endregion
}

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
using System.Net;
using System.Threading.Tasks;
using System.Windows;
using System.Xml.Linq;
using Neo.IronLua;
using TecWare.PPSn.UI;

namespace TecWare.PPSn
{
	/// <summary></summary>
	public partial class PpsMainEnvironment : PpsEnvironment, IPpsWindowPaneManager
	{
		private readonly App app;
		private readonly PpsEnvironmentCollection<PpsMainActionDefinition> actions;
		private readonly PpsEnvironmentCollection<PpsMainViewDefinition> views;

		private readonly PpsProgressStack backgroundProgress;
		private readonly PpsProgressStack forgroundProgress;

		#region -- Ctor/Dtor ----------------------------------------------------------

		public PpsMainEnvironment(PpsEnvironmentInfo info, NetworkCredential userInfo, App app)
			: base(info, userInfo, app.Resources)
		{
			this.app = app;

			this.actions = new PpsEnvironmentCollection<PpsMainActionDefinition>(this);
			this.views = new PpsEnvironmentCollection<PpsMainViewDefinition>(this);

			this.backgroundProgress = new PpsProgressStack(app.Dispatcher);
			this.forgroundProgress = new PpsProgressStack(app.Dispatcher);
		} // ctor

		internal Task<bool> ShutdownAsync()
		{
			Dispose();
			return Task.FromResult<bool>(true);
		} // func ShutdownAsync

		#endregion

		protected async override Task OnSystemOnlineAsync()
		{
			await base.OnSystemOnlineAsync();
			await RefreshNavigatorAsync();
		} // proc OnSystemOnlineAsync

		protected async override Task OnSystemOfflineAsync()
		{
			await base.OnSystemOfflineAsync();
			await RefreshNavigatorAsync();
		} // proc OnSystemOfflineAsync

		private static readonly XName xnEnvironment = "environment";
		private static readonly XName xnCode = "code";
		private static readonly XName xnNavigator = "navigator";

		private async Task RefreshNavigatorAsync()
		{
			// clear the views
			await Dispatcher.InvokeAsync(() =>
			{
				views.Clear();
				actions.Clear();
			});

			try
			{
				// get the views from storage
				var xEnvironment = await Request.GetXmlAsync("wpf/environment.xml", rootName: xnEnvironment);

				// read navigator content
				var xNavigator = xEnvironment.Element(xnNavigator);
				if (xNavigator != null)
				{

					// append the new views
					foreach (var cur in xNavigator.Elements(PpsMainViewDefinition.xnView))
						views.AppendItem(new PpsMainViewDefinition(this, cur));

					// append the new actions
					var priority = 0;
					foreach (var cur in xNavigator.Elements(PpsMainActionDefinition.xnAction))
						actions.AppendItem(new PpsMainActionDefinition(this, cur, ref priority));

					// update document info
					lock (GetObjectInfoSyncObject())
					{
						var removeList = GetRemoveListObjectInfo();
						foreach (var cur in xNavigator.Elements(XName.Get("document")))
							UpdateObjectInfo(cur, removeList);
						ClearObjectInfo(removeList);
					}
				}

				// run environment extensions
				var code = xEnvironment.Element(xnCode)?.Value;
				if (!String.IsNullOrEmpty(code))
				{
					var chunk = await CompileAsync(code, "environment.lua", true, new KeyValuePair<string, Type>("self", typeof(LuaTable)));
					await Dispatcher.InvokeAsync(() => RunScript(chunk, this, true, this));
				}
			}
			catch (WebException ex)
			{
				if (ex.Status == WebExceptionStatus.ProtocolError) // e.g. file not found
					await ShowExceptionAsync(ExceptionShowFlags.Background, ex);
				else
					throw;
			}
		} // proc RefreshNavigatorAsync

		#region -- Window Manager -----------------------------------------------------

		public async Task<PpsMainWindow> CreateMainWindowAsync()
		{
			return await Dispatcher.InvokeAsync(() =>
			{
				// find a free window index
				var freeIndex = 0;
				while (GetWindow(freeIndex) != null)
					freeIndex++;

				var window = new PpsMainWindow(freeIndex);
				window.Show();
				return window;
			}
			);
		} // proc CreateMainWindow

		public PpsMainWindow GetWindow(int index)
		{
			foreach (var c in Application.Current.Windows)
			{
				if (c is PpsMainWindow w && w.WindowIndex == index)
					return w;
			}
			return null;
		} // func GetWindow

		public IEnumerable<PpsMainWindow> GetWindows()
		{
			foreach (var c in Application.Current.Windows)
			{
				if (c is PpsMainWindow w)
					yield return w;
			}
		} // func GetWindows

		#endregion

		#region -- Pane Manager -------------------------------------------------------

		/// <summary></summary>
		/// <param name="pane"></param>
		/// <returns></returns>
		public bool ActivatePane(IPpsWindowPane pane)
			=> pane?.PaneManager.ActivatePane(pane) ?? false;

		/// <summary></summary>
		/// <param name="paneType"></param>
		/// <param name="newPaneMode"></param>
		/// <param name="arguments"></param>
		/// <returns></returns>
		public async Task<IPpsWindowPane> OpenPaneAsync(Type paneType, PpsOpenPaneMode newPaneMode, LuaTable arguments)
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
							var window = await CreateMainWindowAsync();
							return await window.OpenPaneAsync(paneType, PpsOpenPaneMode.Default, arguments);
						}
					case PpsOpenPaneMode.NewSingleWindow:
						{
							var window = new PpsSingleWindow(this, false);
							window.Show();
							return await window.OpenPaneAsync(paneType, PpsOpenPaneMode.Default, arguments);
						}
					case PpsOpenPaneMode.NewSingleDialog:
						{
							var window = new PpsSingleWindow(this, false);
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

		/// <summary></summary>
		/// <param name="paneType"></param>
		/// <param name="arguments"></param>
		/// <returns></returns>
		public IPpsWindowPane FindOpenPane(Type paneType, LuaTable arguments)
		{
			foreach (var p in Panes)
			{
				if (p.EqualPane(paneType, arguments))
					return p;
			}
			return null;
		} // func FindOpenPane

		/// <summary></summary>
		/// <param name="wellknownType"></param>
		/// <returns></returns>
		public Type GetPaneType(PpsWellknownType wellknownType)
		{
			switch(wellknownType)
			{
				case PpsWellknownType.Generic:
					return typeof(PpsGenericMaskWindowPane);
				case PpsWellknownType.Mask:
					return typeof(PpsGenericMaskWindowPane);
				default:
					throw new ArgumentOutOfRangeException(nameof(wellknownType), wellknownType, "Invalid argument.");
			}
		} // func GetPaneType

		public IEnumerable<IPpsWindowPane> Panes
		{
			get
			{
				foreach(var w in GetWindows())
				{
					if (w is IPpsWindowPaneManager m)
					{
						foreach (var p in m.Panes)
							yield return p;
					}
				}
			}
		} // prop Panes

		PpsEnvironment IPpsWindowPaneManager.Environment => this;
		bool IPpsWindowPaneManager.IsActive => false;

		#endregion

		public override Type TracePaneType 
			=> typeof(PpsTracePane);

		/// <summary>List of actions defined for an context.</summary>
		[LuaMember(nameof(Actions))]
		public PpsEnvironmentCollection<PpsMainActionDefinition> Actions => actions;
		/// <summary>Navigator views.</summary>
		[LuaMember(nameof(Views))]
		public PpsEnvironmentCollection<PpsMainViewDefinition> Views => views;
		
		[
		LuaMember(nameof(TestBackgroundProgressState)),
		Obsolete("Only test propose.")
		]
		public IPpsProgress TestBackgroundProgressState()
			=> BackgroundProgressState.CreateProgress();

		[
		LuaMember(nameof(TestForegroundProgressState)),
		Obsolete("Only test propose.")
		]
		public IPpsProgress TestForegroundProgressState()
			=> ForegroundProgressState.CreateProgress();

		[LuaMember(nameof(BackgroundProgressState))]
		public PpsProgressStack BackgroundProgressState => backgroundProgress;
		[LuaMember(nameof(ForegroundProgressState))]
		public PpsProgressStack ForegroundProgressState => forgroundProgress;
	} // class PpsMainEnvironment
}

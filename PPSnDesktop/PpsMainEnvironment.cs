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
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Xml.Linq;
using Neo.IronLua;
using TecWare.DE.Networking;
using TecWare.DE.Stuff;
using TecWare.PPSn.Data;
using TecWare.PPSn.Stuff;
using TecWare.PPSn.UI;

namespace TecWare.PPSn
{
  #region -- class PpsMainActionDefinition --------------------------------------------

  ///////////////////////////////////////////////////////////////////////////////
  /// <summary></summary>
  public class PpsMainActionDefinition : PpsEnvironmentDefinition
	{
		public static readonly XName xnActions = "actions";
		public static readonly XName xnAction = "action";
		public static readonly XName xnCondition = "condition";
		public static readonly XName xnCode = "code";

		private readonly string displayName;
		private readonly int displayGlyph;
		private readonly bool isHidden;
		private readonly LuaChunk condition;
		private readonly LuaChunk code;

		internal PpsMainActionDefinition(PpsMainEnvironment environment, XElement xCur, ref int priority)
			: base(environment, xCur.GetAttribute("name", String.Empty))
		{
			this.displayName = xCur.GetAttribute("displayName", this.Name);
			this.displayGlyph = xCur.GetAttribute("displayGlyph", 57807);
			this.isHidden = xCur.GetAttribute("isHidden", false);
			this.Priority = priority = xCur.GetAttribute("priority", priority + 1);

			// compile condition
			condition = environment.CreateChunk(xCur.Element(xnCondition), true);
			// compile action
			code =  environment.CreateChunk(xCur.Element(xnCode), true);
		} // ctor

		public bool CheckCondition(LuaTable context)
			=> condition == null ? true : (bool)Environment.RunScriptWithReturn<bool>(condition, context, false);

		public LuaResult Execute(LuaTable context)
		{
			try
			{
				return new LuaResult(true, Environment.RunScript(code, context, true));
			}
			catch (Exception e)
			{
				Environment.ShowException(ExceptionShowFlags.None, e);
				return new LuaResult(false);
			}
		} // func Execute

		public string DisplayName => displayName;
		public string DisplayGlyph => char.ConvertFromUtf32(displayGlyph);
		public int Priority { get; }
		public bool IsHidden => isHidden;
	} // class PpsMainActionDefinition

  #endregion

  ///////////////////////////////////////////////////////////////////////////////
  /// <summary></summary>
  public partial class PpsMainEnvironment : PpsEnvironment
	{
		private readonly App app;
		private readonly PpsEnvironmentCollection<PpsMainActionDefinition> actions;
		private readonly PpsEnvironmentCollection<PpsMainViewDefinition> views;

		private readonly PpsProgressStack backgroundProgress;
		private readonly PpsProgressStack forgroundProgress;

		public PpsMainEnvironment(PpsEnvironmentInfo info, NetworkCredential userInfo, App app)
			: base(info, userInfo, app.Resources)
		{
			this.app = app;

			this.actions = new PpsEnvironmentCollection<PpsMainActionDefinition>(this);
			this.views = new PpsEnvironmentCollection<PpsMainViewDefinition>(this);

			this.backgroundProgress = new PpsProgressStack(app.Dispatcher);
			this.forgroundProgress = new PpsProgressStack(app.Dispatcher);
		} // ctor

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
						var removeList = GetRemoveObjectInfo();
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

		internal Task<bool> ShutdownAsync()
		{
			Dispose();
			return Task.FromResult<bool>(true);
		} // func ShutdownAsync

		[LuaMember(nameof(TestBackgroundProgressState))]
		public IPpsProgress TestBackgroundProgressState()
			=> BackgroundProgressState.CreateProgress();

		[LuaMember(nameof(TestForegroundProgressState))]
		public IPpsProgress TestForegroundProgressState()
			=> ForegroundProgressState.CreateProgress();

		[LuaMember(nameof(Actions))]
		public PpsEnvironmentCollection<PpsMainActionDefinition> Actions => actions;
		[LuaMember(nameof(Views))]
		public PpsEnvironmentCollection<PpsMainViewDefinition> Views => views;

		[LuaMember(nameof(BackgroundProgressState))]
		public PpsProgressStack BackgroundProgressState => backgroundProgress;
		[LuaMember(nameof(ForegroundProgressState))]
		public PpsProgressStack ForegroundProgressState => forgroundProgress;
	} // class PpsMainEnvironment
}

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
		private readonly LuaChunk condition;
		private readonly LuaChunk code;

		internal PpsMainActionDefinition(PpsMainEnvironment environment, XElement xCur, ref int priority)
			: base(environment, xCur.GetAttribute("name", String.Empty))
		{
			this.displayName = xCur.GetAttribute("displayName", this.Name);
			this.displayGlyph = xCur.GetAttribute("displayGlyph", 57807);
			this.Priority = priority = xCur.GetAttribute("priority", priority + 1);

			condition = environment.CreateLuaChunk(xCur.Element(xnCondition)); // , new KeyValuePair<string, Type>("contextMenu", typeof(bool))
			code = environment.CreateLuaChunk(xCur.Element(xnCode));
		} // ctor

		public bool CheckCondition(LuaTable environment, bool contextMenu)
		{
			if (condition != null)
				return (bool)Lua.RtConvertValue(condition.Run(environment), typeof(bool));
			else
				return true;
		}// func CheckCondition

		public void Execute(LuaTable environment)
		{
			if (code != null)
				Environment.RunScript(code, environment, false);
		} // proc Execute

		public string DisplayName => displayName;
		public string DisplayGlyph => char.ConvertFromUtf32(displayGlyph);
		public int Priority { get; }
	} // class PpsMainActionDefinition

  #endregion

  ///////////////////////////////////////////////////////////////////////////////
  /// <summary></summary>
  public partial class PpsMainEnvironment : PpsEnvironment
	{
		private readonly App app;
		private PpsEnvironmentCollection<PpsMainActionDefinition> actions;
		private PpsEnvironmentCollection<PpsMainViewDefinition> views;

		public PpsMainEnvironment(PpsEnvironmentInfo info, App app)
			: base(info, app.Resources)
		{
			this.app = app;

			this.actions = new PpsEnvironmentCollection<PpsMainActionDefinition>(this);
			this.views = new PpsEnvironmentCollection<PpsMainViewDefinition>(this);
		} // ctor

		protected override bool ShowLoginDialog(PpsClientLogin clientLogin)
		{
			return app.Dispatcher.Invoke(
				() =>
				{
					var wih = new System.Windows.Interop.WindowInteropHelper(app.MainWindow);
					return clientLogin.ShowWindowsLogin(wih.EnsureHandle());
				});
		} // func ShowLoginDialog

		public async override Task RefreshAsync()
		{
			await base.RefreshAsync();
			if (IsOnline && IsAuthentificated)
				await Task.Run(new Action(UpdateDocumentStore));

			await RefreshNavigatorAsync();
		} // proc RefreshAsync

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
				});
		} // proc CreateMainWindow

		public LuaChunk CreateLuaChunk(string line, params KeyValuePair<string, Type>[] args)
		{
			return Lua.CompileChunk(line, "line.lua", null, args);
		} // func CreateLuaChunk

		public LuaChunk CreateLuaChunk(XElement xCode, params KeyValuePair<string, Type>[] args)
		{
			var code = xCode?.Value;
			return code != null ? Lua.CompileChunk(code, "dummy", null, args) : null;
		} // proc CreateLuaChunk

		private async Task RefreshNavigatorAsync()
		{
			// clear the views
			views.Clear();
			actions.Clear();

			try
			{
				// get the views from storage
				var xNavigator = await Request.GetXmlAsync("wpf/navigator.xml", rootName: "navigator");

				// append the new views
				foreach (var cur in xNavigator.Elements(PpsMainViewDefinition.xnView))
					views.AppendItem(new PpsMainViewDefinition(this, cur));

				// append the new actions
				var priority = 0;
				foreach (var cur in xNavigator.Elements(PpsMainActionDefinition.xnAction))
					actions.AppendItem(new PpsMainActionDefinition(this, cur, ref priority));

				// update document info
				var updateList = new List<string>();
				foreach (var cur in xNavigator.Elements(XName.Get("document")))
					UpdateDocumentDefinitionInfo(cur, updateList);
				ClearDocumentDefinitionInfo(updateList);
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
				var w = c as PpsMainWindow;
				if (w != null && w.WindowIndex == index)
					return w;
			}
			return null;
		} // func GetWindow

		public IEnumerable<PpsMainWindow> GetWindows()
		{
			foreach (var c in Application.Current.Windows)
			{
				var w = c as PpsMainWindow;
				if (w != null)
					yield return w;
			}
		} // func GetWindows

		[LuaMember(nameof(Actions))]
		public PpsEnvironmentCollection<PpsMainActionDefinition> Actions => actions;
		[LuaMember(nameof(Views))]
		public PpsEnvironmentCollection<PpsMainViewDefinition> Views => views;
	} // class PpsMainEnvironment
}

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
using TecWare.DES.Networking;
using TecWare.DES.Stuff;
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
		public static readonly XName xnAction="action";
		public static readonly XName xnCondition = "condition";
		public static readonly XName xnCode = "code";

		private readonly string displayName;
		private readonly LuaChunk condition;
		private readonly LuaChunk code;

		internal PpsMainActionDefinition(PpsMainEnvironment environment, PpsEnvironmentDefinitionSource source, XElement xCur, ref int priority)
			: base(environment, source, xCur.GetAttribute("name", String.Empty))
		{
			this.displayName = xCur.GetAttribute("displayname", this.Name);
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
				code.Run(environment);
		} // proc Execute
				
    public string DisplayName => displayName;
		public int Priority { get; }
	} // class PpsMainActionDefinition

  #endregion

  ///////////////////////////////////////////////////////////////////////////////
  /// <summary></summary>
  public class PpsMainEnvironment : PpsEnvironment
	{
		#region -- class PpsMainLocalStore ------------------------------------------------

		///////////////////////////////////////////////////////////////////////////////
		/// <summary></summary>
		private sealed class PpsMainLocalStore : PpsLocalDataStore
		{
			public PpsMainLocalStore(PpsEnvironment environment) 
				: base(environment)
			{
			} // ctor
			
			protected override void GetResponseDataStream(PpsStoreResponse r)
			{
				var actionName = r.Request.Arguments.Get("action");
				if (r.Request.Path == "/" && String.Compare(actionName, "getviews", StringComparison.OrdinalIgnoreCase) == 0) // get all local views
				{
					r.SetResponseData(CollectLocalViews(), MimeTypes.Xml);
				}
				else if (r.Request.Path == "/" && String.Compare(actionName, "getactions", StringComparison.OrdinalIgnoreCase) == 0) // get all local actions
				{
					r.SetResponseData(CollectLocalActions(), MimeTypes.Xml);
				}
				else
					base.GetResponseDataStream(r);
			} // func GetResponseDataStream
			
			private Stream CollectLocalViews()
			{
				return new FileStream(Path.GetFullPath(@"..\..\..\PPSnDesktop\Local\Views.xml"), FileMode.Open);
			} // func CollectLocalView

			private Stream CollectLocalActions()
			{
				return new FileStream(Path.GetFullPath(@"..\..\..\PPSnDesktop\Local\Actions.xml"), FileMode.Open);
			} // func CollectLocalActions
		} // class PpsMainLocalStore

		#endregion

		private PpsEnvironmentCollection<PpsMainActionDefinition> actions;
		private PpsEnvironmentCollection<PpsMainViewDefinition> views;

		public PpsMainEnvironment(Uri remoteUri, ResourceDictionary mainResources)
			: base(remoteUri, mainResources)
		{
			this.actions = new PpsEnvironmentCollection<PpsMainActionDefinition>(this);
			this.views = new PpsEnvironmentCollection<PpsMainViewDefinition>(this);
		} // ctor

		protected override PpsLocalDataStore CreateLocalDataStore() => new PpsMainLocalStore(this);

		public async override Task RefreshAsync()
		{
			await base.RefreshAsync();

			await RefreshViewsAsync(PpsEnvironmentDefinitionSource.Offline);
			if (IsOnline)
				await RefreshViewsAsync(PpsEnvironmentDefinitionSource.Online);
		} // proc RefreshAsync

		public void CreateMainWindow()
		{
			Dispatcher.Invoke(() =>
				{
					// find a free window index
					var freeIndex = 0;
					while (GetWindow(freeIndex) != null)
						freeIndex++;

					var window = new PpsMainWindow(freeIndex);
					window.Show();
				});
		} // proc CreateMainWindow

		public LuaChunk CreateLuaChunk(XElement xCode, params KeyValuePair<string, Type>[] args)
		{
			var code = xCode?.Value;
			return code != null ? Lua.CompileChunk(code, "dummy", null, args) : null;
		} // proc CreateLuaChunk

		private async Task RefreshViewsAsync(PpsEnvironmentDefinitionSource source)
		{
			// Lade die Views
			var xViews = await this.Web[source].GetXmlAsync("?action=getviews", rootName: PpsMainViewDefinition.xnViews);
			var xActions = await this.Web[source].GetXmlAsync("?action=getactions", rootName: PpsMainActionDefinition.xnActions);

			// Remove all views, actions
			views.Clear((PpsEnvironmentClearFlags)source);
			actions.Clear((PpsEnvironmentClearFlags)source);
			
			foreach (var cur in xViews.Elements(PpsMainViewDefinition.xnView))
				views.AppendItem(new PpsMainViewDefinition(this, source, cur));

			var priority = 0;
			foreach (var cur in xActions.Elements(PpsMainActionDefinition.xnAction))
				actions.AppendItem(new PpsMainActionDefinition(this, source, cur, ref priority));
		} // proc RefreshViewsAsync
		

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

		[LuaMember(nameof(Actions))]
		public PpsEnvironmentCollection<PpsMainActionDefinition> Actions => actions;
		[LuaMember(nameof(Views))]
		public PpsEnvironmentCollection<PpsMainViewDefinition> Views => views;
	} // class PpsMainEnvironment
}

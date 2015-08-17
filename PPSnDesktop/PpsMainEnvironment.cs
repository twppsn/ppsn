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
    private readonly string displayName;
    private readonly ICommand command;

		internal PpsMainActionDefinition(PpsEnvironment environment, PpsEnvironmentDefinitionSource source, string name, string displayName, ICommand command)
			: base(environment, source, name)
		{
      this.displayName = displayName;
      this.command = command;
		} // ctor

    public string DisplayName => displayName;
    public ICommand Command => command;
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
				else
					base.GetResponseDataStream(r);
			} // func GetResponseDataStream
			
			private Stream CollectLocalViews()
			{
				return new FileStream(Path.GetFullPath(@"..\..\Local\Views.xml"), FileMode.Open);
			} // func CollectLocalView
		} // class PpsMainLocalStore

		#endregion

		private PpsEnvironmentCollection<PpsMainActionDefinition> actions;
		private PpsEnvironmentCollection<PpsMainViewDefinition> views;

		public PpsMainEnvironment(Uri remoteUri, ResourceDictionary mainResources)
			: base(remoteUri, mainResources)
		{
			this.actions = new PpsEnvironmentCollection<PpsMainActionDefinition>(this);
			this.views = new PpsEnvironmentCollection<PpsMainViewDefinition>(this);

      // test
      actions.AppendItem(new PpsMainActionDefinition(this, PpsEnvironmentDefinitionSource.Offline, "Test1", "Test 1", null));
      actions.AppendItem(new PpsMainActionDefinition(this, PpsEnvironmentDefinitionSource.Offline, "Test2", "Test 2", null));
      actions.AppendItem(new PpsMainActionDefinition(this, PpsEnvironmentDefinitionSource.Online, "Test1", "Test 2", null));
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

		private async Task RefreshViewsAsync(PpsEnvironmentDefinitionSource source)
		{
			// Lade die Views
			var xViews = await this.Web[source].GetXmlAsync("?action=getviews", rootName: PpsMainViewDefinition.xnViews);

			// Remove all views
			views.Clear((PpsEnvironmentClearFlags)source);

			foreach (var cur in xViews.Elements(PpsMainViewDefinition.xnView))
				views.AppendItem(new PpsMainViewDefinition(this, source, cur));
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

		public PpsEnvironmentCollection<PpsMainActionDefinition> Actions => actions;
		public PpsEnvironmentCollection<PpsMainViewDefinition> Views => views;
	} // class PpsMainEnvironment
}

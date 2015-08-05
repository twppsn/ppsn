using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
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
		private PpsEnvironmentCollection<PpsMainActionDefinition> actions;

		public PpsMainEnvironment(Uri remoteUri, ResourceDictionary mainResources)
			: base(remoteUri, mainResources)
		{
			this.actions = new PpsEnvironmentCollection<PpsMainActionDefinition>(this);

      // test
      actions.AppendItem(new PpsMainActionDefinition(this, PpsEnvironmentDefinitionSource.Offline, "Test1", "Test 1", null));
      actions.AppendItem(new PpsMainActionDefinition(this, PpsEnvironmentDefinitionSource.Offline, "Test2", "Test 2", null));
      actions.AppendItem(new PpsMainActionDefinition(this, PpsEnvironmentDefinitionSource.Online, "Test1", "Test 2", null));
    } // ctor

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

		public PpsEnvironmentCollection<PpsMainActionDefinition> Actions
    { get { return actions; } }
	} // class PpsMainEnvironment
}

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

			views.AppendItem(new PpsMainViewDefinition(this, PpsEnvironmentDefinitionSource.Offline, "TE", "Teilestamm",
				new PpsMainViewFilter[] 
				{
					new PpsMainViewFilter("Aktiv"),
					new PpsMainViewFilter("InAktiv")
				},
				new PpsMainViewSort[]
				{
					new PpsMainViewSort("Teilenummer"),
					new PpsMainViewSort("Teilname"),
					new PpsMainViewSort("Teilmatch")
				}));

			views.AppendItem(new PpsMainViewDefinition(this, PpsEnvironmentDefinitionSource.Offline, "CO", "Kontakte",
				new PpsMainViewFilter[]
				{
					new PpsMainViewFilter("Lieferanten"),
					new PpsMainViewFilter("Kunden"),
					new PpsMainViewFilter("Speditionen"),
					new PpsMainViewFilter("Interessenten")
				},
				new PpsMainViewSort[]
				{
					new PpsMainViewSort("Kundennummer"),
					new PpsMainViewSort("Kundenname"),
					new PpsMainViewSort("Debitorennummer"),
					new PpsMainViewSort("Kreditorennummer"),
					new PpsMainViewSort("Kundenmatch")
				}));

			views.AppendItem(new PpsMainViewDefinition(this, PpsEnvironmentDefinitionSource.Offline, "BE", "Bestellungen",
				new PpsMainViewFilter[]
				{
					new PpsMainViewFilter("Aktiv"),
					new PpsMainViewFilter("Archiv")
				},
				new PpsMainViewSort[]
				{
					new PpsMainViewSort("Lieferantennummer"),
					new PpsMainViewSort("Lieferantenname")
				}));
			views.AppendItem(new PpsMainViewDefinition(this, PpsEnvironmentDefinitionSource.Offline, "AU", "Aufträge",
				new PpsMainViewFilter[]
				{
					new PpsMainViewFilter("Aktiv"),
					new PpsMainViewFilter("Archiv")
				},
				new PpsMainViewSort[]
				{
					new PpsMainViewSort("Kundennummer"),
					new PpsMainViewSort("Kundenname")
				}));
			views.AppendItem(new PpsMainViewDefinition(this, PpsEnvironmentDefinitionSource.Offline, "FE", "Fertigungsaufträge",
				new PpsMainViewFilter[]
				{
					new PpsMainViewFilter("Aktiv"),
					new PpsMainViewFilter("Archiv")
				},
				new PpsMainViewSort[]
				{
					new PpsMainViewSort("Kundennummer"),
					new PpsMainViewSort("Kundenname")
				}));
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

		public PpsEnvironmentCollection<PpsMainActionDefinition> Actions => actions;
		public PpsEnvironmentCollection<PpsMainViewDefinition> Views => views;
	} // class PpsMainEnvironment
}

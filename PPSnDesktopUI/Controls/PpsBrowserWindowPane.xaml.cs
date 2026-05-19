using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using Neo.IronLua;
using TecWare.PPSn.Core.Data;
using TecWare.PPSn.Data;
using TecWare.PPSn.UI;

namespace TecWare.PPSn.Controls
{
	/// <summary>
	/// Interaction logic for PpsBrowserWindowPane.xaml
	/// </summary>
	public partial class PpsBrowserWindowPane : PpsWindowPaneControl
	{
		private IEnumerable<PpsBrowserTabInfo> tabInfos;

		#region --- Ctor/Dtor ---------------------------------------------------------
		public PpsBrowserWindowPane(IPpsWindowPaneHost paneHost, IEnumerable<PpsBrowserTabInfo> tabInfos)
			: base(paneHost)
		{
			this.tabInfos = tabInfos;
			InitializeComponent();
		} // ctor
		#endregion

		#region --- Browser Creation --------------------------------------------------

		private void CreateTabs()
		{
			foreach(var info in tabInfos)
			{
				var tabItem = CreateTabItem(info);
				browserTabs.Items.Add(tabItem);
			}
		} // proc CreateTabs

		private PpsTabItem CreateTabItem(PpsBrowserTabInfo info) 
		{
			//1. LIST abfragen

			//2. TabItem erstellen
			var tabItem = new PpsTabItem
			{
				//3. TabItemHeader erstellen
				Header = info.ListId.ToString() // TODO Set this more accurate
			};

			//4. Browser erstellen
			var listBox = CreateBrowser("", info.MaskType);

			// Sets the values of the attached properties on the listBox-Element
			Grid.SetRow(listBox, 0);
			Grid.SetColumn(listBox, 0);

			// Grid to organize our browser around
			var grid = new Grid
			{
				Margin = new Thickness(4, 4, 4, 4)
			};

			RowDefinition first = new RowDefinition
			{
				Height = new GridLength(3, GridUnitType.Star)
			};

			grid.RowDefinitions.Add(first);
			grid.Children.Add(listBox);

			if (info.TabType == PpsBrowserTabType.Warenkorb)
			{
				// Warenkorb erzeugen
			}
			
			tabItem.Content = grid;
			return tabItem;
		} // proc CreateTabItem

		private PpsDataListBox CreateBrowser(string listStmt, Type maskType)
		{
			var listBox = new PpsDataListBox();
			var columns = new PpsListColumns();

			//1. ListStmt ausführen 
			var t = new LuaTable();
			listBox.DataContext = t;

			foreach(var kv in t)
			{
				//2. Für jede Spalte eine PpsListColumn anlegen 
				var column = CreateListColumn((string)kv.Key);
				columns.AddChild(column);
			}

			// Set Our Columns as the listbox' columns
			PpsListColumns.SetColumns(listBox, columns);
			
			//3. OnClick-Handler zum öffnen der Maske erstellen - 
			return listBox;
		} // proc CreateBrowser
		#endregion

		private PpsListColumn CreateListColumn(string key)
		{
			var column = new PpsListColumn
			{
				Header = key
			};

			var binding = new Binding(key)
			{
				Mode = BindingMode.OneWay
			};

			column.DisplayMemberBinding = binding;

			return column;
		} // proc CreateListColumn

		#region --- Properties --------------------------------------------------------
		#endregion
	} // class PpsBrowserWindowPane

	#region --- class PpsBrowserTabInfo -----------------------------------------------
	/// <summary>Information about the browser tab to be created.</summary>
	public sealed class PpsBrowserTabInfo
	{
		private readonly int listId;
		private readonly PpsBrowserTabType tabType;
		private readonly Type maskType;
		private readonly int maskParameterColumnIndex; // Which Column in the luaTable holds the id parameter for our mask? 

		public PpsBrowserTabInfo(int listId, bool isComplexTab, string maskType, int paramColumnIndex)
		{
			this.listId = listId;
			tabType = isComplexTab ? PpsBrowserTabType.Warenkorb : PpsBrowserTabType.Einfach;

			var type = Type.GetType(maskType) ?? throw new TypeLoadException("Could not find Type: '" + maskType + "'.");
			this.maskType = type;

			maskParameterColumnIndex = paramColumnIndex;
		} // ctor

		public int ListId => listId;

		public PpsBrowserTabType TabType => tabType;

		public Type MaskType => maskType;

		public int MaskParameterColumnIndex => maskParameterColumnIndex;

	} // class PpsBrowserTabInfo
	#endregion

	#region --- enum PpsBrowserTabType ------------------------------------------------

	/// <summary>Type of the BrowserTab.</summary>
	public enum PpsBrowserTabType 
	{
		/// <summary>Tab with single DataListBox</summary>
		Einfach,
		/// <summary>Tab with additional secondary DataListBox</summary>
		Warenkorb
	} // enum PpsBrowserTabType

	#endregion
}

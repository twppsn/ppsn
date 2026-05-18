using System;
using System.Collections.Generic;
using System.Windows;
using Neo.IronLua;
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
				
			}
		} // proc CreateTabs

		private PpsTabItem CreateTabItem(PpsBrowserTabInfo info) 
		{
			var tabItem = new PpsTabItem();
			//1. LIST abfragen
			//2. TabItem erstellen
			//3. TabItemHeader erstellen
			//4. Browser erstellen
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
			var column = new PpsListColumn();
			var header = new PpsListColumnHeader();
			var template = new DataTemplate(typeof(PpsListColumn));

			//1. key decodieren 
			//2. Darstellungsnamen abfragen 

			column.Header = header;
			column.CellTemplate = template;

			return column;
		} // proc CreateListColumn

		#region --- Properties --------------------------------------------------------
		#endregion
	} // class PpsBrowserWindowPane

	#region --- PpsBrowserTabInfo -----------------------------------------------------
	public sealed class PpsBrowserTabInfo
	{
		private readonly int listId;
		private readonly Type maskType;
		private readonly int maskParameterColumnIndex; // Which Column in the luaTable holds the id parameter for our mask? 

		public PpsBrowserTabInfo(int listId, string maskType, int paramColumnIndex)
		{
			this.listId = listId;

			var type = Type.GetType(maskType) ?? throw new TypeLoadException("Could not find Type: '" + maskType + "'.");
			this.maskType = type;

			this.maskParameterColumnIndex = paramColumnIndex;
		} // ctor

		public int ListId => listId;
		public Type MaskType => maskType;
		public int MaskParameterColumnIndex => maskParameterColumnIndex;
	} // class PpsBrowserTabInfo
	#endregion
}

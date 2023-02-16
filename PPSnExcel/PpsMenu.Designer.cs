namespace PPSnExcel
{
	partial class PpsMenu : Microsoft.Office.Tools.Ribbon.RibbonBase
	{
		/// <summary>
		/// Erforderliche Designervariable.
		/// </summary>
		private System.ComponentModel.IContainer components = null;

		public PpsMenu()
				: base(Globals.Factory.GetRibbonFactory())
		{
			InitializeComponent();
		}

		/// <summary> 
		/// Verwendete Ressourcen bereinigen.
		/// </summary>
		/// <param name="disposing">True, wenn verwaltete Ressourcen gelöscht werden sollen; andernfalls False.</param>
		protected override void Dispose(bool disposing)
		{
			if (disposing && (components != null))
			{
				components.Dispose();
			}
			base.Dispose(disposing);
		}

		#region Vom Komponenten-Designer generierter Code

		/// <summary>
		/// Erforderliche Methode für Designerunterstützung -
		/// Der Inhalt der Methode darf nicht mit dem Code-Editor geändert werden.
		/// </summary>
		private void InitializeComponent()
		{
			this.tabPPSn = this.Factory.CreateRibbonTab();
			this.groupData = this.Factory.CreateRibbonGroup();
			this.separator1 = this.Factory.CreateRibbonSeparator();
			this.groupConnection = this.Factory.CreateRibbonGroup();
			this.groupCommands = this.Factory.CreateRibbonGroup();
			this.cmdReport = this.Factory.CreateRibbonButton();
			this.cmdTable = this.Factory.CreateRibbonButton();
			this.cmdRefreshAll = this.Factory.CreateRibbonSplitButton();
			this.cmdRefreshLayout = this.Factory.CreateRibbonButton();
			this.separator3 = this.Factory.CreateRibbonSeparator();
			this.cmdRefresh = this.Factory.CreateRibbonButton();
			this.button1 = this.Factory.CreateRibbonButton();
			this.loginMenu = this.Factory.CreateRibbonMenu();
			this.loginGalery = this.Factory.CreateRibbonGallery();
			this.separator2 = this.Factory.CreateRibbonSeparator();
			this.logoutButton = this.Factory.CreateRibbonButton();
			this.cmdExtended = this.Factory.CreateRibbonMenu();
			this.editTableExCommand = this.Factory.CreateRibbonButton();
			this.removeTableSourceData = this.Factory.CreateRibbonButton();
			this.dataAnonymisiern = this.Factory.CreateRibbonButton();
			this.cmdListObjectInfo = this.Factory.CreateRibbonButton();
			this.separator4 = this.Factory.CreateRibbonSeparator();
			this.cmdStyles = this.Factory.CreateRibbonButton();
			this.newItemButton = this.Factory.CreateRibbonButton();
			this.tabPPSn.SuspendLayout();
			this.groupData.SuspendLayout();
			this.groupConnection.SuspendLayout();
			this.SuspendLayout();
			// 
			// tabPPSn
			// 
			this.tabPPSn.ControlId.ControlIdType = Microsoft.Office.Tools.Ribbon.RibbonControlIdType.Office;
			this.tabPPSn.Groups.Add(this.groupData);
			this.tabPPSn.Groups.Add(this.groupConnection);
			this.tabPPSn.Groups.Add(this.groupCommands);
			this.tabPPSn.KeyTip = "N";
			this.tabPPSn.Label = "PPSn";
			this.tabPPSn.Name = "tabPPSn";
			// 
			// groupData
			// 
			this.groupData.Items.Add(this.cmdReport);
			this.groupData.Items.Add(this.cmdTable);
			this.groupData.Items.Add(this.separator1);
			this.groupData.Items.Add(this.cmdRefreshAll);
			this.groupData.Items.Add(this.button1);
			this.groupData.Label = "Auswertungen";
			this.groupData.Name = "groupData";
			// 
			// separator1
			// 
			this.separator1.Name = "separator1";
			// 
			// groupConnection
			// 
			this.groupConnection.Items.Add(this.loginMenu);
			this.groupConnection.Items.Add(this.cmdExtended);
			this.groupConnection.Label = "Verbindung";
			this.groupConnection.Name = "groupConnection";
			// 
			// groupCommands
			// 
			this.groupCommands.Label = "Befehle";
			this.groupCommands.Name = "groupCommands";
			this.groupCommands.Visible = false;
			// 
			// cmdReport
			// 
			this.cmdReport.ControlSize = Microsoft.Office.Core.RibbonControlSize.RibbonControlSizeLarge;
			this.cmdReport.Enabled = false;
			this.cmdReport.KeyTip = "R";
			this.cmdReport.Label = "Report";
			this.cmdReport.Name = "cmdReport";
			this.cmdReport.OfficeImageId = "PropertySheet";
			this.cmdReport.ScreenTip = "Report öffnen";
			this.cmdReport.ShowImage = true;
			this.cmdReport.SuperTip = "Öffnet einen vorgefertigten Report";
			this.cmdReport.Click += new Microsoft.Office.Tools.Ribbon.RibbonControlEventHandler(this.cmdReport_Click);
			// 
			// cmdTable
			// 
			this.cmdTable.ControlSize = Microsoft.Office.Core.RibbonControlSize.RibbonControlSizeLarge;
			this.cmdTable.Enabled = false;
			this.cmdTable.KeyTip = "T";
			this.cmdTable.Label = "Tabelle";
			this.cmdTable.Name = "cmdTable";
			this.cmdTable.OfficeImageId = "TableInsert";
			this.cmdTable.ScreenTip = "Datentabelle verknüpfen";
			this.cmdTable.ShowImage = true;
			this.cmdTable.SuperTip = "Verknüpft eine Datentabelle mit dem aktuellen Arbeitsblatt";
			this.cmdTable.Click += new Microsoft.Office.Tools.Ribbon.RibbonControlEventHandler(this.cmdTable_Click);
			// 
			// cmdRefreshAll
			// 
			this.cmdRefreshAll.ControlSize = Microsoft.Office.Core.RibbonControlSize.RibbonControlSizeLarge;
			this.cmdRefreshAll.Items.Add(this.cmdRefreshLayout);
			this.cmdRefreshAll.Items.Add(this.separator3);
			this.cmdRefreshAll.Items.Add(this.cmdRefresh);
			this.cmdRefreshAll.Label = "Aktualisieren";
			this.cmdRefreshAll.Name = "cmdRefreshAll";
			this.cmdRefreshAll.OfficeImageId = "RefreshAll";
			this.cmdRefreshAll.ScreenTip = "Alles aktualisieren";
			this.cmdRefreshAll.SuperTip = "Aktualisiert die komplette Arbeitsmappe";
			this.cmdRefreshAll.Click += new Microsoft.Office.Tools.Ribbon.RibbonControlEventHandler(this.cmdRefreshAll_Click);
			// 
			// cmdRefreshLayout
			// 
			this.cmdRefreshLayout.Label = "Tabelle zurücksetzen";
			this.cmdRefreshLayout.Name = "cmdRefreshLayout";
			this.cmdRefreshLayout.ShowImage = true;
			this.cmdRefreshLayout.SuperTip = "Aktualisiert von der gewählten Liste die Spalten und das Layout";
			this.cmdRefreshLayout.Click += new Microsoft.Office.Tools.Ribbon.RibbonControlEventHandler(this.cmdRefreshLayout_Click);
			// 
			// separator3
			// 
			this.separator3.Name = "separator3";
			// 
			// cmdRefresh
			// 
			this.cmdRefresh.Label = "Daten aktualisieren";
			this.cmdRefresh.Name = "cmdRefresh";
			this.cmdRefresh.OfficeImageId = "Refresh";
			this.cmdRefresh.ScreenTip = "Daten aktualisieren";
			this.cmdRefresh.ShowImage = true;
			this.cmdRefresh.SuperTip = "Aktualisiert die Daten der ausgewählte Tabelle";
			this.cmdRefresh.Click += new Microsoft.Office.Tools.Ribbon.RibbonControlEventHandler(this.cmdRefresh_Click);
			// 
			// button1
			// 
			this.button1.Label = "Als Report speichern";
			this.button1.Name = "button1";
			this.button1.ShowImage = true;
			this.button1.Visible = false;
			// 
			// loginMenu
			// 
			this.loginMenu.Dynamic = true;
			this.loginMenu.Items.Add(this.loginGalery);
			this.loginMenu.Items.Add(this.separator2);
			this.loginMenu.Items.Add(this.newItemButton);
			this.loginMenu.Items.Add(this.logoutButton);
			this.loginMenu.Label = "menu1";
			this.loginMenu.Name = "loginMenu";
			// 
			// loginGalery
			// 
			this.loginGalery.Label = "Anmelden";
			this.loginGalery.Name = "loginGalery";
			this.loginGalery.ShowImage = true;
			this.loginGalery.Click += new Microsoft.Office.Tools.Ribbon.RibbonControlEventHandler(this.loginGalery_Click);
			this.loginGalery.ItemsLoading += new Microsoft.Office.Tools.Ribbon.RibbonControlEventHandler(this.loginGalery_ItemsLoading);
			// 
			// separator2
			// 
			this.separator2.Name = "separator2";
			// 
			// logoutButton
			// 
			this.logoutButton.Description = "Meldet den Nutzer vom aktuellen Environment ab.";
			this.logoutButton.Label = "Abmelden";
			this.logoutButton.Name = "logoutButton";
			this.logoutButton.ShowImage = true;
			this.logoutButton.Click += new Microsoft.Office.Tools.Ribbon.RibbonControlEventHandler(this.logoutButton_Click);
			// 
			// cmdExtended
			// 
			this.cmdExtended.Items.Add(this.editTableExCommand);
			this.cmdExtended.Items.Add(this.removeTableSourceData);
			this.cmdExtended.Items.Add(this.dataAnonymisiern);
			this.cmdExtended.Items.Add(this.cmdListObjectInfo);
			this.cmdExtended.Items.Add(this.separator4);
			this.cmdExtended.Items.Add(this.cmdStyles);
			this.cmdExtended.Label = "Erweitert";
			this.cmdExtended.Name = "cmdExtended";
			this.cmdExtended.OfficeImageId = "PropertySheet";
			this.cmdExtended.ShowImage = true;
			this.cmdExtended.SuperTip = "Erweiterte Menüpunkte";
			// 
			// editTableExCommand
			// 
			this.editTableExCommand.Label = "Tabelle bearbeiten...";
			this.editTableExCommand.Name = "editTableExCommand";
			this.editTableExCommand.ShowImage = true;
			this.editTableExCommand.Click += new Microsoft.Office.Tools.Ribbon.RibbonControlEventHandler(this.editTableExCommand_Click);
			// 
			// removeTableSourceData
			// 
			this.removeTableSourceData.Label = "Tabellendaten löschen";
			this.removeTableSourceData.Name = "removeTableSourceData";
			this.removeTableSourceData.ShowImage = true;
			this.removeTableSourceData.Click += new Microsoft.Office.Tools.Ribbon.RibbonControlEventHandler(this.removeTableSourceData_Click);
			// 
			// dataAnonymisiern
			// 
			this.dataAnonymisiern.Label = "Tabellendaten anonymisiern";
			this.dataAnonymisiern.Name = "dataAnonymisiern";
			this.dataAnonymisiern.ShowImage = true;
			this.dataAnonymisiern.Click += new Microsoft.Office.Tools.Ribbon.RibbonControlEventHandler(this.dataAnonymisiern_Click);
			// 
			// cmdListObjectInfo
			// 
			this.cmdListObjectInfo.Label = "Query anzeigen...";
			this.cmdListObjectInfo.Name = "cmdListObjectInfo";
			this.cmdListObjectInfo.ShowImage = true;
			this.cmdListObjectInfo.Click += new Microsoft.Office.Tools.Ribbon.RibbonControlEventHandler(this.cmdListObjectInfo_Click);
			// 
			// separator4
			// 
			this.separator4.Name = "separator4";
			// 
			// cmdStyles
			// 
			this.cmdStyles.Label = "Eigene Formatvorlagen...";
			this.cmdStyles.Name = "cmdStyles";
			this.cmdStyles.ScreenTip = "Formatvorlagen exportieren, importieren";
			this.cmdStyles.ShowImage = true;
			this.cmdStyles.SuperTip = "Ermöglicht das verwalten von Formatvorlagen.";
			this.cmdStyles.Visible = false;
			this.cmdStyles.Click += new Microsoft.Office.Tools.Ribbon.RibbonControlEventHandler(this.cmdStyles_Click);
			// 
			// newItemButton
			// 
			this.newItemButton.Description = "Neue Umgebung einrichten";
			this.newItemButton.Label = "Neue Umgebung...";
			this.newItemButton.Name = "newItemButton";
			this.newItemButton.ShowImage = true;
			this.newItemButton.Click += new Microsoft.Office.Tools.Ribbon.RibbonControlEventHandler(this.newItemButton_Click);
			// 
			// PpsMenu
			// 
			this.Name = "PpsMenu";
			this.RibbonType = "Microsoft.Excel.Workbook";
			this.Tabs.Add(this.tabPPSn);
			this.Load += new Microsoft.Office.Tools.Ribbon.RibbonUIEventHandler(this.PpsMenu_Load);
			this.tabPPSn.ResumeLayout(false);
			this.tabPPSn.PerformLayout();
			this.groupData.ResumeLayout(false);
			this.groupData.PerformLayout();
			this.groupConnection.ResumeLayout(false);
			this.groupConnection.PerformLayout();
			this.ResumeLayout(false);

		}

		#endregion
		private Microsoft.Office.Tools.Ribbon.RibbonTab tabPPSn;
		private Microsoft.Office.Tools.Ribbon.RibbonGroup groupData;
		private Microsoft.Office.Tools.Ribbon.RibbonButton cmdReport;
		internal Microsoft.Office.Tools.Ribbon.RibbonGroup groupConnection;
		internal Microsoft.Office.Tools.Ribbon.RibbonMenu cmdExtended;
		internal Microsoft.Office.Tools.Ribbon.RibbonButton cmdStyles;
		internal Microsoft.Office.Tools.Ribbon.RibbonButton cmdTable;
		internal Microsoft.Office.Tools.Ribbon.RibbonButton button1;
		internal Microsoft.Office.Tools.Ribbon.RibbonGallery loginGalery;
		internal Microsoft.Office.Tools.Ribbon.RibbonMenu loginMenu;
		internal Microsoft.Office.Tools.Ribbon.RibbonSeparator separator2;
		internal Microsoft.Office.Tools.Ribbon.RibbonButton logoutButton;
		internal Microsoft.Office.Tools.Ribbon.RibbonSplitButton cmdRefreshAll;
		internal Microsoft.Office.Tools.Ribbon.RibbonButton cmdRefreshLayout;
		internal Microsoft.Office.Tools.Ribbon.RibbonSeparator separator3;
		internal Microsoft.Office.Tools.Ribbon.RibbonButton cmdRefresh;
		internal Microsoft.Office.Tools.Ribbon.RibbonButton cmdListObjectInfo;
		internal Microsoft.Office.Tools.Ribbon.RibbonSeparator separator1;
		internal Microsoft.Office.Tools.Ribbon.RibbonGroup groupCommands;
		internal Microsoft.Office.Tools.Ribbon.RibbonButton editTableExCommand;
		internal Microsoft.Office.Tools.Ribbon.RibbonButton removeTableSourceData;
		internal Microsoft.Office.Tools.Ribbon.RibbonButton dataAnonymisiern;
		internal Microsoft.Office.Tools.Ribbon.RibbonSeparator separator4;
		internal Microsoft.Office.Tools.Ribbon.RibbonButton newItemButton;
	}

	partial class ThisRibbonCollection
	{
		internal PpsMenu PpsMenu
		{
			get { return this.GetRibbon<PpsMenu>(); }
		}
	}
}

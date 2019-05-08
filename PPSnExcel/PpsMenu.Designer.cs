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
			this.cmdReport = this.Factory.CreateRibbonButton();
			this.cmdTable = this.Factory.CreateRibbonButton();
			this.separator1 = this.Factory.CreateRibbonSeparator();
			this.cmdRefreshAll = this.Factory.CreateRibbonSplitButton();
			this.cmdRefreshLayout = this.Factory.CreateRibbonButton();
			this.separator3 = this.Factory.CreateRibbonSeparator();
			this.cmdRefresh = this.Factory.CreateRibbonButton();
			this.button1 = this.Factory.CreateRibbonButton();
			this.group1 = this.Factory.CreateRibbonGroup();
			this.loginMenu = this.Factory.CreateRibbonMenu();
			this.loginGalery = this.Factory.CreateRibbonGallery();
			this.separator2 = this.Factory.CreateRibbonSeparator();
			this.logoutButton = this.Factory.CreateRibbonButton();
			this.cmdExtended = this.Factory.CreateRibbonMenu();
			this.cmdStyles = this.Factory.CreateRibbonButton();
			this.cmdListObjectInfo = this.Factory.CreateRibbonButton();
			this.cmdOptions = this.Factory.CreateRibbonButton();
			this.tabPPSn.SuspendLayout();
			this.groupData.SuspendLayout();
			this.group1.SuspendLayout();
			this.SuspendLayout();
			// 
			// tabPPSn
			// 
			this.tabPPSn.ControlId.ControlIdType = Microsoft.Office.Tools.Ribbon.RibbonControlIdType.Office;
			this.tabPPSn.Groups.Add(this.groupData);
			this.tabPPSn.Groups.Add(this.group1);
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
			// separator1
			// 
			this.separator1.Name = "separator1";
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
			this.cmdRefreshLayout.Label = "Layout aktualisieren";
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
			this.cmdRefresh.Label = "Tabelle aktualisieren";
			this.cmdRefresh.Name = "cmdRefresh";
			this.cmdRefresh.OfficeImageId = "Refresh";
			this.cmdRefresh.ScreenTip = "Tabelle aktualisieren";
			this.cmdRefresh.ShowImage = true;
			this.cmdRefresh.SuperTip = "Aktualisiert die ausgewählte Tabelle";
			this.cmdRefresh.Click += new Microsoft.Office.Tools.Ribbon.RibbonControlEventHandler(this.cmdRefresh_Click);
			// 
			// button1
			// 
			this.button1.Label = "Als Report speichern";
			this.button1.Name = "button1";
			this.button1.ShowImage = true;
			this.button1.Visible = false;
			// 
			// group1
			// 
			this.group1.Items.Add(this.loginMenu);
			this.group1.Items.Add(this.cmdExtended);
			this.group1.Label = "Verbindung";
			this.group1.Name = "group1";
			// 
			// loginMenu
			// 
			this.loginMenu.Dynamic = true;
			this.loginMenu.Items.Add(this.loginGalery);
			this.loginMenu.Items.Add(this.separator2);
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
			this.cmdExtended.Items.Add(this.cmdStyles);
			this.cmdExtended.Items.Add(this.cmdListObjectInfo);
			this.cmdExtended.Items.Add(this.cmdOptions);
			this.cmdExtended.Label = "Erweitert";
			this.cmdExtended.Name = "cmdExtended";
			this.cmdExtended.OfficeImageId = "PropertySheet";
			this.cmdExtended.ShowImage = true;
			this.cmdExtended.SuperTip = "Erweiterte Menüpunkte";
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
			// cmdListObjectInfo
			// 
			this.cmdListObjectInfo.Label = "Xml-Quell-Daten anzeigen";
			this.cmdListObjectInfo.Name = "cmdListObjectInfo";
			this.cmdListObjectInfo.ShowImage = true;
			this.cmdListObjectInfo.Click += new Microsoft.Office.Tools.Ribbon.RibbonControlEventHandler(this.cmdListObjectInfo_Click);
			// 
			// cmdOptions
			// 
			this.cmdOptions.Label = "Optionen";
			this.cmdOptions.Name = "cmdOptions";
			this.cmdOptions.ShowImage = true;
			this.cmdOptions.Visible = false;
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
			this.group1.ResumeLayout(false);
			this.group1.PerformLayout();
			this.ResumeLayout(false);

		}

		#endregion
		private Microsoft.Office.Tools.Ribbon.RibbonTab tabPPSn;
		private Microsoft.Office.Tools.Ribbon.RibbonGroup groupData;
		private Microsoft.Office.Tools.Ribbon.RibbonButton cmdReport;
		internal Microsoft.Office.Tools.Ribbon.RibbonGroup group1;
		internal Microsoft.Office.Tools.Ribbon.RibbonButton cmdOptions;
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
	}

	partial class ThisRibbonCollection
	{
		internal PpsMenu PpsMenu
		{
			get { return this.GetRibbon<PpsMenu>(); }
		}
	}
}

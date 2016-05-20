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
			this.group2 = this.Factory.CreateRibbonGroup();
			this.cmdRefresh = this.Factory.CreateRibbonButton();
			this.cmdEditTable = this.Factory.CreateRibbonButton();
			this.button1 = this.Factory.CreateRibbonButton();
			this.group1 = this.Factory.CreateRibbonGroup();
			this.loginMenu = this.Factory.CreateRibbonMenu();
			this.loginGalery = this.Factory.CreateRibbonGallery();
			this.separator2 = this.Factory.CreateRibbonSeparator();
			this.loginButton = this.Factory.CreateRibbonButton();
			this.cmdExtended = this.Factory.CreateRibbonMenu();
			this.cmdStyles = this.Factory.CreateRibbonButton();
			this.separator1 = this.Factory.CreateRibbonSeparator();
			this.cmdOptions = this.Factory.CreateRibbonButton();
			this.tabPPSn.SuspendLayout();
			this.groupData.SuspendLayout();
			this.group2.SuspendLayout();
			this.group1.SuspendLayout();
			this.SuspendLayout();
			// 
			// tabPPSn
			// 
			this.tabPPSn.ControlId.ControlIdType = Microsoft.Office.Tools.Ribbon.RibbonControlIdType.Office;
			this.tabPPSn.Groups.Add(this.groupData);
			this.tabPPSn.Groups.Add(this.group2);
			this.tabPPSn.Groups.Add(this.group1);
			this.tabPPSn.KeyTip = "N";
			this.tabPPSn.Label = "PPSn";
			this.tabPPSn.Name = "tabPPSn";
			// 
			// groupData
			// 
			this.groupData.Items.Add(this.cmdReport);
			this.groupData.Items.Add(this.cmdTable);
			this.groupData.Label = "Importieren";
			this.groupData.Name = "groupData";
			// 
			// cmdReport
			// 
			this.cmdReport.ControlSize = Microsoft.Office.Core.RibbonControlSize.RibbonControlSizeLarge;
			this.cmdReport.Label = "Report";
			this.cmdReport.Name = "cmdReport";
			this.cmdReport.ScreenTip = "Report öffnen";
			this.cmdReport.ShowImage = true;
			this.cmdReport.SuperTip = "Öffnet einen vorgefertigten Report";
			this.cmdReport.Click += new Microsoft.Office.Tools.Ribbon.RibbonControlEventHandler(this.cmdReport_Click);
			// 
			// cmdTable
			// 
			this.cmdTable.ControlSize = Microsoft.Office.Core.RibbonControlSize.RibbonControlSizeLarge;
			this.cmdTable.KeyTip = "T";
			this.cmdTable.Label = "Tabelle";
			this.cmdTable.Name = "cmdTable";
			this.cmdTable.ScreenTip = "Datentabelle verknüpfen";
			this.cmdTable.ShowImage = true;
			this.cmdTable.SuperTip = "Verknüpft eine Datentabelle mit dem aktuellen Arbeitsblatt";
			this.cmdTable.Click += new Microsoft.Office.Tools.Ribbon.RibbonControlEventHandler(this.cmdTable_Click);
			// 
			// group2
			// 
			this.group2.Items.Add(this.cmdRefresh);
			this.group2.Items.Add(this.cmdEditTable);
			this.group2.Items.Add(this.button1);
			this.group2.Label = "Bearbeiten";
			this.group2.Name = "group2";
			// 
			// cmdRefresh
			// 
			this.cmdRefresh.ControlSize = Microsoft.Office.Core.RibbonControlSize.RibbonControlSizeLarge;
			this.cmdRefresh.Label = "Aktualisieren";
			this.cmdRefresh.Name = "cmdRefresh";
			this.cmdRefresh.ShowImage = true;
			this.cmdRefresh.Click += new Microsoft.Office.Tools.Ribbon.RibbonControlEventHandler(this.cmdRefresh_Click);
			// 
			// cmdEditTable
			// 
			this.cmdEditTable.Label = "Spalten bearbeiten";
			this.cmdEditTable.Name = "cmdEditTable";
			this.cmdEditTable.ShowImage = true;
			// 
			// button1
			// 
			this.button1.Label = "Als Report speichern";
			this.button1.Name = "button1";
			this.button1.ShowImage = true;
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
			this.loginMenu.Items.Add(this.loginGalery);
			this.loginMenu.Items.Add(this.separator2);
			this.loginMenu.Items.Add(this.loginButton);
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
			// loginButton
			// 
			this.loginButton.Label = "button2";
			this.loginButton.Name = "loginButton";
			this.loginButton.ShowImage = true;
			this.loginButton.Click += new Microsoft.Office.Tools.Ribbon.RibbonControlEventHandler(this.loginButton_Click);
			// 
			// cmdExtended
			// 
			this.cmdExtended.Items.Add(this.cmdStyles);
			this.cmdExtended.Items.Add(this.separator1);
			this.cmdExtended.Items.Add(this.cmdOptions);
			this.cmdExtended.Label = "Erweitert";
			this.cmdExtended.Name = "cmdExtended";
			this.cmdExtended.ShowImage = true;
			// 
			// cmdStyles
			// 
			this.cmdStyles.Label = "Eigene Formatvorlagen...";
			this.cmdStyles.Name = "cmdStyles";
			this.cmdStyles.ScreenTip = "Formatvorlagen exportieren, importieren";
			this.cmdStyles.ShowImage = true;
			this.cmdStyles.SuperTip = "Ermöglicht das verwalten von Formatvorlagen.";
			this.cmdStyles.Click += new Microsoft.Office.Tools.Ribbon.RibbonControlEventHandler(this.cmdStyles_Click);
			// 
			// separator1
			// 
			this.separator1.Name = "separator1";
			// 
			// cmdOptions
			// 
			this.cmdOptions.Label = "Optionen";
			this.cmdOptions.Name = "cmdOptions";
			this.cmdOptions.ShowImage = true;
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
			this.group2.ResumeLayout(false);
			this.group2.PerformLayout();
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
		internal Microsoft.Office.Tools.Ribbon.RibbonGroup group2;
		internal Microsoft.Office.Tools.Ribbon.RibbonButton cmdRefresh;
		internal Microsoft.Office.Tools.Ribbon.RibbonButton cmdEditTable;
		internal Microsoft.Office.Tools.Ribbon.RibbonButton button1;
		internal Microsoft.Office.Tools.Ribbon.RibbonSeparator separator1;
		internal Microsoft.Office.Tools.Ribbon.RibbonGallery loginGalery;
		internal Microsoft.Office.Tools.Ribbon.RibbonMenu loginMenu;
		internal Microsoft.Office.Tools.Ribbon.RibbonSeparator separator2;
		internal Microsoft.Office.Tools.Ribbon.RibbonButton loginButton;
	}

	partial class ThisRibbonCollection
	{
		internal PpsMenu PpsMenu
		{
			get { return this.GetRibbon<PpsMenu>(); }
		}
	}
}

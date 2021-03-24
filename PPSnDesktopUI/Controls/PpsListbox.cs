#region -- copyright --
//
// Licensed under the EUPL, Version 1.1 or - as soon they will be approved by the
// European Commission - subsequent versions of the EUPL(the "Licence"); You may
// not use this work except in compliance with the Licence.
//
// You may obtain a copy of the Licence at:
// http://ec.europa.eu/idabc/eupl
//
// Unless required by applicable law or agreed to in writing, software distributed
// under the Licence is distributed on an "AS IS" basis, WITHOUT WARRANTIES OR
// CONDITIONS OF ANY KIND, either express or implied. See the Licence for the
// specific language governing permissions and limitations under the Licence.
//
#endregion
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Markup;
using TecWare.DE.Data;
using TecWare.PPSn.UI;

namespace TecWare.PPSn.Controls
{
	#region -- enum PpsListBoxShowSelectionType ---------------------------------------

	/// <summary></summary>
	public enum PpsListBoxShowSelectionType
	{
		/// <summary>Always hilite selected items.</summary>
		Always,
		/// <summary>Hilite selected item when focused.</summary>
		WhenFocused,
		/// <summary>Never show selection.</summary>
		Never
	} // enum PpsListBoxShowSelectionType

	#endregion

	#region -- class PpsListBox -------------------------------------------------------

	/// <summary></summary>
	public class PpsListBox : ListView
	{
		private bool TryGetViewTemplate(IEnumerable newValue, out DataTemplate template)
		{
			template = null;

			if (newValue == null)
				return false;

			var type = newValue.GetType();
			if (type.IsConstructedGenericType && type.GetGenericTypeDefinition() == typeof(IEnumerable<>))
			{
				var itemType = type.GetGenericArguments()[0];
				template = this.FindResource<DataTemplate>(PpsListGridView.GetViewResourceKeyFromTypeName(itemType.Name));
				return template != null;
			}
			else
				return false;
		} // func TryGetViewTemplate

		private PpsListGridViewColumns GenerateGridViewColumns(IDataColumns columns)
		{
			var cols = new PpsListGridViewColumns();
			for (var i = 0; i < columns.Columns.Count; i++)
				cols.AddChild(GenerateGridViewColum(columns.Columns[i], i));
			return cols;
		} // func GenerateGridViewColumns

		private PpsListGridViewColumn GenerateGridViewColum(IDataColumn c, int index)
		{
			return new PpsListGridViewColumn
			{
				Header = c.Name,
				DisplayMemberBinding = new Binding($"[{index}]")
			};
		} // func GenerateGridViewColum

		protected override void OnItemTemplateChanged(DataTemplate oldItemTemplate, DataTemplate newItemTemplate)
		{
			if (newItemTemplate is PpsListGridViewTemplate gridViewTemplate) // is the template a column based template create a view
				View = new PpsListGridView(this, (PpsListGridViewColumns)gridViewTemplate.LoadContent());

			base.OnItemTemplateChanged(oldItemTemplate, newItemTemplate);
		} // proc OnItemTemplateChanged

		protected override void OnItemsSourceChanged(IEnumerable oldValue, IEnumerable newValue)
		{
			base.OnItemsSourceChanged(oldValue, newValue);

			// auto detect-view
			if (UseAutoView)
			{
				if (TryGetViewTemplate(newValue, out var template))
					ItemTemplate = template;
				else if (newValue is IDataColumns columns)
					View = new PpsListGridView(this, GenerateGridViewColumns(columns));
			}
		} // proc OnItemsSourceChanged

		#region -- ShowSelectionMode - Property ---------------------------------------

		/// <summary>The type of showing selected items</summary>
		public static readonly DependencyProperty ShowSelectionModeProperty = DependencyProperty.Register(nameof(ShowSelectionMode), typeof(PpsListBoxShowSelectionType), typeof(PpsButton), new FrameworkPropertyMetadata(PpsListBoxShowSelectionType.Always));

		/// <summary>The property defines the type of hiliting selected items</summary>
		public PpsListBoxShowSelectionType ShowSelectionMode { get => (PpsListBoxShowSelectionType)GetValue(ShowSelectionModeProperty); set => SetValue(ShowSelectionModeProperty, value); }

		#endregion

		#region -- UseAutoGridView - property -----------------------------------------

		public static readonly DependencyProperty UseAutoViewProperty = DependencyProperty.Register(nameof(UseAutoView), typeof(bool), typeof(PpsListBox), new FrameworkPropertyMetadata(BooleanBox.False));

		public bool UseAutoView { get => BooleanBox.GetBool(GetValue(UseAutoViewProperty)); set => SetValue(UseAutoViewProperty, value); }

		#endregion

		static PpsListBox()
		{
			DefaultStyleKeyProperty.OverrideMetadata(typeof(PpsListBox), new FrameworkPropertyMetadata(typeof(PpsListBox)));
		} // sctor
	} // class PpsListBox

	#endregion

	#region -- class PpsListGridViewColumn --------------------------------------------

	[RuntimeNameProperty("Name")]
	public class PpsListGridViewColumn : GridViewColumn
	{
		PpsListGridView gridView;

		internal void PrepareColumn(PpsListGridView gridView, ListViewItem item)
		{
			this.gridView = gridView;
			if (Double.IsNaN(Width)) // use star!
			{
				item.SizeChanged += Item_SizeChanged;
			}
		} // proc PrepareColumn

		private void Item_SizeChanged(object sender, SizeChangedEventArgs e)
		{
			var item = (ListViewItem)sender;
			//var lvi = (ListView)ItemsControl.GetItemsOwner(item);
			var view = gridView; // (GridView) lvi.View;

			// wird für jede zeile und spalte ausgeführt!

			var totalColumnWidth = 0.0;
			for (var i = 0; i < view.Columns.Count; i++)
				if( view.Columns[i] != this)
					totalColumnWidth += view.Columns[i].ActualWidth;

			var newWidth = item.ActualWidth - totalColumnWidth;
			if (newWidth > 0)
				Width = newWidth;
		}

		// public GridLength Size { get; set; }

		#region -- Name - property ----------------------------------------------------

		public static readonly DependencyProperty NameProperty = FrameworkElement.NameProperty.AddOwner(typeof(PpsListGridViewColumn));

		public string Name { get => (string)GetValue(NameProperty); set => SetValue(NameProperty, value); }

		#endregion
	} // class PpsListGridViewColumn

	#endregion

	#region -- class PpsListGridViewColumns -------------------------------------------

	[ContentProperty("Columns")]
	public class PpsListGridViewColumns : FrameworkElement, IAddChild
	{
		private readonly List<PpsListGridViewColumn> columns = new List<PpsListGridViewColumn>();

		public void AddChild(object value)
			=> Columns.Add((PpsListGridViewColumn)value);

		public void AddText(string text)
			=> throw new NotImplementedException();

		internal void PrepareColumns(PpsListGridView gridView, ListViewItem item)
		{
			foreach (var c in columns)
			{
				c.PrepareColumn(gridView, item);
				gridView.Columns.Add(c);
			}
		} // proc PrepareColumns

		public List<PpsListGridViewColumn> Columns => columns;
	} // class PpsListGridViewColumns

	#endregion

	#region -- class PpsListGridView --------------------------------------------------

	public class PpsListGridView : GridView
	{
		private readonly PpsListGridViewColumns baseColumns = null;

		public PpsListGridView()
		{
		} // ctor

		public PpsListGridView(PpsListBox listBox, PpsListGridViewColumns columns)
		{
			baseColumns = columns;

			var res = listBox.TryFindResource(typeof(PpsListGridViewColumn));
			if (res is Style gridViewHeaderStyle)
				ColumnHeaderContainerStyle = gridViewHeaderStyle;
		} // ctor

		protected override void PrepareItem(ListViewItem item)
		{
			if (baseColumns != null && Columns.Count == 0 && baseColumns.Columns.Count > 0)
				baseColumns.PrepareColumns(this, item);
			base.PrepareItem(item);
		} // proc PrepareItem

		protected override object ItemContainerDefaultStyleKey 
			=> typeof(PpsListGridViewColumns);

		/// <summary>Generate a resource key string for the type name.</summary>
		/// <param name="typeName"></param>
		/// <returns></returns>
		public static object GetViewResourceKeyFromTypeName(string typeName)
		{
			if (typeName.EndsWith("DataRow"))
				typeName = typeName.Substring(0, typeName.Length - 7);
			return "PPSn" + typeName + "Columns";
		} // func GetViewResourceKeyFromTypeName

		static PpsListGridView()
		{
			AllowsColumnReorderProperty.OverrideMetadata(typeof(PpsListGridView), new FrameworkPropertyMetadata(BooleanBox.False));
		}
	} // class PpsListGridView

	#endregion

	#region -- class PpsListGridViewTemplate ------------------------------------------

	/// <summary>List grid view template.</summary>
	public class PpsListGridViewTemplate : DataTemplate
	{
	} // class PpsListGridViewTemplate

	#endregion
}

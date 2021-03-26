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
using System.ComponentModel;
using System.Diagnostics;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Markup;
using TecWare.DE.Data;
using TecWare.DE.Stuff;
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
		public PpsListBox()
		{
		} // ctor

		protected override Size ArrangeOverride(Size arrangeBounds)
		{
			var sz = base.ArrangeOverride(arrangeBounds);
			if (View is PpsListGridView v)
				v.ArrangeColumns(sz.Width);
			return sz;
		} // proc ArrangeOverride

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
				View = new PpsListGridView(this, gridViewTemplate);

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

	#region -- class PpsGridViewColumn ------------------------------------------------

	internal sealed class PpsGridViewColumn : GridViewColumn
	{
		private readonly PpsListGridView gridView;
		private readonly PpsListGridViewColumn column;

		public PpsGridViewColumn(PpsListGridView gridView, PpsListGridViewColumn column)
		{
			this.gridView = gridView ?? throw new ArgumentNullException(nameof(gridView));
			this.column = column ?? throw new ArgumentNullException(nameof(column));

			Header = column.Header;
			CellTemplate = column.CellTemplate;
			DisplayMemberBinding = column.DisplayMemberBinding;
			UpdateActualWidth(Double.NaN);

			column.PropertyChanged += Column_PropertyChanged;
		} // ctor

		private void Column_PropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			switch (e.PropertyName)
			{
				case nameof(PpsListGridViewColumn.Header):
					Header = column.Header;
					break;
				case nameof(PpsListGridViewColumn.Width):
					UpdateActualWidth(Double.NaN);
					break;
				case nameof(PpsListGridViewColumn.DisplayMemberBinding):
					DisplayMemberBinding = column.DisplayMemberBinding;
					break;
				case nameof(PpsListGridViewColumn.CellTemplate):
					CellTemplate = column.CellTemplate;
					break;
			}
		} // proc Column_PropertyChanged

		public void UpdateActualWidth(double actualWidth)
		{
			if (column.Width.IsAbsolute)
				Width = column.Width.Value;
			else
			{
				if (Double.IsNaN(actualWidth))
				{
					actualWidth = column.Width.Value * 100;
					gridView.InvalidateColumnsArrange();
				}

				if (Width != actualWidth)
					Width = actualWidth;
			}
		} // proc UpdateActualWidth
	} // class PpsGridViewColumn

	#endregion

	#region -- class PpsListGridViewColumn --------------------------------------------

	public class PpsListGridViewColumn : FrameworkContentElement, INotifyPropertyChanged
	{
		public event PropertyChangedEventHandler PropertyChanged;

		private void InvokePropertyChanged(string propertyName)
			=> PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

		protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
		{
			// Debug.Print("{0}: {1} -> {2}", e.Property.Name, e.OldValue, e.NewValue);
			base.OnPropertyChanged(e);
			if (e.Property == WidthProperty || e.Property == HeaderProperty || e.Property == CellTemplateProperty)
				InvokePropertyChanged(e.Property.Name);
		} // proc OnPropertyChanged

		internal GridViewColumn CreateGridViewColumn(PpsListGridView gridView)
			=> new PpsGridViewColumn(gridView, this);

		#region -- Header - property --------------------------------------------------

		public static readonly DependencyProperty HeaderProperty = DependencyProperty.Register(nameof(Header), typeof(object), typeof(PpsListGridViewColumn), new FrameworkPropertyMetadata(null));

		public object Header { get => GetValue(HeaderProperty); set => SetValue(HeaderProperty, value); }

		#endregion

		#region -- CellTemplate - property --------------------------------------------

		public static readonly DependencyProperty CellTemplateProperty = DependencyProperty.Register(nameof(CellTemplate), typeof(DataTemplate), typeof(PpsListGridViewColumn), new FrameworkPropertyMetadata(null));

		public DataTemplate CellTemplate { get => (DataTemplate)GetValue(CellTemplateProperty); set => SetValue(CellTemplateProperty, value); }

		#endregion

		#region -- Width - property ---------------------------------------------------

		public static readonly DependencyProperty WidthProperty = DependencyProperty.Register(nameof(Width), typeof(GridLength), typeof(PpsListGridViewColumn), new FrameworkPropertyMetadata(new GridLength(1.0, GridUnitType.Star)));

		public GridLength Width { get => (GridLength)GetValue(WidthProperty); set => SetValue(WidthProperty, value); }

		#endregion

		#region -- DisplayMemberBinding - property ------------------------------------

		private BindingBase displayMemberBinding = null; // no dependency property for markups

		public BindingBase DisplayMemberBinding
		{
			get => displayMemberBinding;
			set 
			{
				if (displayMemberBinding != value)
				{
					displayMemberBinding = value;
					InvokePropertyChanged(nameof(DisplayMemberBinding));
				}
			}
		} // prop DisplayMemberBinding

		#endregion
	} // class PpsListGridViewColumn

	#endregion

	#region -- class PpsListGridViewColumns -------------------------------------------

	[ContentProperty("Columns")]
	public class PpsListGridViewColumns : FrameworkContentElement, IAddChild
	{
		private readonly List<PpsListGridViewColumn> columns = new List<PpsListGridViewColumn>();

		public PpsListGridViewColumns()
		{
		} // ctor

		public void AddChild(object value)
			=> Columns.Add((PpsListGridViewColumn)value);

		public void AddText(string text)
			=> throw new NotImplementedException();

		public List<PpsListGridViewColumn> Columns => columns;
	} // class PpsListGridViewColumns

	#endregion

	#region -- class PpsListGridViewTemplate ------------------------------------------

	/// <summary>List grid view template.</summary>
	public class PpsListGridViewTemplate : DataTemplate
	{
	} // class PpsListGridViewTemplate

	#endregion

	#region -- class PpsListGridView --------------------------------------------------

	public class PpsListGridView : GridView
	{
		private readonly PpsListGridViewColumns columns = null;
		private readonly PpsListBox attachedListBox = null;

		public PpsListGridView(PpsListBox listBox, PpsListGridViewColumns columns)
		{
			attachedListBox = listBox;
			this.columns = columns ?? throw new ArgumentNullException(nameof(columns));
			Init(listBox);
		} // ctor

		public PpsListGridView(PpsListBox listBox, PpsListGridViewTemplate template)
		{
			attachedListBox = listBox;
			columns = (PpsListGridViewColumns)template.LoadContent();
			Init(listBox);
		} // ctor

		private void Init(PpsListBox listBox)
		{
			// set default container style
			var res = listBox.TryFindResource(typeof(PpsListGridViewColumn));
			if (res is Style gridViewHeaderStyle)
				ColumnHeaderContainerStyle = gridViewHeaderStyle;

			// prepare columns
			if (columns != null)
			{
				foreach (var col in columns.Columns)
					Columns.Add(col.CreateGridViewColumn(this));
				InvalidateColumnsArrange();
			}
		} // proc Init

		internal void InvalidateColumnsArrange()
		{
			if (attachedListBox == null)
				attachedListBox.InvalidateArrange();
		} // proc InvalidateColumnsArrange

		internal void ArrangeColumns(double actualWidth)
		{
			if (columns == null || columns.Columns.Count == 0 )
				return;

			var totalWidth = actualWidth;
			var absoluteWidth = 0.0;
			var starWidth = 0.0;

			foreach (var c in columns.Columns)
			{
				if (c.Width.IsAbsolute)
					absoluteWidth += c.Width.Value;
				else if (c.Width.IsStar)
					starWidth += c.Width.Value;
			}

			var restWidth = totalWidth - absoluteWidth;
			if (restWidth > 0)
			{
				for (var i = 0; i < columns.Columns.Count; i++)
				{
					var width = columns.Columns[i].Width;
					if (width.IsStar)
						((PpsGridViewColumn)Columns[i]).UpdateActualWidth(width.Value * restWidth / starWidth);
				}
			}
		} // proc ArrangeColumns

		protected override object DefaultStyleKey 
			=> base.DefaultStyleKey;

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
}

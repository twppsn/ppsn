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
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Markup;
using System.Windows.Media;
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
		private FrameworkElement headerControl = null;

		public PpsListBox()
		{
		} // ctor

		public override void OnApplyTemplate()
		{
			headerControl = GetTemplateChild("PART_Header") as FrameworkElement;
			base.OnApplyTemplate();

			UpdateHeaderControl();
		} // proc OnApplyTemplate 

		protected override void OnItemsSourceChanged(IEnumerable oldValue, IEnumerable newValue)
		{
			base.OnItemsSourceChanged(oldValue, newValue);

			// auto detect-view
			if (UseAutoView)
			{
				//if (TryGetViewTemplate(newValue, out var template))
				//	ItemTemplate = template;
				//else if (newValue is IDataColumns columns)
				//	View = new PpsListGridView(this, GenerateGridViewColumns(columns));
			}
		} // proc OnItemsSourceChanged

		internal void UpdateHeaderControl()
		{
			if (headerControl != null)
				headerControl.Visibility = PpsListColumns.GetColumns(this) == null ? Visibility.Collapsed : Visibility.Visible;
		} // proc UpdateHeaderControl

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
			SelectionModeProperty.OverrideMetadata(typeof(PpsListBox), new FrameworkPropertyMetadata(SelectionMode.Single));
		} // sctor
	} // class PpsListBox

	#endregion

	#region -- class PpsListColumn ----------------------------------------------------

	public class PpsListColumn : FrameworkContentElement, INotifyPropertyChanged
	{
		public event PropertyChangedEventHandler PropertyChanged;

		internal FrameworkElement CreateCell(PpsListColumnRow parent)
		{
			if (CellTemplate != null)
			{
				var content = new ContentPresenter
				{
					Content = parent.DataContext,
					ContentTemplate = CellTemplate
				};
				return content;
			}
			else if (DisplayMemberBinding != null)
			{
				var text = new TextBlock
				{
					Foreground = Brushes.Black,
					Background = Brushes.LightCoral
				};

				text.SetBinding(TextBlock.TextProperty, DisplayMemberBinding);
				return text;
			}
			else
				return null;
		} // func CreateCell

		private void InvokePropertyChanged(string propertyName)
			=> PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

		protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
		{
			// Debug.Print("{0}: {1} -> {2}", e.Property.Name, e.OldValue, e.NewValue);
			base.OnPropertyChanged(e);
			if (e.Property == WidthProperty || e.Property == HeaderProperty || e.Property == CellTemplateProperty)
				InvokePropertyChanged(e.Property.Name);
		} // proc OnPropertyChanged

		#region -- Header - property --------------------------------------------------

		public static readonly DependencyProperty HeaderProperty = DependencyProperty.Register(nameof(Header), typeof(object), typeof(PpsListColumn), new FrameworkPropertyMetadata(null));

		public object Header { get => GetValue(HeaderProperty); set => SetValue(HeaderProperty, value); }

		#endregion

		#region -- CellTemplate - property --------------------------------------------

		public static readonly DependencyProperty CellTemplateProperty = DependencyProperty.Register(nameof(CellTemplate), typeof(DataTemplate), typeof(PpsListColumn), new FrameworkPropertyMetadata(null));

		public DataTemplate CellTemplate { get => (DataTemplate)GetValue(CellTemplateProperty); set => SetValue(CellTemplateProperty, value); }

		#endregion

		#region -- Width - property ---------------------------------------------------

		public static readonly DependencyProperty WidthProperty = DependencyProperty.Register(nameof(Width), typeof(GridLength), typeof(PpsListColumn), new FrameworkPropertyMetadata(new GridLength(1.0, GridUnitType.Star)));

		public GridLength Width { get => (GridLength)GetValue(WidthProperty); set => SetValue(WidthProperty, value); }

		#endregion

		#region -- HorizontalAlignment - property -------------------------------------

		public static readonly DependencyProperty HorizontalAlignmentProperty = DependencyProperty.Register(nameof(HorizontalAlignment), typeof(HorizontalAlignment), typeof(PpsListColumn), new FrameworkPropertyMetadata(HorizontalAlignment.Left));

		public HorizontalAlignment HorizontalAlignment { get => (HorizontalAlignment)GetValue(HorizontalAlignmentProperty); set => SetValue(HorizontalAlignmentProperty, value); }

		#endregion

		#region -- VerticalAlignment - property ---------------------------------------

		public static readonly DependencyProperty VerticalAlignmentProperty = DependencyProperty.Register(nameof(VerticalAlignment), typeof(VerticalAlignment), typeof(PpsListColumn), new FrameworkPropertyMetadata(VerticalAlignment.Center));

		public VerticalAlignment VerticalAlignment { get => (VerticalAlignment)GetValue(VerticalAlignmentProperty); set => SetValue(VerticalAlignmentProperty, value); }

		#endregion

		#region -- DisplayMemberBinding - property ------------------------------------

		private BindingBase displayMemberBinding = null; // no dependency property for markups

		public BindingBase DisplayMemberBinding
		{
			get => displayMemberBinding;
			set {
				if (displayMemberBinding != value)
				{
					displayMemberBinding = value;
					InvokePropertyChanged(nameof(DisplayMemberBinding));
				}
			}
		} // prop DisplayMemberBinding

		#endregion
	} // class PpsListColumn

	#endregion

	#region -- class PpsListColumns ---------------------------------------------------

	[ContentProperty("ListColumns")]
	public class PpsListColumns : FrameworkContentElement, IAddChild
	{
		private readonly ObservableCollection<PpsListColumn> columns = new ObservableCollection<PpsListColumn>();

		public PpsListColumns()
		{
		} // ctor

		public void AddChild(object value)
			=> columns.Add((PpsListColumn)value);

		public void AddText(string text)
			=> throw new NotImplementedException();

		public static readonly DependencyProperty RowTemplateProperty = DependencyProperty.Register(nameof(RowTemplate), typeof(DataTemplate), typeof(PpsListColumns), new FrameworkPropertyMetadata(null));

		public DataTemplate RowTemplate { get => (DataTemplate)GetValue(RowTemplateProperty); set => SetValue(RowTemplateProperty, value); }

		public Collection<PpsListColumn> ListColumns => columns;

		#region -- Columns ------------------------------------------------------------

		public static readonly DependencyProperty ColumnsProperty = DependencyProperty.RegisterAttached("Columns", typeof(PpsListColumns), typeof(PpsListColumns), new FrameworkPropertyMetadata(null, new PropertyChangedCallback(OnColumnsChanged)));

		private static readonly DependencyPropertyKey columnsGeneratorPropertyKey = DependencyProperty.RegisterAttachedReadOnly("ColumnsGenerator", typeof(PpsListColumnGenerator), typeof(PpsListColumns), new FrameworkPropertyMetadata(null, new PropertyChangedCallback(OnColumnsGeneratorChanged)));
		public static readonly DependencyProperty ColumnsGeneratorProperty = columnsGeneratorPropertyKey.DependencyProperty;

		private static void OnColumnsGeneratorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
		{
			if (e.OldValue is PpsListColumnGenerator g)
				g.Dispose();
		} // proc OnColumnsGeneratorChanged

		private static DataTemplate GetDefaultTemplate(FrameworkElement fe)
			=> fe.TryFindResource(typeof(PpsListColumns)) as DataTemplate;

		private static void OnColumnsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
		{
			var itemsControl = (ItemsControl)d;
			if (e.NewValue is PpsListColumns columns)
			{
				itemsControl.ItemTemplate = columns.RowTemplate ?? GetDefaultTemplate(itemsControl);
				if (itemsControl is PpsListBox l)
					l.UpdateHeaderControl();

				SetColumnsGenerator(itemsControl, new PpsListColumnGenerator(itemsControl, columns));
			}
		} // proc OnColumnsChanged

		public static PpsListColumns GetColumns(DependencyObject d)
			=> (PpsListColumns)d.GetValue(ColumnsProperty);

		public static void SetColumns(DependencyObject d, PpsListColumns value)
			=> d.SetValue(ColumnsProperty, value);

		private static void SetColumnsGenerator(ItemsControl itemsControl, IPpsListColumnGenerator value)
			=> itemsControl.SetValue(columnsGeneratorPropertyKey, value);

		public static IPpsListColumnGenerator GetColumnsGenerator(ItemsControl itemsControl)
			=> (IPpsListColumnGenerator)itemsControl.GetValue(ColumnsGeneratorProperty);

		#endregion
	} // class PpsListColumns

	#endregion

	#region -- interface IPpsListColumnGenerator --------------------------------------

	/// <summary>Implements a column generator.</summary>
	public interface IPpsListColumnGenerator
	{
		/// <summary>Attach a cell genrator</summary>
		/// <param name="cellGenerator"></param>
		void RegisterCellGenerator(IPpsListCellGenerator cellGenerator);

		/// <summary>Recalc offset and withs of the columns.</summary>
		/// <param name="availableWidth"></param>
		void InvalidateColumnWidths(double availableWidth, bool fromHeader);
		/// <summary></summary>
		/// <param name="index"></param>
		/// <param name="offset"></param>
		/// <param name="width"></param>
		bool GetCellBounds(int index, out double offset, out double width);

		/// <summary>Return a column description for the index.</summary>
		/// <param name="index"></param>
		/// <returns></returns>
		PpsListColumn this[int index] { get; }
		/// <summary>Number of columns</summary>
		int Count { get; }

		/// <summary>Are column width calculated.</summary>
		bool HasColumnWidths { get; }
	} // interface IPpsListColumnGenerator

	#endregion

	#region -- interface IPpsListCellGenerator ----------------------------------------

	/// <summary>Implemented for a cell generator.</summary>
	public interface IPpsListCellGenerator
	{
		/// <summary>Update cell content presenter.</summary>
		void UpdateCells();

		/// <summary></summary>
		void InvalidateArrange();

		/// <summary>Current column generator</summary>
		IPpsListColumnGenerator Generator { get; }
	} // interface IPpsListCellGenerator

	#endregion

	#region -- class PpsListColumnGenerator -------------------------------------------

	internal sealed class PpsListColumnGenerator : IPpsListColumnGenerator, IDisposable
	{
		#region -- class ColumnMeasure ------------------------------------------------

		private sealed class ColumnMeasure
		{
			private readonly PpsListColumn column;
			private double actualOffset = 0;
			private double actualWidth = 0;

			public ColumnMeasure(PpsListColumn column)
			{
				this.column = column ?? throw new ArgumentNullException(nameof(column));
			} // ctor

			public void UpdateActualWidth(double offset, double width)
			{
				actualOffset = offset;
				actualWidth = width;
			} // proc UpdateActualWidth

			public PpsListColumn Column => column;
			public GridLength Width => column.Width;
			public double ActualOffset => actualOffset;
			public double ActualWidth => actualWidth;
		} // class ColumnMeasure

		#endregion

		private readonly ItemsControl control;
		private readonly PpsListColumns columns;
		private readonly List<ColumnMeasure> columnInfo = new List<ColumnMeasure>();
		private readonly List<WeakReference<IPpsListCellGenerator>> cellGenerators = new List<WeakReference<IPpsListCellGenerator>>();

		private double lastColumnWidth = 0;
		private double? invalidColumnWidths = null;

		public PpsListColumnGenerator(ItemsControl control, PpsListColumns columns)
		{
			this.control = control ?? throw new ArgumentNullException(nameof(control));
			this.columns = columns ?? throw new ArgumentNullException(nameof(columns));

			if (columns.ListColumns is INotifyCollectionChanged nc)
				nc.CollectionChanged += Columns_CollectionChanged;

			InvalidateColumns();
		} // ctor

		public void Dispose()
		{
			if (columns.ListColumns is INotifyCollectionChanged nc)
				nc.CollectionChanged -= Columns_CollectionChanged;
		} // proc Dispose

		private void Columns_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
		{
			switch (e.Action)
			{
				case NotifyCollectionChangedAction.Add:
				case NotifyCollectionChangedAction.Remove:
				case NotifyCollectionChangedAction.Reset:
				case NotifyCollectionChangedAction.Move:
				case NotifyCollectionChangedAction.Replace:
					InvalidateColumns();
					break;
			}
		} // event Columns_CollectionChanged

		private void InvalidateColumns()
		{
			var columnCount = columns.ListColumns.Count;
			var isChanged = false;

			// create new controls for columns
			for (var i = 0; i < columnCount; i++)
			{
				var col = columns.ListColumns[i];
				if (i < columnInfo.Count)
				{
					if (columnInfo[i].Column != col)
					{
						columnInfo[i] = new ColumnMeasure(col);
						isChanged = true;
					}
				}
				else
				{
					columnInfo.Add(new ColumnMeasure(col));
					isChanged = true;
				}
			}

			// resize array
			var removeAtEnd = columnInfo.Count - columnCount;
			if (removeAtEnd > 0)
			{
				columnInfo.RemoveRange(columnCount, removeAtEnd);
				isChanged = true;
			}

			if (isChanged)
			{
				control.InvalidateMeasure();
				ForEachCell(c => c.UpdateCells());
			}

			// update offset and withs
			if (lastColumnWidth > 0)
				InvalidateColumnWidths(lastColumnWidth, false);
		} // proc InvalidateColumns

		public void InvalidateColumnWidths(double availableWidth, bool fromHeader)
		{
			if (lastColumnWidth != availableWidth)
			{
				if (invalidColumnWidths.HasValue)
				{
					if (!fromHeader)
						invalidColumnWidths = availableWidth;
				}
				else
					invalidColumnWidths = availableWidth;
			}
		} // proc InvalidateColumnWidths

		private void UpdateColumnWidths(double actualWidth)
		{
			var cols = columns.ListColumns;
			if (cols.Count == 0)
				return;

			lastColumnWidth = actualWidth;

			var totalWidth = actualWidth;
			var absoluteWidth = 0.0;
			var starWidth = 0.0;

			foreach (var col in columnInfo)
			{
				if (col.Width.IsAbsolute)
					absoluteWidth += col.Width.Value;
				else if (col.Width.IsStar)
					starWidth += col.Width.Value;
			}

			var restWidth = totalWidth - absoluteWidth;
			var offset = 0.0;
			var hasRestWidth = restWidth > 0 && !Double.IsInfinity(actualWidth);

			foreach (var col in columnInfo)
			{
				var width = col.Width;
				if (width.IsStar)
				{
					if (hasRestWidth)
						col.UpdateActualWidth(offset, width.Value * restWidth / starWidth);
					else
						col.UpdateActualWidth(offset, width.Value * 100 / starWidth);
				}
				else if (width.IsAbsolute)
					col.UpdateActualWidth(offset, width.Value);
				else
					col.UpdateActualWidth(offset, 100);

				offset += col.ActualWidth;
			}

			ForEachCell(c => c.InvalidateArrange());
		} // proc UpdateColumnWidths

		public void RegisterCellGenerator(IPpsListCellGenerator cellGenerator)
		{
			for (var i = 0; i < cellGenerators.Count; i++)
			{
				if (!cellGenerators[i].TryGetTarget(out _))
				{
					cellGenerators[i] = new WeakReference<IPpsListCellGenerator>(cellGenerator);
					InvalidateCells(cellGenerator);
					return;
				}
			}

			cellGenerators.Add(new WeakReference<IPpsListCellGenerator>(cellGenerator));
			InvalidateCells(cellGenerator);
		} // proc RegisterCellGenerator

		private void InvalidateCells(IPpsListCellGenerator cellGenerator)
		{
			if (columnInfo.Count > 0)
			{
				cellGenerator.UpdateCells();
				cellGenerator.InvalidateArrange();
			}
		} // proc InvalidateCells

		private void ForEachCell(Action<IPpsListCellGenerator> action)
		{
			foreach (var wc in cellGenerators)
			{
				if (wc.TryGetTarget(out var c))
					action(c);
			}
		} // proc ForEachCell

		public bool GetCellBounds(int index, out double offset, out double width)
		{
			if (invalidColumnWidths.HasValue)
			{
				UpdateColumnWidths(invalidColumnWidths.Value);
				invalidColumnWidths = null;
			}

			if (index >= 0 && index < columnInfo.Count)
			{
				offset = columnInfo[index].ActualOffset;
				width = columnInfo[index].ActualWidth;
				return true;
			}
			else
			{
				offset = 0;
				width = 0;
				return false;
			}
		} // func GetCellBounds

		public PpsListColumn this[int index] => columnInfo[index].Column;
		public int Count => columnInfo.Count;

		public bool HasColumnWidths => columns.ListColumns.Count > 0;
	} // class PpsListColumnGenerator

	#endregion

	#region -- class PpsListColumnCells -----------------------------------------------

	public abstract class PpsListColumnCells : FrameworkElement, IPpsListCellGenerator
	{
		private readonly VisualCollection visuals;

		private UIElement[] elements = Array.Empty<UIElement>();
		private IPpsListColumnGenerator columnGenerator = null;

		protected PpsListColumnCells()
		{
			visuals = new VisualCollection(this);
		} // ctor

		protected override void OnVisualParentChanged(DependencyObject oldParent)
		{
			base.OnVisualParentChanged(oldParent);

			var itemsControl = this.GetVisualParent<ItemsControl>();
			if (itemsControl != null)
				SetColumnGenerator(PpsListColumns.GetColumnsGenerator(itemsControl));
		} // proc OnVisualParentChanged

		protected void SetColumnGenerator(IPpsListColumnGenerator columnGenerator)
		{
			this.columnGenerator = columnGenerator;
			if (columnGenerator != null)
			{
				columnGenerator.RegisterCellGenerator(this);
				columnGenerator.InvalidateColumnWidths(ActualWidth, IsHeader);
			}
		} // proc SetColumnGenerator

		private bool GetElementInfo(int index, out UIElement element, out double actualOffset, out double actualWidth)
		{
			if (index >= 0 && index < elements.Length
				&& elements[index] != null
				&& columnGenerator != null && columnGenerator.GetCellBounds(index, out actualOffset, out actualWidth))
			{
				element = elements[index];
				return true;
			}
			else
			{
				actualOffset = 0;
				actualWidth = 0;
				element = null;
				return false;
			}
		} // func GetElementInfo

		protected abstract UIElement CreateElement(int i, PpsListColumn column);

		private UIElement AppendElement(UIElement element, PpsListColumn column)
		{
			if (element is FrameworkElement fe)
				SetStyleForCell(column, fe);

			visuals.Add(element);
			AddLogicalChild(element);
			return element;
		} // proc AppendElement

		protected virtual void SetStyleForCell(PpsListColumn column, FrameworkElement fe) { }

		private void ClearElement(int i)
		{
			if (elements[i] != null)
			{
				visuals.Remove(elements[i]);
				RemoveLogicalChild(elements[i]);
				elements[i] = null;
			}
		} // proc ClearElement

		private void ClearElements()
		{
			for (var i = 0; i < elements.Length; i++)
				ClearElement(i);
		} // func ClearElements

		void IPpsListCellGenerator.InvalidateArrange()
			=> InvalidateArrangeFromCellGenerator();

		protected abstract void InvalidateArrangeFromCellGenerator();

		void IPpsListCellGenerator.UpdateCells()
		{
			if (elements == null)
				elements = new UIElement[columnGenerator.Count];
			else if (elements.Length != columnGenerator.Count)
			{
				ClearElements();

				elements = new UIElement[columnGenerator.Count];
			}

			for (var i = 0; i < elements.Length; i++)
			{
				ClearElement(i);
				var column = columnGenerator[i];
				elements[i] = AppendElement(CreateElement(i, column), column);
			}

			InvalidateMeasure();
			InvalidateArrange();
		} // proc IPpsListCellGenerator.UpdateCells

		private Size MeasureElement(int index, Size availableSize)
		{
			if (GetElementInfo(index, out var element, out var actualOffset, out var actualWidth))
			{
				var sz = new Size(actualWidth, availableSize.Height);
				element.Measure(sz);

				return new Size(actualOffset + actualWidth, element.DesiredSize.Height);
			}
			else
				return Size.Empty;
		} // proc Measure

		private void ArrangeElement(int index, Size finalSize)
		{
			if (GetElementInfo(index, out var element, out var actualOffset, out var actualWidth))
				element.Arrange(new Rect(actualOffset, 0, actualWidth, finalSize.Height));
		} // proc Arrange

		protected sealed override Size MeasureOverride(Size availableSize)
		{
			if (!Double.IsInfinity(availableSize.Width) && ColumnGenerator != null)
				ColumnGenerator.InvalidateColumnWidths(availableSize.Width, IsHeader);

			var height = 0.0;
			var width = 0.0;

			if (columnGenerator != null)
			{
				for (var i = 0; i < columnGenerator.Count; i++)
				{
					var sz = MeasureElement(i, availableSize);
					if (!sz.IsEmpty)
					{
						if (!Double.IsInfinity(sz.Height) && sz.Height > height)
							height = sz.Height;
						width = sz.Width;
					}
				}
			}

			return new Size(width, height);
		} // func MeasureOverride

		protected override Size ArrangeOverride(Size finalSize)
		{
			if (columnGenerator != null)
			{
				// arrange cells
				for (var i = 0; i < columnGenerator.Count; i++)
					ArrangeElement(i, finalSize);
			}

			return finalSize;
		} // func ArrangeOverride

		protected sealed override int VisualChildrenCount
			=> visuals.Count;

		protected sealed override Visual GetVisualChild(int index)
			=> visuals[index];

		protected sealed override IEnumerator LogicalChildren
			=> visuals.GetEnumerator();

		IPpsListColumnGenerator IPpsListCellGenerator.Generator => columnGenerator;

		protected abstract bool IsHeader { get; }
		protected IPpsListColumnGenerator ColumnGenerator => columnGenerator;
	} // class PpsListColumnCells

	#endregion

	#region -- class PpsListColumnRow -------------------------------------------------

	public class PpsListColumnRow : PpsListColumnCells
	{
		protected override UIElement CreateElement(int i, PpsListColumn column)
		{
			if (column.CellTemplate != null)
			{
				var contentPresenter = new ContentPresenter
				{
					Content = DataContext,
					ContentTemplate = column.CellTemplate
				};
				return contentPresenter;

			}
			else if (column.DisplayMemberBinding != null)
			{
				var text = new TextBlock();
				text.Padding = new Thickness(2);
				text.SetBinding(TextBlock.TextProperty, column.DisplayMemberBinding);
				return text;
			}
			else
				return null;
		} // func CreateElement

		protected override void SetStyleForCell(PpsListColumn column, FrameworkElement fe)
		{
			base.SetStyleForCell(column, fe);
			fe.SetBinding(HorizontalAlignmentProperty, new Binding(nameof(PpsListColumn.HorizontalAlignment)) { Source = column });
			fe.SetBinding(VerticalAlignmentProperty, new Binding(nameof(PpsListColumn.VerticalAlignment)) { Source = column });
		} // proc SetStyleForCell

		protected override void InvalidateArrangeFromCellGenerator()
			=> InvalidateMeasure();

		protected override Size ArrangeOverride(Size finalSize)
		{
			// invalidate size
			if (ColumnGenerator != null)
				ColumnGenerator.InvalidateColumnWidths(finalSize.Width, false);

			return base.ArrangeOverride(finalSize);
		} // func ArrangeOverride

		protected override bool IsHeader => false;
	} // class PpsListColumnRow

	#endregion

	#region -- class PpsListColumnHeaderCell ------------------------------------------

	/// <summary>Column header cell.</summary>
	public sealed class PpsListColumnHeaderCell : ContentControl
	{
		public PpsListColumnHeaderCell(PpsListColumn column)
		{
			// this.column = column ?? throw new ArgumentNullException(nameof(column));

			DataContext = column;
			SetBinding(ContentProperty, new Binding(nameof(PpsListColumn.Header)));
		} // ctor

		static PpsListColumnHeaderCell()
		{
			DefaultStyleKeyProperty.OverrideMetadata(typeof(PpsListColumnHeaderCell), new FrameworkPropertyMetadata(typeof(PpsListColumnHeaderCell)));
		}
	} // class PpsListColumnHeaderCell

	#endregion

	#region -- class PpsListColumnHeader ----------------------------------------------

	/// <summary>Column presenter</summary>
	public class PpsListColumnHeader : PpsListColumnCells
	{
		protected override UIElement CreateElement(int i, PpsListColumn column)
			=> new PpsListColumnHeaderCell(column);

		protected override void SetStyleForCell(PpsListColumn column, FrameworkElement fe)
		{
			base.SetStyleForCell(column, fe);

			fe.SetValue(HorizontalAlignmentProperty, HorizontalAlignment.Stretch);
			fe.SetValue(VerticalAlignmentProperty, VerticalAlignment.Stretch);
		} // proc SetStyleForCell

		protected override void InvalidateArrangeFromCellGenerator()
			=> InvalidateMeasure();

		protected override bool IsHeader => true;
	} // class PpsListColumnHeader

	#endregion
}

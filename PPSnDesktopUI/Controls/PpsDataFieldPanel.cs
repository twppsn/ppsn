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
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace TecWare.PPSn.Controls
{
	#region -- enum Optimization --------------------------------------------------

	/// <summary>Selects the Optimization strategy</summary>
	public enum Optimization
	{
		/// <summary>the result has the lowest difference between the tallest and smallest column</summary>
		TightestSpan,
		/// <summary>the result has the lowest difference between the tallest and smallest column - the last column is omitted</summary>
		TightestSpanWithoutLast,
		/// <summary>the result has the smallest column heights overall</summary>
		LowestHeight
	} // enum Optimization

	#endregion

	/// <summary>
	/// This panel sorts its children in Columns. One can define the ColumnCount and the optimisation strategy.
	/// </summary>
	public class PpsDataFieldPanel : Panel
	{
		#region ---- Properties ---------------------------------------------------------

		/// <summary>DependencyProperty</summary>
		public static readonly DependencyProperty ColumnCountProperty = DependencyProperty.Register(nameof(ColumnCount),
																								   typeof(int),
																								   typeof(PpsDataFieldPanel),
																								   new FrameworkPropertyMetadata(2, FrameworkPropertyMetadataOptions.AffectsMeasure, new PropertyChangedCallback(InvalidateColumnDefinitionsCallback)));
		/// <summary>DependencyProperty</summary>
		public static readonly DependencyProperty ColumnMarginProperty = DependencyProperty.Register(nameof(ColumnMargin), typeof(double), typeof(PpsDataFieldPanel), new FrameworkPropertyMetadata(30.0, FrameworkPropertyMetadataOptions.AffectsMeasure));
		/// <summary>DependencyProperty</summary>
		public static readonly DependencyProperty LabelWidthProperty = DependencyProperty.Register(nameof(LabelWidth), typeof(double), typeof(PpsDataFieldPanel), new FrameworkPropertyMetadata(80.0, FrameworkPropertyMetadataOptions.AffectsArrange));
		/// <summary>DependencyProperty</summary>
		public static readonly DependencyProperty RowMarginProperty = DependencyProperty.Register(nameof(RowMargin), typeof(double), typeof(PpsDataFieldPanel), new FrameworkPropertyMetadata(5.0, FrameworkPropertyMetadataOptions.AffectsMeasure));
		/// <summary>DependencyProperty</summary>
		public static readonly DependencyProperty RowHeightProperty = DependencyProperty.Register(nameof(RowHeight), typeof(double), typeof(PpsDataFieldPanel), new FrameworkPropertyMetadata(32.0, FrameworkPropertyMetadataOptions.AffectsMeasure));
		/// <summary>DependencyProperty</summary>
		public static readonly DependencyProperty ArrangeOptimizationProperty = DependencyProperty.Register(nameof(ArrangeOptimization),
																										 typeof(Optimization),
																										 typeof(PpsDataFieldPanel),
																										 new FrameworkPropertyMetadata(Optimization.TightestSpan, FrameworkPropertyMetadataOptions.AffectsMeasure, new PropertyChangedCallback(InvalidateColumnDefinitionsCallback)));
		/// <summary>DependencyProperty</summary>
		public static readonly DependencyProperty IndentGroupChildrenProperty = DependencyProperty.Register(nameof(IndentGroupChildren), typeof(int), typeof(PpsDataFieldPanel), new FrameworkPropertyMetadata(5, FrameworkPropertyMetadataOptions.AffectsArrange));

		/// <summary>DependencyProperty</summary>
		public static readonly DependencyProperty MinColumnWidthProperty = DependencyProperty.Register(nameof(MinColumnWidth), typeof(double), typeof(PpsDataFieldPanel), new FrameworkPropertyMetadata(250.0, FrameworkPropertyMetadataOptions.AffectsMeasure));

		/// <summary>the count of Columns the Panel shold arrage the items</summary>
		public int ColumnCount { get => (int)GetValue(ColumnCountProperty); set => SetValue(ColumnCountProperty, value); }
		/// <summary>the margin between the Columns</summary>
		public double ColumnMargin { get => (double)GetValue(ColumnMarginProperty); set => SetValue(ColumnMarginProperty, value); }
		/// <summary>Sets the minium Width of the Label and Control - AffectsMeasure</summary>
		public double MinColumnWidth { get => (double)GetValue(MinColumnWidthProperty); set => SetValue(MinColumnWidthProperty, value); }
		/// <summary>the width reserved for the Label</summary>
		public double LabelWidth { get => (double)GetValue(LabelWidthProperty); set => SetValue(LabelWidthProperty, value); }
		/// <summary>the Margin between rows</summary>
		public double RowMargin { get => (double)GetValue(RowMarginProperty); set => SetValue(RowMarginProperty, value); }
		/// <summary>the horizontal grid</summary>
		public double RowHeight { get => (double)GetValue(RowHeightProperty); set => SetValue(RowHeightProperty, value); }
		/// <summary>selection of the desired appearance</summary>
		public Optimization ArrangeOptimization { get => (Optimization)GetValue(ArrangeOptimizationProperty); set => SetValue(ArrangeOptimizationProperty, value); }
		/// <summary>if >0 children of a group are indented by the value</summary>
		public int IndentGroupChildren { get => (int)GetValue(IndentGroupChildrenProperty); set => SetValue(IndentGroupChildrenProperty, value); }

		/// <summary>The Panel has no Content to show</summary>
		public static readonly DependencyProperty IsEmptyProperty = DependencyProperty.Register(nameof(IsEmpty), typeof(bool), typeof(PpsDataFieldPanel), new FrameworkPropertyMetadata(false));
		/// <summary>The Panel has no Content to show</summary>
		public bool IsEmpty { get => BooleanBox.GetBool(GetValue(IsEmptyProperty)); private set => SetValue(IsEmptyProperty, value); }

		private void EvaluateContent()
		{
			var isempty = !labels.Any(kvp => kvp.Key.IsVisible);
			if ((isempty != IsEmpty) && IsEmpty)
				columnDefinitions = null;

			IsEmpty = isempty;
		}

		#endregion

		#region ---- Callbacks ----------------------------------------------------------

		private static void InvalidateColumnDefinitionsCallback(DependencyObject d, DependencyPropertyChangedEventArgs e)
			=> ((PpsDataFieldPanel)d).InvalidateColumnDefinitions();

		private static void LabelChangedCallback(DependencyObject d, DependencyPropertyChangedEventArgs e)
		{
			if (d is Visual v)
			{
				var pa = VisualTreeHelper.GetParent(v);
				if (pa is PpsDataFieldPanel p)
					p.UpdateLabelInformation(d, e);
			}
		} // proc LabelChangedCallback

		#endregion

		/// <summary>Called when the columns should be reordered</summary>
		public void InvalidateColumnDefinitions()
			=> columnDefinitions = null;

		#region ---- Attached Properties ------------------------------------------------

		#region Label

		/// <summary>DependencyProperty</summary>
		public static readonly DependencyProperty LabelProperty = DependencyProperty.RegisterAttached("Label", typeof(object), typeof(PpsDataFieldPanel), new FrameworkPropertyMetadata(String.Empty, new PropertyChangedCallback(LabelChangedCallback)));

		/// <summary>Returns the Label of the Control</summary>
		/// <param name="d">Control</param>
		/// <returns></returns>
		public static object GetLabel(DependencyObject d)
			=> d.GetValue(LabelProperty);
		/// <summary>Sets the Label of the Control</summary>
		/// <param name="d">Control</param>
		/// <param name="value"></param>
		public static void SetLabel(DependencyObject d, object value)
			=> d.SetValue(LabelProperty, value);

		#endregion

		#region GridLines

		/// <summary>DependencyProperty</summary>
		public static readonly DependencyProperty GridLinesProperty = DependencyProperty.RegisterAttached("GridLines", typeof(int), typeof(PpsDataFieldPanel), new PropertyMetadata((int)1));

		/// <summary>Returns the requested LineCount of the Control</summary>
		/// <param name="d">Control</param>
		/// <returns></returns>
		public static int GetGridLines(DependencyObject d)
			=> (int)d.GetValue(GridLinesProperty);

		/// <summary>Sets the amount of lines the Control should fill</summary>
		/// <param name="d">Control</param>
		/// <param name="value"></param>
		public static void SetGridLines(DependencyObject d, int value)
			=> d.SetValue(GridLinesProperty, value);

		#endregion

		#region FullWidth

		/// <summary>DependencyProperty</summary>
		public static readonly DependencyProperty FullWidthProperty = DependencyProperty.RegisterAttached("FullWidth", typeof(bool), typeof(PpsDataFieldPanel), new PropertyMetadata(false));

		/// <summary>Returns if the Control is a Separator (unsused)</summary>
		/// <param name="d">Control</param>
		/// <returns></returns>
		public static bool GetFullWidth(DependencyObject d)
			=> (bool)d.GetValue(FullWidthProperty);

		/// <summary>Marks the Control as a Separator</summary>
		/// <param name="d">Control</param>
		/// <param name="value"></param>
		public static void SetFullWidth(DependencyObject d, bool value)
			=> d.SetValue(FullWidthProperty, value);

		#endregion

		#region GroupName
		/// <summary>DependencyProperty</summary>
		public static readonly DependencyProperty GroupNameProperty = DependencyProperty.RegisterAttached("GroupName", typeof(string), typeof(PpsDataFieldPanel), new PropertyMetadata(String.Empty));

		/// <summary>Returns the GroupID of the Control (-1 means ungrouped)</summary>
		/// <param name="d">Control</param>
		/// <returns></returns>
		public static string GetGroupName(DependencyObject d)
			=> (string)d.GetValue(GroupNameProperty);
		/// <summary>Sets the GroupID of the Control (-1 means ungrouped)</summary>
		/// <param name="d">Control</param>
		/// <param name="value"></param>
		public static void SetGroupName(DependencyObject d, string value)
			=> d.SetValue(GroupNameProperty, value);
		#endregion

		#endregion

		#region ---- Handling of Labels -----------------------------------------------

		#region -- class PpsDataFieldPanelCollection-----------------------------------

		private sealed class PpsDataFieldPanelCollection : UIElementCollection
		{
			private PpsDataFieldPanel panel;

			public PpsDataFieldPanelCollection(UIElement visualParent, FrameworkElement logicalParent)
				: base(visualParent, logicalParent)
			{
				this.panel = (PpsDataFieldPanel)visualParent;
			}

			public override int Add(UIElement element)
			{
				panel.InvalidateColumnDefinitions();
				if (!panel.labels.ContainsKey(element) && !(element is Label))
				{
					panel.UpdateLabelInformation(element, new DependencyPropertyChangedEventArgs(LabelProperty, null, GetLabel(element)));
					element.IsVisibleChanged += (s, e) =>
					{
						panel.labels[element].Visibility = ((FrameworkElement)s).Visibility;
						if (BooleanBox.GetBool(e.NewValue) == panel.IsEmpty)
							panel.EvaluateContent();
					};
				}
				return base.Add(element);
			}

			public override void Remove(UIElement element)
			{
				panel.InvalidateColumnDefinitions();
				if (panel.labels.ContainsKey(element))
					panel.RemoveLabel(element);
				base.Remove(element);
			}
		} // class class PpsDataFieldPanelCollection

		#endregion

		private readonly Dictionary<UIElement, Label> labels = new Dictionary<UIElement, Label>();

		private void UpdateLabelInformation(DependencyObject d, DependencyPropertyChangedEventArgs e)
		{
			var element = (UIElement)d;
			var newValue = e.NewValue;

			// because UpdateLabelInformation must not delete a Element, each Control gets its label even if it's empty
			if (labels.TryGetValue(element, out var lbl))
			{
				if (lbl.Content != newValue) // change
					lbl.Content = newValue;
			}
			else // new
			{
				lbl = new Label() { Content = newValue, Target = element };
				labels.Add(element, lbl);
				InternalChildren.Add(lbl);
			}
		}

		private void RemoveLabel(DependencyObject d)
		{
			var element = (UIElement)d;
			if (labels.TryGetValue(element, out var lbl))
			{
				InternalChildren.Remove(lbl);
				labels.Remove(lbl);
			}
		}

		/// <summary></summary>
		/// <param name="logicalParent"></param>
		/// <returns></returns>
		protected override UIElementCollection CreateUIElementCollection(FrameworkElement logicalParent)
			=> new PpsDataFieldPanelCollection(this, logicalParent);

		#endregion

		#region ---- Layouting ----------------------------------------------------------

		private List<int> columnDefinitions;

		/// <summary>
		/// Approximates the Size of the Control
		/// </summary>
		/// <param name="availableSize">maximum Size</param>
		/// <returns></returns>
		protected override Size MeasureOverride(Size availableSize)
		{
			var returnSize = new Size();

			if (columnDefinitions == null)
			{
				var childrenToArrange = (from UIElement child in InternalChildren where ((child.Visibility == Visibility.Visible) && (labels.ContainsKey(child))) select child).ToArray();

				columnDefinitions = PartitionDataFields(childrenToArrange, ColumnCount, ArrangeOptimization);
			}

			var highestColumn = columnDefinitions.Max();

			var height = highestColumn * (RowHeight + RowMargin);

			returnSize.Height = height;

			if (availableSize.Width == Double.PositiveInfinity)
				returnSize.Width = ColumnCount * (MinColumnWidth + ColumnMargin);
			else
				returnSize.Width = Math.Max(ColumnCount * (MinColumnWidth + ColumnMargin), availableSize.Width);

			return returnSize;
		} // func MeasurrOverride

		/// <summary>
		/// Arranges the children in the given Size - returns a size different of finalSize if needed
		/// </summary>
		/// <param name="finalSize"></param>
		/// <returns></returns>
		protected override Size ArrangeOverride(Size finalSize)
		{
			var returnSize = new Size();

			var childrenToArrange = (from UIElement child in InternalChildren where (!(child is Label) && (child.Visibility == Visibility.Visible)) select child).ToArray();

			if (!childrenToArrange.Any())
				return returnSize;

			if (columnDefinitions == null)
			{
				columnDefinitions = PartitionDataFields(childrenToArrange, ColumnCount, ArrangeOptimization);
			}

			var highestColumn = columnDefinitions.Max();

			var height = highestColumn * (RowHeight + RowMargin);

			returnSize.Height = height;

			returnSize.Width = Math.Max(finalSize.Width, ColumnCount * (MinColumnWidth + ColumnMargin));

			var columnWidth = (returnSize.Width - ((ColumnCount - 1) * ColumnMargin)) / ColumnCount;

			var i = 0;

			var groupName = String.Empty;

			for (var column = 0; column < columnDefinitions.Count; column++)
			{
				var columnHeight = columnDefinitions[column];
				var columnX = column * (columnWidth + ColumnMargin);

				var line = 0;

				while (line < columnHeight)
				{
					var child = childrenToArrange[i];
					var controlLines = GetGridLines(child);

					var thisLabelWidth = GetFullWidth(child) ? 0 : LabelWidth;

					var controlRect = new Rect(columnX + thisLabelWidth,
											   line * (RowHeight + RowMargin),
											   columnWidth - thisLabelWidth,
											   controlLines * RowHeight + (controlLines - 1) * RowMargin);
					child.Arrange(controlRect);

					((FrameworkElement)child).Height = controlRect.Height;

					// indentation
					var indentation = 0;
					if (IndentGroupChildren > 0)
					{
						if (groupName == String.Empty) // no group
							indentation = 0;
						else if (GetGroupName(child) != groupName) // group header
							indentation = 0;
						else
							indentation = 5;
						groupName = GetGroupName(child);
					}

					if (!GetFullWidth(child) && labels.TryGetValue(child, out var lbl))
					{
						var labelRect = new Rect(columnX + indentation,
												 controlRect.Top,
												 LabelWidth,
												 controlRect.Height);
						lbl.Arrange(labelRect);
					}

					// next child
					i++;
					// increase line
					line += controlLines;
				}
			}

			return returnSize;
		} // func ArrangeOverride

		#endregion

		#region ---- Partitioning Functions ---------------------------------------------

		/// <summary>returns the heights of the columns</summary>
		/// <param name="children"></param>
		/// <param name="columnCount"></param>
		/// <param name="optimisation"></param>
		/// <returns></returns>
		private static List<int> PartitionDataFields(UIElement[] children, int columnCount, Optimization optimisation)
		{
			var groupedInput = new List<int>();

			// group the items
			var actualGroup = String.Empty;
			for (var i = 0; i < children.Length; i++)
			{
				var newGroup = GetGroupName(children[i]);

				// if ungrouped (-1) just add an entity
				if (newGroup == String.Empty)
					groupedInput.Add(GetGridLines(children[i]));

				// if new group starts, add a list element
				else if (newGroup != actualGroup)
					groupedInput.Add(GetGridLines(children[i]));

				// a group was set and this item is in the same group as the item before
				else
					groupedInput[groupedInput.Count - 1] += GetGridLines(children[i]);

				actualGroup = newGroup;
			}

			var columnDefinitions = Partition(groupedInput, columnCount, optimisation);

			var accessed = 0;
			for (var i = 0; i < columnDefinitions.Count; i++)
			{
				var input = groupedInput.GetRange(accessed, columnDefinitions[i]);
				accessed += columnDefinitions[i];
				var height = input.Sum();
				columnDefinitions[i] = height;
			}

			return columnDefinitions;
		} // func PartitionDataFields

		/// <summary>this function partitions the elements to a specific columncount - the result with the least difference between highest and lowest will be returned</summary>
		/// <param name="elements">the heights of the elements</param>
		/// <param name="partitionCount">the count of columns</param>
		/// <param name="optimization"></param>
		/// <returns>the count of the elements</returns>
		private static List<int> Partition(List<int> elements, int partitionCount, Optimization optimization = Optimization.TightestSpan)
		{
			// trivial cases
			if (partitionCount <= 0)
				throw new ArgumentOutOfRangeException(nameof(partitionCount));
			if (partitionCount == 1)
				return new List<int> { elements.Count };
			if (partitionCount >= elements.Count)
			{
				var ret = new List<int>();
				for (var i = 0; i < partitionCount; i++)
					ret.Add(i < elements.Count ? 1 : 0);
				return ret;
			}

			var fullstack = elements.Sum();
			// a good partitioning should have stack heights around that value
			var median = (fullstack + partitionCount - 1) / partitionCount;

			var possiblePartitions = new List<List<int>>();

			// if there are not many more elements than columns, the main algorithm may fail to provide a result with the correct column count
			// fallback approach - produces at least one valid partitioning (but not the best)
			var naiivecount = elements.Count / partitionCount;
			// rolling varies the insertion of the ''undivideable'' items - results in better fallbacks, does not cost much
			var rolling = true;
			var roll = 0;
			// following variables for speedup
			var failed = -1;
			while (rolling)
			{
				var results = new List<int>();
				for (var i = 0; i < partitionCount; i++)
					results.Add(naiivecount);
				if (failed < 0)
					failed = elements.Count - results.Sum();
				if (failed + roll > partitionCount)
				{
					rolling = false;
					continue;
				}
				for (var i = 0; i < failed; i++)
					results[i + roll]++;

				possiblePartitions.Add(results);
				// naiive approach did not have a rest
				if (failed == 0)
				{
					rolling = false;
					continue;
				}
				roll++;
			}
			// fallback end

			// real partitioner
			// the algorithm is producing results in the valid range
			var valid = true;
			// the direction for optimization
			var direction = -1;
			// from this point the optimization will start in both directions
			var pivot = median + 1;
			while (valid)
			{
				pivot += direction;
				var tempResult = new List<int>();
				var tempsum = 0;

				var tempStackHeight = 0;
				for (var i = 0; i < elements.Count; i++)
				{
					tempStackHeight += elements[i];
					if (tempStackHeight >= pivot || i == (elements.Count - 1))
					{
						var partitionSize = (i + 1) - tempsum;

						// add the partition
						if (tempResult.Count > 0)
						{
							tempResult.Add(partitionSize);
						}
						else
						{
							tempResult.Add(i + 1);
						}

						tempsum += partitionSize;

						// reset the stackheight
						tempStackHeight = 0;
					}
				}
				// the stacks should not be smaller then half the median
				if ((direction == -1) && (pivot <= median / 2))
				{
					direction = 1;
					pivot = median;
				}

				if (direction == -1)
				{
					if (tempResult.Count <= partitionCount)
					{
						possiblePartitions.Add(tempResult);
					}
				}
				else
				{
					if (tempResult.Count >= partitionCount)
					{
						possiblePartitions.Add(tempResult);
					}
					else
					{
						valid = false;
					}
				}
			}

			// valid partitions have an exact partition count match
			var validPartitions = (from partition in possiblePartitions where partition.Count == partitionCount select partition);

			switch (optimization)
			{
				case Optimization.TightestSpanWithoutLast:
					return (from partition in validPartitions orderby PartitionSpan(elements, partition.GetRange(0, partition.Count - 1)) ascending select partition).First();
				case Optimization.LowestHeight:
					return (from partition in validPartitions orderby PartitionMaxHeight(elements, partition) ascending select partition).First();
				case Optimization.TightestSpan:
				default:
					return (from partition in validPartitions orderby PartitionSpan(elements, partition) ascending select partition).First();
			}
		} // func Partition

		private static int PartitionMaxHeight(List<int> elements, List<int> partitions)
		{
			var max = 0;
			for (var i = 0; i < partitions.Count; i++)
			{
				var count = elements.GetRange(partitions.Take(i).Sum(), partitions[i]).Sum();
				max = Math.Max(count, max);
			}
			return max;
		} // func PartitionMaxHeight

		private static int PartitionSpan(List<int> elements, List<int> partitions)
		{
			var min = Int32.MaxValue;
			var max = 0;
			for (var i = 0; i < partitions.Count; i++)
			{
				var count = elements.GetRange(partitions.Take(i).Sum(), partitions[i]).Sum();
				min = Math.Min(count, min);
				max = Math.Max(count, max);
			}
			return max - min;
		} // func PartitionMaxHeight

		#endregion
	}
}

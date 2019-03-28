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
using System.Collections.Specialized;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace TecWare.PPSn.Controls
{
	#region -- class PpsFixedStackPanel -----------------------------------------------

	/// <summary></summary>
	public class PpsFixedStackPanel : VirtualizingPanel, IScrollInfo
	{
		#region -- ItemHeight - property ----------------------------------------------

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
		public static readonly DependencyProperty ItemHeightProperty = DependencyProperty.Register(nameof(ItemHeight), typeof(double), typeof(PpsFixedStackPanel), new FrameworkPropertyMetadata(Double.NaN));
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

		/// <summary>Is the item height fixed.</summary>
		public double ItemHeight { get => (double)GetValue(ItemHeightProperty); set => SetValue(ItemHeightProperty, value); }

		#endregion

		private ScrollViewer scrollViewer = null;

		private Point scrollOffset = new Point(0.0, 0.0);
		private Size viewportSize = new Size(0.0, 0.0);
		private Size extenedSize = new Size(0.0, 0.0);

		private bool canScrollX = false;
		private bool canScrollY = false;

		/// <summary></summary>
		public PpsFixedStackPanel()
		{
		}

		private static void ValidateRange(ref double v, double minValue, double maxValue)
		{
			if (v < minValue)
				v = minValue;
			else if (v > maxValue)
				v = minValue < maxValue ? maxValue : minValue;
		} // proc ValidateRange

		private double GetItemsPerPage()
			=> Math.Floor(viewportSize.Height / ItemHeight);

		private bool SetScrollOffsetCore(double x, double y)
		{
			ValidateRange(ref x, 0.0, extenedSize.Width - viewportSize.Width);
			ValidateRange(ref y, 0.0, extenedSize.Height - viewportSize.Height);

			var newScrollOffset = new Point(x, y);
			if (newScrollOffset != scrollOffset)
			{
				scrollOffset = newScrollOffset;
				return true;
			}
			else
				return false;
		} // func SetScrollOffsetCore

		private void SetScrollOffset(double x, double y)
		{
			if (SetScrollOffsetCore(x,y))
			{
				//Debug.Print("ScrollOffset: {0:N0}", newScrollOffset.Y);
				scrollViewer?.InvalidateScrollInfo();
				InvalidateMeasure();
			}
		} // proc SetScrollOffset

		private void UpdateScrollInfo(Size newViewportSize, ItemsControl itemsControl)
		{
			if (itemsControl.HasItems)
			{
				var extendedHeight = itemsControl.Items.Count * ItemHeight;
				UpdateScrollInfo(newViewportSize, new Size(newViewportSize.Width, extendedHeight));
			}
			else
				ClearScrollInfo(newViewportSize);
		} // proc UpdateScrollInfo

		private void UpdateScrollInfo(Size newViewport, Size newExtended)
		{
			var updateScrollViewer = false;
			if (newViewport != viewportSize)
			{
				viewportSize = newViewport;
				updateScrollViewer = true;
			}
			if (newExtended != extenedSize)
			{
				extenedSize = newExtended;
				updateScrollViewer = true;
			}

			if (SetScrollOffsetCore(scrollOffset.X, scrollOffset.Y))
			{
				updateScrollViewer = true;
				InvalidateMeasure();
			}

			//Debug.Print("ScrollHeight: {0:N0} zu {1:N0}", viewportSize.Height, extenedSize.Height);

			if (updateScrollViewer && scrollViewer != null)
				scrollViewer.InvalidateScrollInfo();
		} // proc UpdateScrollInfo

		private void ClearScrollInfo(Size viewportSize)
			=> UpdateScrollInfo(viewportSize, new Size(0.0, 0.0));
		
		private IItemContainerGenerator GetItemContainerGenerator()
		{
			var generator = ItemContainerGenerator;
			if (generator == null)
			{
				var children = InternalChildren; // fix: null bug
				return ItemContainerGenerator;
			}
			else
				return generator;
		} // func GetItemContainerGenerator

		/// <summary></summary>
		/// <param name="finalSize"></param>
		/// <returns></returns>
		protected override Size ArrangeOverride(Size finalSize)
		{
			var internalChildren = Children;
			var generator = GetItemContainerGenerator();
			var itemsControl = ItemsControl.GetItemsOwner(this);
			var itemHeight = ItemHeight;

			if (internalChildren.Count == 0)
			{
				ClearScrollInfo(finalSize);
				viewportSize = finalSize;
				extenedSize = new Size(0.0, 0.0);
				return finalSize;
			}
			else
			{
				// set scroll info
				UpdateScrollInfo(finalSize, itemsControl);

				var startOffsetY = (int)Math.Floor(scrollOffset.Y / itemHeight) * itemHeight - scrollOffset.Y;

				// arrange children
				for (var i = 0; i < internalChildren.Count; i++)
				{
					var child = internalChildren[i];
					var index = generator.IndexFromGeneratorPosition(new GeneratorPosition(i, 0));
					child.Arrange(new Rect(0.0, startOffsetY, finalSize.Width, itemHeight));
					startOffsetY += itemHeight;
				}

				return finalSize;
			}
		} // proc ArrangeOverride

		/// <summary></summary>
		/// <param name="availableSize"></param>
		/// <returns></returns>
		protected override Size MeasureOverride(Size availableSize)
		{
			// check item height
			var itemHeight = ItemHeight;
			if (Double.IsNaN(itemHeight) || Double.IsInfinity(itemHeight))
				return availableSize;

			var childAvailableSize = new Size(availableSize.Width, itemHeight);
			var internalChildren = InternalChildren;
			var generator = GetItemContainerGenerator();
			var itemsControl = ItemsControl.GetItemsOwner(this);

			// calculate generation offset
			var startIndex = (int)Math.Floor(scrollOffset.Y / itemHeight);
			var startOffsetY = startIndex * itemHeight;
			var lastOffsetY = scrollOffset.Y + availableSize.Height;
			var currentIndex = startIndex;
			var desiredWidth = 0.0;

			if (itemsControl.HasItems)
			{
				UpdateScrollInfo(availableSize, itemsControl);

				var startPosition = generator.GeneratorPositionFromIndex(startIndex);
				var childIndex = startPosition.Offset == 0 ? startPosition.Index : startPosition.Index + 1;
				if (childIndex >= 0)
				{
					using (generator.StartAt(startPosition, GeneratorDirection.Forward, true))
					{
						while (true)
						{
							var child = (UIElement)generator.GenerateNext(out var isNew);
							if (child != null)
							{
								if (isNew)
								{
									if (childIndex >= internalChildren.Count)
										AddInternalChild(child);
									else
										InsertInternalChild(childIndex, child);

									// prepare child
									generator.PrepareItemContainer(child);
								}
								else
									Debug.Assert(child == internalChildren[childIndex], "Child generation error.");

								// measure child
								child.Measure(childAvailableSize);

								startOffsetY += itemHeight;
								desiredWidth = Math.Max(desiredWidth, child.DesiredSize.Width);

							}
							else
								break; // no more childs

							childIndex++;
							currentIndex++;

							// all items generated
							if (currentIndex >= itemsControl.Items.Count)
								break;
							if (startOffsetY >= lastOffsetY)
								break;
						}
					}
				}
			}
			else
				ClearScrollInfo(availableSize);

			// remove unused children
			CleanUpUnusedChildren(startIndex, currentIndex - 1);

			return new Size(desiredWidth, availableSize.Height);
		} // proc MeasureOverride

		private void CleanUpUnusedChildren(int firstIndex, int lastIndex)
		{
			var internalChildren = InternalChildren;
			var generator = GetItemContainerGenerator();

			for (var i = internalChildren.Count - 1; i >= 0; i--)
			{

				var generatorPosition = new GeneratorPosition(i, 0);
				var itemIndex = generator.IndexFromGeneratorPosition(generatorPosition);

				if (itemIndex < firstIndex || itemIndex > lastIndex)
				{
					generator.Remove(generatorPosition, 1);
					RemoveInternalChildRange(i, 1);
				}
			}
		} // proc CleanUpUnusedChildren

		/// <summary></summary>
		/// <param name="sender"></param>
		/// <param name="args"></param>
		protected override void OnItemsChanged(object sender, ItemsChangedEventArgs args)
		{
			switch (args.Action)
			{
				case NotifyCollectionChangedAction.Reset:
					break;
				case NotifyCollectionChangedAction.Remove:
				case NotifyCollectionChangedAction.Replace:
				case NotifyCollectionChangedAction.Move:
					RemoveInternalChildRange(args.Position.Index, args.ItemUICount);
					break;
			}
		} // proc OnItemsChanged

		#region -- IScrollInfo - members ----------------------------------------------

		void IScrollInfo.SetHorizontalOffset(double offset)
			=> SetScrollOffset(offset, scrollOffset.Y);

		void IScrollInfo.SetVerticalOffset(double offset)
			=> SetScrollOffset(scrollOffset.X, offset);

		Rect IScrollInfo.MakeVisible(Visual visual, Rect rectangle)
			=> new Rect();

		void IScrollInfo.LineDown()
			=> SetScrollOffset(scrollOffset.X, scrollOffset.Y + ItemHeight);
		void IScrollInfo.LineUp()
			=> SetScrollOffset(scrollOffset.X, scrollOffset.Y - ItemHeight);

		void IScrollInfo.LineLeft()
			=> SetScrollOffset(scrollOffset.X + 32.0, scrollOffset.Y);
		void IScrollInfo.LineRight()
			=> SetScrollOffset(scrollOffset.X - 32.0, scrollOffset.Y);

		void IScrollInfo.MouseWheelDown()
			=> SetScrollOffset(scrollOffset.X, scrollOffset.Y + ItemHeight * 3.0);
		void IScrollInfo.MouseWheelUp()
			=> SetScrollOffset(scrollOffset.X, scrollOffset.Y - ItemHeight * 3.0);

		void IScrollInfo.MouseWheelLeft()
			=> SetScrollOffset(scrollOffset.X + 32.0, scrollOffset.Y);
		void IScrollInfo.MouseWheelRight()
			=> SetScrollOffset(scrollOffset.X - 32.0, scrollOffset.Y);

		void IScrollInfo.PageDown()
			=> SetScrollOffset(scrollOffset.X, scrollOffset.Y + ItemHeight * GetItemsPerPage());
		void IScrollInfo.PageUp()
			=> SetScrollOffset(scrollOffset.X, scrollOffset.Y - ItemHeight * GetItemsPerPage());

		void IScrollInfo.PageRight()
			=> SetScrollOffset(scrollOffset.X + viewportSize.Width, scrollOffset.Y);
		void IScrollInfo.PageLeft()
			=> SetScrollOffset(scrollOffset.X - viewportSize.Width, scrollOffset.Y);

		double IScrollInfo.ExtentWidth => extenedSize.Width;
		double IScrollInfo.ExtentHeight => extenedSize.Height;

		double IScrollInfo.HorizontalOffset => scrollOffset.X;
		double IScrollInfo.VerticalOffset => scrollOffset.Y;

		double IScrollInfo.ViewportWidth => viewportSize.Width;
		double IScrollInfo.ViewportHeight => viewportSize.Height;

		bool IScrollInfo.CanHorizontallyScroll { get => canScrollX; set => canScrollX = value; }
		bool IScrollInfo.CanVerticallyScroll { get => canScrollY; set => canScrollY = value; }
		ScrollViewer IScrollInfo.ScrollOwner { get => scrollViewer; set => scrollViewer = value; }

		#endregion
	} // class PpsFixedStackPanel

	#endregion
}

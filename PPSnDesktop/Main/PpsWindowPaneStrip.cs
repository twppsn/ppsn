﻿#region -- copyright --
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
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace TecWare.PPSn.Main
{
	#region -- class PpsWindowPaneStripItemMoveArgs -----------------------------------

	internal sealed class PpsWindowPaneStripItemMoveArgs : RoutedEventArgs
	{
		private readonly int oldIndex;
		private readonly int newIndex;

		public PpsWindowPaneStripItemMoveArgs(int oldIndex, int newIndex)
			: base(PpsWindowPaneStrip.ItemMoveEvent)
		{
			this.oldIndex = oldIndex;
			this.newIndex = newIndex;
		} // ctor

		public int OldIndex => oldIndex;
		public int NewIndex => newIndex;
	} // class PpsWindowPaneStripItemMoveArgs
	
	#endregion

	#region -- class PpsWindowPaneStripItem -------------------------------------------

	internal class PpsWindowPaneStripItem : ContentControl
	{
		#region -- IsSelected - Property ----------------------------------------------

#pragma warning disable CS0108 // Member hides inherited member; missing new keyword
		public static readonly DependencyProperty IsSelectedProperty = Selector.IsSelectedProperty.AddOwner(typeof(PpsWindowPaneStripItem),
			new FrameworkPropertyMetadata(BooleanBox.False, FrameworkPropertyMetadataOptions.AffectsParentMeasure | FrameworkPropertyMetadataOptions.BindsTwoWayByDefault | FrameworkPropertyMetadataOptions.Journal, OnIsSelectedChanged));
#pragma warning restore CS0108 // Member hides inherited member; missing new keyword

		private static void OnIsSelectedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
		{
			if (d is PpsWindowPaneStripItem item)
			{
				var newValue = BooleanBox.GetBool(e.NewValue);
				if (newValue)
					item.OnSelected(new RoutedEventArgs(Selector.SelectedEvent, item));
				else
					item.OnUnselected(new RoutedEventArgs(Selector.UnselectedEvent, item));
			}
		} // proc OnIsSelectedChanged

		private void OnSelected(RoutedEventArgs e)
			=> RaiseEvent(e);

		private void OnUnselected(RoutedEventArgs e)
			=> RaiseEvent(e);

		[Bindable(true)]
		public bool IsSelected
		{
			get => (bool)GetValue(IsSelectedProperty);
			set => SetValue(IsSelectedProperty, BooleanBox.GetObject(value));
		} // prop IsSelected

		#endregion

		private bool Select()
		{
			IsSelected = true; // mark as selected
			return true;
		} // proc Select

		protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
		{
			if ((e.Source == this || !IsSelected) && Select())
				e.Handled = true;
			base.OnMouseLeftButtonDown(e);
		} // proc OnMouseLeftButtonDown

		private PpsWindowPaneStrip StripParent
			=> ItemsControl.ItemsControlFromItemContainer(this) as PpsWindowPaneStrip;

		public bool IsFixed => ((PpsWindowPaneHost)Content).IsFixed;

		static PpsWindowPaneStripItem()
		{
			DefaultStyleKeyProperty.OverrideMetadata(typeof(PpsWindowPaneStripItem), new FrameworkPropertyMetadata(typeof(PpsWindowPaneStripItem)));
		} // ctor
	} // class PpsWindowPaneStripItem

	#endregion

	#region -- class PpsWindowPaneStripPanel ------------------------------------------

	internal class PpsWindowPaneStripPanel : Panel
	{
		protected override Size ArrangeOverride(Size arrangeSize)
		{
			var left = 0.0;
			var stretchHeight = VerticalAlignment == VerticalAlignment.Stretch;
			foreach (var cur in InternalChildren.Cast<PpsWindowPaneStripItem>().Where(c => c.Visibility == Visibility.Visible))
			{
				var sz = cur.DesiredSize;
				cur.Arrange(new Rect(left, 0, sz.Width, stretchHeight ? arrangeSize.Height : sz.Height));
				left += sz.Width;
			}
			return new Size(arrangeSize.Width, arrangeSize.Height);
		} // func ArrangeOverride

		private bool IsOverflowItem(double remainingWidth, Size size)
			=> remainingWidth < size.Width;

		private int FindLastVisiblePosition(PpsWindowPaneStripItem item, double totalWidth)
		{
			var remainingWidth = totalWidth;
			var newIndex = 0;
			var currentItemSize = item.DesiredSize;

			foreach (var cur in InternalChildren.OfType<PpsWindowPaneStripItem>().TakeWhile(c => c != item))
			{
				if (!cur.IsFixed && IsOverflowItem(remainingWidth, currentItemSize))
					return newIndex;

				remainingWidth -= cur.DesiredSize.Width;
				newIndex++;
			}

			return newIndex - 1;
		} // func FindLastVisiblePosition

		protected override Size MeasureOverride(Size constraint)
		{
			var isOverflow = false;
			var remainingWidth = constraint.Width;
			var isInfinity = Double.IsInfinity(remainingWidth);

			var sumWidth = 0.0;
			var maxHeight = 0.0;

			// arrage tab items horizontal
			var currentIndex = 0;
			foreach (var cur in InternalChildren.Cast<PpsWindowPaneStripItem>())
			{
				// measure control
				cur.Measure(new Size(Double.PositiveInfinity, constraint.Height));
				var sz = cur.DesiredSize;
				if (isOverflow || (!isInfinity && IsOverflowItem(remainingWidth, sz)))
				{
					if (cur.IsSelected) // selected is hidden
					{
						// find measure new offset
						var newIndex = FindLastVisiblePosition(cur, constraint.Width);
						if (newIndex >= 0 && newIndex < currentIndex) // reorder item
						{
							RaiseEvent(new PpsWindowPaneStripItemMoveArgs(currentIndex, newIndex));
							Dispatcher.BeginInvoke(new Action(InvalidateMeasure));
							break;
						}
					}
					else
						cur.Visibility = Visibility.Hidden;
					isOverflow = true;
				}
				else
				{
					cur.Visibility = Visibility.Visible;
					if (!isInfinity)
						remainingWidth -= sz.Width;
					sumWidth += sz.Width;
					maxHeight = Math.Max(sz.Height, maxHeight);
				}
				currentIndex++;
			}

			return new Size(sumWidth, maxHeight);
		} // func MeasureOverride
	} // class PpsWindowPaneStripPanel

	#endregion

	#region -- class PpsWindowPaneStrip -----------------------------------------------

	internal delegate void PpsWindowPaneStripItemMoveEventEventHandler(object sender, PpsWindowPaneStripItemMoveArgs e);

	internal sealed class PpsWindowPaneStrip : ItemsControl
	{
		#region -- Container Element generation ---------------------------------------

		protected override void AddText(string text)
			=> throw new NotSupportedException();

		protected override DependencyObject GetContainerForItemOverride()
			=> new PpsWindowPaneStripItem();

		protected override bool IsItemItsOwnContainerOverride(object item)
			=> item is PpsWindowPaneStripItem;

		#endregion

		public static readonly RoutedEvent ItemMoveEvent;

		static PpsWindowPaneStrip()
		{
			ItemMoveEvent = EventManager.RegisterRoutedEvent(String.Empty, RoutingStrategy.Bubble, typeof(PpsWindowPaneStripItemMoveEventEventHandler), typeof(PpsWindowPaneStripItem));

			DefaultStyleKeyProperty.OverrideMetadata(typeof(PpsWindowPaneStrip), new FrameworkPropertyMetadata(typeof(PpsWindowPaneStrip)));
		} // ctor
	} // class PpsWindowPaneStrip

	#endregion
}

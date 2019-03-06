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
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace TecWare.PPSn.UI
{
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
			if((e.Source == this || !IsSelected) && Select())
				e.Handled = true;
			base.OnMouseLeftButtonDown(e);
		} // proc OnMouseLeftButtonDown

		private PpsWindowPaneStrip StripParent
			=> ItemsControl.ItemsControlFromItemContainer(this) as PpsWindowPaneStrip;

		static PpsWindowPaneStripItem()
		{
			DefaultStyleKeyProperty.OverrideMetadata(typeof(PpsWindowPaneStripItem), new FrameworkPropertyMetadata(typeof(PpsWindowPaneStripItem)));
		} // ctor
	} // class PpsWindowPaneStripItem

	#endregion

	#region -- class PpsWindowPaneStripItem -------------------------------------------

	internal class PpsWindowPaneStripPanel : Panel
	{
		protected override Size ArrangeOverride(Size arrangeSize)
		{
			var left = 0.0;
			foreach (var cur in InternalChildren.Cast<PpsWindowPaneStripItem>().Where(c => c.Visibility == Visibility.Visible))
			{
				var sz = cur.DesiredSize;
				cur.Arrange(new Rect(left, 0.0, sz.Width, sz.Height));
				left += sz.Width;
			}
			return new Size(left, arrangeSize.Height);
		} // func ArrangeOverride

		private bool IsOverflowItem(double remainingWidth, Size size)
			=> remainingWidth < size.Width;

		protected override Size MeasureOverride(Size constraint)
		{
			var isOverflow = false;
			var remainingWidth =constraint.Width;
			var isInfinity = Double.IsInfinity(remainingWidth);
			var sumWidth = 0.0;
			var maxHeight = 0.0;

			// arrage tab items horizontal
			foreach (var cur in InternalChildren.Cast<PpsWindowPaneStripItem>())
			{
				// measure control
				cur.Measure(new Size(Double.PositiveInfinity, constraint.Height));
				var sz = cur.DesiredSize;
				if (isOverflow || (!isInfinity && IsOverflowItem(remainingWidth, sz)))
				{
					if (cur.IsSelected) // selected is hidden
					{
						// todo:
					}
					else
						cur.Visibility = Visibility.Collapsed;
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
			}

			return new Size(sumWidth, maxHeight);
		} // func MeasureOverride
	} // class PpsWindowPaneStripPanel

	#endregion

	#region -- class PpsWindowPaneStrip -----------------------------------------------

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

		static PpsWindowPaneStrip()
		{
			DefaultStyleKeyProperty.OverrideMetadata(typeof(PpsWindowPaneStrip), new FrameworkPropertyMetadata(typeof(PpsWindowPaneStrip)));
		} // ctor
	} // class PpsWindowPaneStrip

	#endregion
}

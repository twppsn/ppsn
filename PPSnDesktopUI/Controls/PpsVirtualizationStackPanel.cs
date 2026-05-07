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
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace TecWare.PPSn.Controls
{
	public class PpsVirtualizationStackPanel : VirtualizingPanel, IScrollInfo
	{
		private ScrollViewer scrollViewer;
		private bool canScrollY = false;
		private bool canScrollX = false;

		private Vector offset;
		private Size extent;
		private Size viewPort;

		private Size itemSize = new(100, 30);

		private ItemContainerGenerator generator = null;

		#region --- ctor --------------------------------------------------------------
		public PpsVirtualizationStackPanel() 
		{
		} // ctor
		#endregion

		private IRecyclingItemContainerGenerator GetItemContainerGenerator()
		{
			if (generator is null)
			{
				var _ = InternalChildren; // get filled
				if (ItemContainerGenerator is null)
					throw new ArgumentNullException();
				generator = ItemContainerGenerator.GetItemContainerGeneratorForPanel(this);
			}
			return generator;
		} // func GetItemContainerGenertor


		protected override Size MeasureOverride(Size availableSize)
		{
			// Wir wollen die vorhandene Fläche ausfüllen

			var c = ItemsControl.GetItemsOwner(this);
			if (c == null) return availableSize;

			extent = new Size(itemSize.Width, c.Items.Count * itemSize.Height); // TODO: ItemSize dynamisch berechnen 
			viewPort = availableSize;

			var visibleCount = (int)Math.Ceiling(availableSize.Height / itemSize.Height); // Anzahl der sichtbaren Elemente

			var firstVisibleIndex = GetFirstVisibleIndex(); 
			var lastVisibleIndex = Math.Min(firstVisibleIndex + visibleCount, c.Items.Count) -1;

			var generator = GetItemContainerGenerator();
			var startingPosition = generator.GeneratorPositionFromIndex(firstVisibleIndex); // Wir rendern erst ab dem ersten sichtbaren Index
			var iChildIndex = (startingPosition.Offset == 0) ? startingPosition.Index : startingPosition.Index + 1;

			using (generator.StartAt(startingPosition, GeneratorDirection.Forward, true))
			{

				for (var i = firstVisibleIndex; i <= lastVisibleIndex; i++, iChildIndex ++) // Nur sichbare Elemente bearbeiten 
				{
					var child = (UIElement)generator.GenerateNext(out var isNew);
					if (isNew)
					{
						if (iChildIndex >= InternalChildren.Count)
							AddInternalChild(child); // Neu erstellten Container hinzufügen
						else
							InsertInternalChild(iChildIndex, child); // Neu erstellten Container an dem angegebenen Index einfügen

						ItemContainerGenerator.PrepareItemContainer(child); // Container Vorbereiten
					}
					else if (!InternalChildren.Contains(child))
					{
						InsertInternalChild(iChildIndex, child); // Container an dem angegebenen Index einfügen
						generator.PrepareItemContainer(child); // Container Vorbereiten
					}

					child.Measure(availableSize); // Gewünschte Größe des Elements wird ermittelt 

					if( i == firstVisibleIndex )
					{
						itemSize = child.DesiredSize; // Die erwünschte Größe unseres Kindelementes ist ab jetzt unsere Itemgröße 
						ScrollVelocity = (int)Math.Ceiling(child.DesiredSize.Height); // Die aufgerundete Höhe unseres ersten Kindelements ist unsere Scrollgeschwindigkeit
					}
				}

			}

			CleanupItems(firstVisibleIndex, lastVisibleIndex); // Nicht sichtbares Zeug aufräumen

			return new Size(
				double.IsInfinity(availableSize.Width) ? 0 : availableSize.Width, 
				double.IsInfinity(availableSize.Height) ? 0 : availableSize.Height
			);
		} // func

		protected override Size ArrangeOverride(Size finalSize)
		{
			double y = 0;
			foreach(UIElement child in InternalChildren) // Für jedes interne Kindelement
			{
				y = (GetItemIndexFromChild(child) * itemSize.Height) - offset.Y;
				child.Arrange(new Rect(new Point(0, y), child.DesiredSize)); // Positioniert das Kindelement
			}
			
			viewPort = finalSize;
			return finalSize;
		}

		private void CleanupItems(int minVisibleIndex, int maxVisibleIndex)
		{
			var generator = (IRecyclingItemContainerGenerator)ItemContainerGenerator;

			for (var i = InternalChildren.Count - 1; i >= 0; i--)
			{
				var childPos = new GeneratorPosition(i, 0);
				var itemIndex = generator.IndexFromGeneratorPosition(childPos);

				if (itemIndex < minVisibleIndex || itemIndex > maxVisibleIndex)
				{
					generator.Recycle(childPos, 1);
					RemoveInternalChildRange(i, 1);
				}
			}
		} // proc CleanupItems

		protected override double GetItemOffsetCore(UIElement child) 
			=> base.GetItemOffsetCore(child);

		//protected override void OnItemsChanged(object sender, ItemsChangedEventArgs args) 
		//	=> base.OnItemsChanged(sender, args);

		ScrollViewer IScrollInfo.ScrollOwner
		{
			get => scrollViewer;
			set => scrollViewer = value;
		}

		bool IScrollInfo.CanVerticallyScroll { get => canScrollY; set => canScrollY = value; }
		bool IScrollInfo.CanHorizontallyScroll { get => canScrollX; set => canScrollX = true; }

		double IScrollInfo.HorizontalOffset => offset.X;
		double IScrollInfo.VerticalOffset => offset.Y;
		double IScrollInfo.ViewportWidth => viewPort.Width;
		double IScrollInfo.ViewportHeight => viewPort.Height;

		double IScrollInfo.ExtentWidth => extent.Width;
		double IScrollInfo.ExtentHeight => extent.Height;

		void IScrollInfo.LineDown()
		{
			SetVerticalOffsetCore(offset.Y + ScrollVelocity);
		} // proc IScrollInfo.LineDown()

		void IScrollInfo.LineLeft()
		{
			SetHorizontalOffsetCore(offset.X - ScrollVelocity);
		} // proc IScrollInfo.LineLeft()

		void IScrollInfo.LineRight()
		{
			SetHorizontalOffsetCore(offset.X + ScrollVelocity);
		} // proc IScrollInfo.LineRight()

		void IScrollInfo.LineUp()
		{
			SetVerticalOffsetCore(offset.Y - ScrollVelocity);
		} // proc IScrollInfo.LineUp
		Rect IScrollInfo.MakeVisible(Visual visual, Rect rectangle) => new Rect(0, 0, 0, 0);

		void IScrollInfo.MouseWheelDown()
		{
			SetVerticalOffsetCore(offset.Y + ScrollVelocity);
		} // proc IScrollInfo.MouseWheelDown

		void IScrollInfo.MouseWheelLeft()
		{
			SetHorizontalOffsetCore(offset.X - ScrollVelocity);
		} // proc IScrollInfo.MouseWheelLeft

		void IScrollInfo.MouseWheelRight()
		{
			SetHorizontalOffsetCore(offset.X + ScrollVelocity);
		} // proc IScrollInfo.MouseWheelRight

		void IScrollInfo.MouseWheelUp()
		{
			SetVerticalOffsetCore(offset.Y - ScrollVelocity);
		} // proc IScrollInfo.MouseWheelUp()

		void IScrollInfo.PageDown()
		{
			SetVerticalOffsetCore(offset.Y + viewPort.Height);
		} // proc IScrollInfo.PageDown

		void IScrollInfo.PageLeft()
		{
			SetHorizontalOffsetCore(offset.X - viewPort.Width);
		} // proc IScrollInfo.PageLeft

		void IScrollInfo.PageRight()
		{
			SetHorizontalOffsetCore(offset.X + viewPort.Width);
		} // proc IScrollInfo.PageRight

		void IScrollInfo.PageUp()
		{
			SetVerticalOffsetCore(offset.Y - viewPort.Height);
		} // proc IScrollInfo.PageUp

		void IScrollInfo.SetHorizontalOffset(double offset)
		{
			SetHorizontalOffsetCore(offset);
		} // proc IScrollInfo.SetHorizontalOffset

		void IScrollInfo.SetVerticalOffset(double offset)
		{
			SetVerticalOffsetCore(offset);
		} // proc IScrollInfo.SetVerticalOffset

		#region --- Core Methods ------------------------------------------------------

		private void SetVerticalOffsetCore(double offset) 
		{
			this.offset.Y = Math.Max(0, Math.Min(offset, extent.Height - viewPort.Height));

			// Notify that layout has changed
			scrollViewer.InvalidateScrollInfo();
			InvalidateMeasure();
		} // proc SetVerticalOffsetCore

		private void SetHorizontalOffsetCore(double offset)
		{
			this.offset.X = Math.Max(0, Math.Min(offset, extent.Width - viewPort.Width));

			// Notify that layout has changed
			scrollViewer.InvalidateScrollInfo();
			InvalidateMeasure();
		} // proc SetHorizontalOffsetCore

		private int GetFirstVisibleIndex() 
		{
			var index = (int)Math.Floor(offset.Y / itemSize.Height);
			return Math.Max(0, index);
		} // proc GetFirstVisibleIndex

		private int GetItemIndexFromChild(UIElement child) 
		{
			var c = ItemsControl.GetItemsOwner(this);
			return c.ItemContainerGenerator.IndexFromContainer(child);
		} // proc GetItemIndexFromChild

		#endregion

		private int ScrollVelocity = 0;

	} // class PpsVirtualizationStackPanel
}

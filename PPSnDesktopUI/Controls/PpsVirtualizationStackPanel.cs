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
using System.Text;
using System.Threading.Tasks;
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

		private ItemContainerGenerator generator = null;

		private IItemContainerGenerator GetItemContainerGenerator()
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
			extent = new Size(200, 30000);
			viewPort = availableSize;
			var generator = GetItemContainerGenerator();
			var p = generator.GeneratorPositionFromIndex(0);
			using (generator.StartAt(p, GeneratorDirection.Forward))
			{
				var t = generator.GenerateNext(out var isNew);
				if (t is FrameworkElement m)
				{
					if (isNew)
					{
						generator.PrepareItemContainer(m);
						AddInternalChild(m);
					}
					m.Measure(availableSize);
					var sz = m.DesiredSize;
					return availableSize; // m.DesiredSize;

				}
			}
			return availableSize; // new Size(0, 0);
			
			//else
			//	return base.MeasureOverride(availableSize);
		} // func

		protected override Size ArrangeOverride(Size finalSize)
		{
				var generator = GetItemContainerGenerator();
			var p = generator.GeneratorPositionFromIndex(0);
			var rc = new Rect(0, 0, finalSize.Width, 30);
			using (generator.StartAt(p, GeneratorDirection.Forward))
			{
				while (true)
				{
					var t = generator.GenerateNext(out var isNew);
					if (t is FrameworkElement m)
					{
						if (isNew)
						{
							generator.PrepareItemContainer(m);
							AddInternalChild(m);
						}
						m.Arrange(rc);
						rc.Y += 30;
					}
					else
						break;
				}
			}
			
			viewPort = finalSize;
			return base.ArrangeOverride(finalSize);
		}

		//protected override double GetItemOffsetCore(UIElement child) 
		//	=> base.GetItemOffsetCore(child);

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

		void IScrollInfo.LineDown() => throw new NotImplementedException();
		void IScrollInfo.LineLeft() => throw new NotImplementedException();
		void IScrollInfo.LineRight() => throw new NotImplementedException();
		void IScrollInfo.LineUp() => throw new NotImplementedException();
		Rect IScrollInfo.MakeVisible(Visual visual, Rect rectangle) => new Rect(0, 0, 0, 0);
		void IScrollInfo.MouseWheelDown() => throw new NotImplementedException();
		void IScrollInfo.MouseWheelLeft() => throw new NotImplementedException();
		void IScrollInfo.MouseWheelRight() => throw new NotImplementedException();
		void IScrollInfo.MouseWheelUp() => throw new NotImplementedException();
		void IScrollInfo.PageDown() => throw new NotImplementedException();
		void IScrollInfo.PageLeft() => throw new NotImplementedException();
		void IScrollInfo.PageRight() => throw new NotImplementedException();
		void IScrollInfo.PageUp() => throw new NotImplementedException();
		void IScrollInfo.SetHorizontalOffset(double offset) => throw new NotImplementedException();
		void IScrollInfo.SetVerticalOffset(double offset) => throw new NotImplementedException();
	} // class PpsVirtualizationStackPanel
}

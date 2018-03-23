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
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace TecWare.PPSn.Controls
{
	public class PpsStackSectionPanel : Panel
	{
		public static readonly DependencyProperty TitleBarTemplateProperty = DependencyProperty.Register(nameof(TitleBarTemplate), typeof(DataTemplate), typeof(PpsStackSectionPanel), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsMeasure));

		public DataTemplate TitleBarTemplate { get => (DataTemplate)GetValue(TitleBarTemplateProperty); set => SetValue(TitleBarTemplateProperty, value); }

		public PpsStackSectionPanel()
		{
			SetCurrentValue(TitleBarTemplateProperty, new DataTemplate());
			//TitleBarTemplate = new DataTemplate();
		}

		#region ---- Callbacks ----------------------------------------------------------

		private static void IsOpenChangedCallback(DependencyObject d, DependencyPropertyChangedEventArgs e)
		{
			if (d is Visual v)
			{
				var pa = VisualTreeHelper.GetParent(v);
				if (pa is PpsStackSectionPanel p)
					p.InvalidateMeasure();
			}
		} // proc TitleChangedCallback

		private static void IsEmptyChangedCallback(DependencyObject d, DependencyPropertyChangedEventArgs e)
		{
			if (d is Visual v)
			{
				var pa = VisualTreeHelper.GetParent(v);
				if (pa is PpsStackSectionPanel p)
					p.InvalidateMeasure();
			}
		} // proc TitleChangedCallback

		private static void ContentPresenterTemplateChangedCallback(DependencyObject d, DependencyPropertyChangedEventArgs e)
		{
			if (d is Visual v)
			{
				var pa = VisualTreeHelper.GetParent(v);
				if (pa is PpsStackSectionPanel p)
					p.ChangePresenterTemplate(d, e);
			}
		} // proc TitleChangedCallback

		private void ChangePresenterTemplate(DependencyObject d, DependencyPropertyChangedEventArgs e)
		{
			var element = (UIElement)d;
			var newvalue = (DataTemplate)e.NewValue;

			var presenter = presenterCollection[element];

			if (presenter != null)
			{
				presenter.ContentTemplate = newvalue;
			}
		}

		#endregion

		#region ---- Attached Properties ------------------------------------------------

		#region Title

		/// <summary>DependencyProperty</summary>
		public static readonly DependencyProperty TitleProperty = DependencyProperty.RegisterAttached("Title", typeof(object), typeof(PpsStackSectionPanel));

		/// <summary>Returns the Title of the Control</summary>
		/// <param name="d">Control</param>
		/// <returns></returns>
		public static object GetTitle(DependencyObject d)
			=> (object)d.GetValue(TitleProperty);
		/// <summary>Sets the Title of the Control</summary>
		/// <param name="d">Control</param>
		/// <param name="value"></param>
		public static void SetTitle(DependencyObject d, object value)
			=> d.SetValue(TitleProperty, value);

		#endregion

		#region Subtitle

		/// <summary>DependencyProperty</summary>
		public static readonly DependencyProperty SubtitleProperty = DependencyProperty.RegisterAttached("Subtitle", typeof(object), typeof(PpsStackSectionPanel));

		/// <summary>Returns the Subtitle of the Control</summary>
		/// <param name="d">Control</param>
		/// <returns></returns>
		public static object GetSubtitle(DependencyObject d)
			=> d.GetValue(SubtitleProperty);
		/// <summary>Sets the Subtitle of the Control</summary>
		/// <param name="d">Control</param>
		/// <param name="value"></param>
		public static void SetSubtitle(DependencyObject d, object value)
			=> d.SetValue(SubtitleProperty, value);

		#endregion

		#region IsOpen

		/// <summary>DependencyProperty</summary>
		public static readonly DependencyProperty IsOpenProperty = DependencyProperty.RegisterAttached("IsOpen", typeof(bool), typeof(PpsStackSectionPanel), new FrameworkPropertyMetadata(false, new PropertyChangedCallback(IsOpenChangedCallback)));

		/// <summary>Returns the Subtitle of the Control</summary>
		/// <param name="d">Control</param>
		/// <returns></returns>
		public static bool GetIsOpen(DependencyObject d)
			=> (bool)d.GetValue(IsOpenProperty);
		/// <summary>Sets the Subtitle of the Control</summary>
		/// <param name="d">Control</param>
		/// <param name="value"></param>
		public static void SetIsOpen(DependencyObject d, bool value)
			=> d.SetValue(IsOpenProperty, value);

		#endregion
		
		#region ContentPresenterTemplate

		/// <summary>DependencyProperty</summary>
		public static readonly DependencyProperty ContentPresenterTemplateProperty = DependencyProperty.RegisterAttached("ContentPresenterTemplate", typeof(DataTemplate), typeof(PpsStackSectionPanel), new FrameworkPropertyMetadata(new PropertyChangedCallback(ContentPresenterTemplateChangedCallback)));

		/// <summary>Returns the Title of the Control</summary>
		/// <param name="d">Control</param>
		/// <returns></returns>
		public static DataTemplate GetContentPresenterTemplate(DependencyObject d)
			=> (DataTemplate)d.GetValue(ContentPresenterTemplateProperty);
		/// <summary>Sets the Title of the Control</summary>
		/// <param name="d">Control</param>
		/// <param name="value"></param>
		public static void SetContentPresenterTemplate(DependencyObject d, DataTemplate value)
			=> d.SetValue(ContentPresenterTemplateProperty, value);

		#endregion

		#endregion


		#region ---- Handling of Labels -----------------------------------------------

		#region -- class PpsDataFieldPanelCollection-----------------------------------

		private sealed class PpsDataFieldPanelCollection : UIElementCollection
		{
			private PpsStackSectionPanel panel;

			public PpsDataFieldPanelCollection(UIElement visualParent, FrameworkElement logicalParent)
				: base(visualParent, logicalParent)
			{
				this.panel = (PpsStackSectionPanel)visualParent;
			}

			public override int Add(UIElement element)
			{
				if (!panel.HasPresenter(element) && !(element is ContentPresenter cp && panel.IsPresenter(cp)))
				{
					panel.AddPresenter(element);
				}
				return base.Add(element);
			}

			public override void Remove(UIElement element)
			{
				if (panel.HasPresenter(element))
				{
					panel.RemovePresenter(element);
				}
				base.Remove(element);
			}

			#region ---- Invalid Functions ----------------------------------------------

			public override void Insert(int index, UIElement element)
				=> throw new FieldAccessException();

			public override void RemoveAt(int index)
				=> throw new FieldAccessException();

			public override void RemoveRange(int index, int count)
				=> throw new FieldAccessException();

			public override int IndexOf(UIElement element)
				=> throw new FieldAccessException();

			public override void CopyTo(Array array, int index)
				=> throw new FieldAccessException();

			public override void CopyTo(UIElement[] array, int index)
				=> throw new FieldAccessException();

			#endregion
		} // class class PpsDataFieldPanelCollection

		private void RemovePresenter(DependencyObject d)
		{
			var element = (UIElement)d;

			presenterCollection.Remove(element);
			this.InvalidateMeasure();
		}

		#endregion

		private readonly Dictionary<UIElement, ContentPresenter> presenterCollection = new Dictionary<UIElement, ContentPresenter>();

		private bool IsPresenter(DependencyObject d)
		{
			var element = (ContentPresenter)d;

			return presenterCollection.ContainsValue(element);
		}

		private bool HasPresenter(DependencyObject d)
		{
			var element = (UIElement)d;

			return presenterCollection.ContainsKey(element);
		}

		private void AddPresenter(DependencyObject d)
		{
			var element = (UIElement)d;

			var cp = new ContentPresenter() { ContentTemplate = GetContentPresenterTemplate(element) ?? TitleBarTemplate };

			element.IsVisibleChanged += (s, e) => cp.Visibility = (bool)e.NewValue ? Visibility.Visible : Visibility.Collapsed;

			presenterCollection.Add(element, cp);
			InternalChildren.Add(cp);
			this.InvalidateMeasure();
		}

		/// <summary></summary>
		/// <param name="logicalParent"></param>
		/// <returns></returns>
		protected override UIElementCollection CreateUIElementCollection(FrameworkElement logicalParent)
			=> new PpsDataFieldPanelCollection(this, logicalParent);

		#endregion

		protected override Size MeasureOverride(Size availableSize)
		{
			var height = 0.0;
			foreach (var presenter in presenterCollection)
			{
				if (presenter.Key.IsMeasureValid && presenter.Key.RenderSize.Height <= 0)
					continue;

				presenter.Value.Measure(availableSize);
				height += presenter.Value.DesiredSize.Height;
				if (GetIsOpen(presenter.Key))
				{
					height += presenter.Key.DesiredSize.Height;
				}
			}

			var requestSize = new Size(availableSize.Width, height);

			return requestSize;
		}

		protected override Size ArrangeOverride(Size finalSize)
		{
			var i = 0.0;

			foreach (var item in presenterCollection)
			{
				if (item.Key.IsMeasureValid && item.Key.RenderSize.Height <= 0)
				{
					item.Value.Visibility = Visibility.Collapsed;
					continue;
				}
				else
				{
					item.Value.Visibility = Visibility.Visible;
				}

				if (item.Value != null)
				{
					item.Value.Measure(finalSize);
					var positionRect = new Rect(0, i, finalSize.Width, item.Value.DesiredSize.Height);
					item.Value.Arrange(positionRect);
				}
				i += item.Value.DesiredSize.Height;

				item.Value.DataContext = item.Key;


				if (GetIsOpen(item.Key))
				{
					item.Key.Measure(finalSize);

					var childRect = new Rect(0, i, finalSize.Width, item.Key.DesiredSize.Height);

					item.Key.Arrange(childRect);
					i += childRect.Height;
				}
				else
				{
					var childRect = new Rect(0, 0, 0, 0);
					item.Key.Arrange(childRect);
				}
				i += 5;
			}

			return new Size(finalSize.Width, i);
		}

	}
}

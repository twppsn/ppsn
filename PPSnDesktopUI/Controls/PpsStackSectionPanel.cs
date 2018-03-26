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
	/// <summary>This Panel shows it's children in Expaners/Accordeon style</summary>
	public class PpsStackSectionPanel : Panel
	{
		/// <summary>DependencyProperty</summary>
		public static readonly DependencyProperty TitleBarTemplateProperty = DependencyProperty.Register(nameof(TitleBarTemplate), typeof(DataTemplate), typeof(PpsStackSectionPanel), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsMeasure));

		/// <summary>The Titlebar is the Expander, the Template is mandatory, and must at least handle the IsOpen status.</summary>
		public DataTemplate TitleBarTemplate { get => (DataTemplate)GetValue(TitleBarTemplateProperty); set => SetValue(TitleBarTemplateProperty, value); }

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
		} // proc ChangePresenterTemplate

		#endregion

		#region ---- Attached Properties ------------------------------------------------

		#region Title

		/// <summary>DependencyProperty</summary>
		public static readonly DependencyProperty TitleProperty = DependencyProperty.RegisterAttached("Title", typeof(object), typeof(PpsStackSectionPanel));

		/// <summary>Contains the Title of a ChildItem</summary>
		/// <param name="d">ChildItem</param>
		/// <returns>Main Tile</returns>
		public static object GetTitle(DependencyObject d)
			=> (object)d.GetValue(TitleProperty);
		/// <summary>Sets the Title of a ChildItem</summary>
		/// <param name="d">ChildItem</param>
		/// <param name="value">new Ttile</param>
		public static void SetTitle(DependencyObject d, object value)
			=> d.SetValue(TitleProperty, value);

		#endregion

		#region Subtitle

		/// <summary>DependencyProperty</summary>
		public static readonly DependencyProperty SubtitleProperty = DependencyProperty.RegisterAttached("Subtitle", typeof(object), typeof(PpsStackSectionPanel));
		/// <summary>Returns the Subtitle of a ChildItem</summary>
		/// <param name="d">ChildItem</param>
		/// <returns>Subtitle</returns>
		public static object GetSubtitle(DependencyObject d)
			=> d.GetValue(SubtitleProperty);
		/// <summary>Sets the Subtitle of a ChildItem</summary>
		/// <param name="d">ChildItem</param>
		/// <param name="value">new Subtitle</param>
		public static void SetSubtitle(DependencyObject d, object value)
			=> d.SetValue(SubtitleProperty, value);

		#endregion

		#region IsOpen

		/// <summary>DependencyProperty</summary>
		public static readonly DependencyProperty IsOpenProperty = DependencyProperty.RegisterAttached("IsOpen", typeof(bool), typeof(PpsStackSectionPanel), new FrameworkPropertyMetadata(false, new PropertyChangedCallback(IsOpenChangedCallback)));
		/// <summary>Returns the OpenedState of a ChildItem</summary>
		/// <param name="d">ChildItem</param>
		/// <returns>Opened State</returns>
		public static bool GetIsOpen(DependencyObject d)
			=> (bool)d.GetValue(IsOpenProperty);
		/// <summary>Sets the OpenedState of a ChildItem</summary>
		/// <param name="d">ChildItem</param>
		/// <param name="value">new Opened State</param>
		public static void SetIsOpen(DependencyObject d, bool value)
			=> d.SetValue(IsOpenProperty, value);

		#endregion


		#region ContentPresenterTemplate

		/// <summary>DependencyProperty</summary>
		public static readonly DependencyProperty ContentPresenterTemplateProperty = DependencyProperty.RegisterAttached("ContentPresenterTemplate", typeof(DataTemplate), typeof(PpsStackSectionPanel), new FrameworkPropertyMetadata(new PropertyChangedCallback(ContentPresenterTemplateChangedCallback)));
		/// <summary>Returns the individual TitlebarTemplate</summary>
		/// <param name="d">ChildItem</param>
		/// <returns>individual ContentPresenter</returns>
		public static DataTemplate GetContentPresenterTemplate(DependencyObject d)
			=> (DataTemplate)d.GetValue(ContentPresenterTemplateProperty);
		/// <summary>Sets a individual ContentPresenterTemplate for this ChildItem</summary>
		/// <param name="d">ChildItem</param>
		/// <param name="value">new DataTemplate</param>
		public static void SetContentPresenterTemplate(DependencyObject d, DataTemplate value)
			=> d.SetValue(ContentPresenterTemplateProperty, value);

		#endregion

		#endregion

		#region ---- Handling of Labels -------------------------------------------------

		#region ---- class PpsDataFieldPanelCollection ----------------------------------

		/// <summary>The standart UIElementCollection is overload to attach the ContentPresenters to the UIElements</summary>
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
				// if the item is a new Control, create a ContentPresenter for it
				if (!panel.HasPresenter(element) && !(element is ContentPresenter cp && panel.IsPresenter(cp)))
				{
					panel.AddPresenter(element);
				}
				// add the item to the Children of the Panel
				return base.Add(element);
			}

			public override void Remove(UIElement element)
			{
				// if a UIElement is deleted - also delete its Presenter
				if (panel.HasPresenter(element))
				{
					panel.RemovePresenter(element);
				}
				base.Remove(element);
			}

			#region ---- Invalid Functions ----------------------------------------------

			/// <summary>Do not use - indexes are invalid</summary>
			public override void Insert(int index, UIElement element)
				=> throw new FieldAccessException();

			/// <summary>Do not use - indexes are invalid</summary>
			public override void RemoveAt(int index)
				=> throw new FieldAccessException();

			/// <summary>Do not use - indexes are invalid</summary>
			public override void RemoveRange(int index, int count)
				=> throw new FieldAccessException();

			/// <summary>Do not use - indexes are invalid</summary>
			public override int IndexOf(UIElement element)
				=> throw new FieldAccessException();

			/// <summary>Do not use - indexes are invalid</summary>
			public override void CopyTo(Array array, int index)
				=> throw new FieldAccessException();

			/// <summary>Do not use - indexes are invalid</summary>
			public override void CopyTo(UIElement[] array, int index)
				=> throw new FieldAccessException();

			#endregion
		} // class PpsDataFieldPanelCollection

		#endregion

		// this is the hidden List connecting the UIElements with their ContentPresenters
		private readonly Dictionary<UIElement, ContentPresenter> presenterCollection = new Dictionary<UIElement, ContentPresenter>();

		private bool IsPresenter(DependencyObject d)
		{
			var element = (ContentPresenter)d;

			return presenterCollection.ContainsValue(element);
		} // func IsPresenter

		private bool HasPresenter(DependencyObject d)
		{
			var element = (UIElement)d;

			return presenterCollection.ContainsKey(element);
		} // func HasPresener

		private void AddPresenter(DependencyObject d)
		{
			var element = (UIElement)d;

			var cp = new ContentPresenter() { ContentTemplate = GetContentPresenterTemplate(element) ?? TitleBarTemplate };

			// the Eventhandler is attached to hide the ContentPresenter if the UIElement is hidden
			element.IsVisibleChanged += (s, e) => cp.Visibility = (bool)e.NewValue ? Visibility.Visible : Visibility.Collapsed;

			presenterCollection.Add(element, cp);
			InternalChildren.Add(cp);
			this.InvalidateMeasure();
		} // proc AddPresenter

		private void RemovePresenter(DependencyObject d)
		{
			var element = (UIElement)d;

			presenterCollection.Remove(element);
			this.InvalidateMeasure();
		} // proc RemovePresenter

		/// <summary>The Collection has to be overriden to handle the Creation of ContentPreseners</summary>
		/// <param name="logicalParent"></param>
		/// <returns></returns>
		protected override UIElementCollection CreateUIElementCollection(FrameworkElement logicalParent)
			=> new PpsDataFieldPanelCollection(this, logicalParent);

		#endregion

		/// <summary>Function to estimate the needed Space of the Control</summary>
		/// <param name="availableSize">maximum space</param>
		/// <returns>the estimated Size of the Control</returns>
		protected override Size MeasureOverride(Size availableSize)
		{
			var height = 0.0;

			// the Children of the Control contains both Presenter an UIElements, so enumerate the Presenter
			foreach (var presenter in presenterCollection)
			{
				// if a UIElement does not request any vertical space, it is empty thus not shown
				if (presenter.Key.IsMeasureValid && presenter.Key.RenderSize.Height <= 0)
					continue;

				presenter.Value.Measure(availableSize);
				height += presenter.Value.DesiredSize.Height;
				// if the item is set to collapsed, it does not need vertical space
				if (GetIsOpen(presenter.Key))
				{
					height += presenter.Key.DesiredSize.Height;
				}
			}

			var requestSize = new Size(availableSize.Width, height);

			return requestSize;
		}

		/// <summary>Function to arrange the Children and their Expanders</summary>
		/// <param name="finalSize">vertical Space the Control should use</param>
		/// <returns>>true needed vertical space</returns>
		protected override Size ArrangeOverride(Size finalSize)
		{
			var verticalposition = 0.0;

			foreach (var presenter in presenterCollection)
			{
				// if a UIElement does not request any vertical space, it is empty thus not shown
				if (presenter.Key.IsMeasureValid && presenter.Key.RenderSize.Height <= 0)
				{
					presenter.Value.Visibility = Visibility.Collapsed;
					continue;
				}
				else
				{
					presenter.Value.Visibility = Visibility.Visible;
				}

				if (presenter.Value != null)
				{
					presenter.Value.Measure(finalSize);
					var positionRect = new Rect(0, verticalposition, finalSize.Width, presenter.Value.DesiredSize.Height);
					presenter.Value.Arrange(positionRect);

					presenter.Value.DataContext = presenter.Key;

					verticalposition += presenter.Value.DesiredSize.Height;
				}

				// if the Item is not Collapsed, show it
				if (GetIsOpen(presenter.Key))
				{
					presenter.Key.Measure(finalSize);

					var childRect = new Rect(0, verticalposition, finalSize.Width, presenter.Key.DesiredSize.Height);

					presenter.Key.Arrange(childRect);
					verticalposition += childRect.Height;
				}
				else
				{
					// otherwise do not show it
					var childRect = new Rect(0, 0, 0, 0);
					presenter.Key.Arrange(childRect);
				}

				// add a spacer between Items
				verticalposition += 5;
			}

			return new Size(finalSize.Width, verticalposition);
		} // func ArrangeOverride
	}
}

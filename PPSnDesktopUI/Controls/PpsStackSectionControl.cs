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
using System.Linq;
using System.Collections;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace TecWare.PPSn.Controls
{
	/*
	#region ---- Enum ExpanderStyles ----------------------------------------------------

	/// <summary>these Styles are used to enrich the Default Behavior of Collapsing/Expanding</summary>
	public enum ExpanderStyles
	{
		/// <summary>All items are open at the beginning, the user may close any</summary>
		AllOpen,
		/// <summary>All items are closed at the beginning, the user may expand any</summary>
		AllClosed,
		/// <summary>Only the first item is open at the beginning, if the user expands an item, all other will be closed</summary>
		Accordeon
	}

	#endregion
	*/

	#region -- class PpsStackSectionControl -------------------------------------------

	/// <summary></summary>
	public class PpsStackSectionControl : ItemsControl
	{
		/// <summary>DependencyProperty</summary>
		public static readonly DependencyProperty HeaderTemplateProperty = DependencyProperty.Register(nameof(HeaderTemplate), typeof(DataTemplate), typeof(PpsStackSectionControl), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsMeasure));
		/// <summary>The Titlebar is the Expander, the Template is mandatory, and must at least handle the IsOpen status.</summary>
		public DataTemplate HeaderTemplate { get => (DataTemplate)GetValue(HeaderTemplateProperty); set => SetValue(HeaderTemplateProperty, value); }

		/// <summary>DependencyProperty</summary>
		public static readonly DependencyProperty VerticalMarginProperty = DependencyProperty.Register(nameof(VerticalMargin), typeof(double), typeof(PpsStackSectionControl), new FrameworkPropertyMetadata(5.0, FrameworkPropertyMetadataOptions.AffectsMeasure));
		/// <summary>The Vertical Margin is inserted between each Presenter</summary>
		public double VerticalMargin { get => (double)GetValue(VerticalMarginProperty); set => SetValue(VerticalMarginProperty, value); }

		/// <summary>DependencyProperty</summary>
		public static readonly DependencyProperty ExpanderStyleProperty = DependencyProperty.Register(nameof(ExpanderStyle), typeof(ExpanderStyles), typeof(PpsStackSectionControl), new FrameworkPropertyMetadata(ExpanderStyles.AllClosed, FrameworkPropertyMetadataOptions.AffectsMeasure));
		/// <summary>The Style of the Expanders</summary>
		public ExpanderStyles ExpanderStyle { get => (ExpanderStyles)GetValue(ExpanderStyleProperty); set => SetValue(ExpanderStyleProperty, value); }

		/// <summary></summary>
		static PpsStackSectionControl()
		{
			DefaultStyleKeyProperty.OverrideMetadata(typeof(PpsStackSectionControl), new FrameworkPropertyMetadata(typeof(PpsStackSectionControl)));
		}

		/// <summary>Function override to allow Templating of UIElements</summary>
		/// <param name="item">unused</param>
		/// <returns></returns>
		protected override bool IsItemItsOwnContainerOverride(object item)
		{
			return false;
		}

		/// <summary>Procedure override to template the Items with ItemTemplate</summary>
		/// <param name="element">Parent PpsStackSectionControl</param>
		/// <param name="item">Single Item to Template</param>
		protected override void PrepareContainerForItemOverride(DependencyObject element, object item)
		{
			base.PrepareContainerForItemOverride(element, item);
			((ContentPresenter)element).ContentTemplate = ItemTemplate;
		}


		#region ---- Attached Properties ------------------------------------------------

		#region Title

		/// <summary>DependencyProperty</summary>
		public static readonly DependencyProperty TitleProperty = DependencyProperty.RegisterAttached("Title", typeof(object), typeof(PpsStackSectionControl));

		/// <summary>Contains the Title of a ChildItem</summary>
		/// <param name="d">ChildItem</param>
		/// <returns>Main Tile</returns>
		public static object GetTitle(DependencyObject d)
			=> d.GetValue(TitleProperty);
		/// <summary>Sets the Title of a ChildItem</summary>
		/// <param name="d">ChildItem</param>
		/// <param name="value">new Ttile</param>
		public static void SetTitle(DependencyObject d, object value)
			=> d.SetValue(TitleProperty, value);

		#endregion

		#region Subtitle

		/// <summary>DependencyProperty</summary>
		public static readonly DependencyProperty SubtitleProperty = DependencyProperty.RegisterAttached("Subtitle", typeof(object), typeof(PpsStackSectionControl));
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
		public static readonly DependencyProperty IsOpenProperty = DependencyProperty.RegisterAttached("IsOpen", typeof(bool), typeof(PpsStackSectionControl), new FrameworkPropertyMetadata(false, new PropertyChangedCallback(IsOpenChangedCallback)));
		/// <summary>Returns the OpenedState of a ChildItem</summary>
		/// <param name="d">ChildItem</param>
		/// <returns>Opened State</returns>
		public static bool GetIsOpen(DependencyObject d)
			=> BooleanBox.GetBool(d.GetValue(IsOpenProperty));
		/// <summary>Sets the OpenedState of a ChildItem</summary>
		/// <param name="d">ChildItem</param>
		/// <param name="value">new Opened State</param>
		public static void SetIsOpen(DependencyObject d, bool value)
			=> d.SetValue(IsOpenProperty, BooleanBox.GetObject(value));
		private static void IsOpenChangedCallback(DependencyObject d, DependencyPropertyChangedEventArgs e)
		{
			var pss = FindParentStackSection(d);
			if (pss != null)
			{
				SetIsOpen(pss, BooleanBox.GetBool(e.NewValue));

				FindParentStackSectionControl(d)?.ChangeIsOpen(pss, e);
			}
		} // proc IsOpenChangedCallback

		private static PpsStackSectionControl FindParentStackSectionControl(DependencyObject d)
		{
			if (d is Visual v)
			{
				var parent = VisualTreeHelper.GetParent(v);
				while (!(parent is PpsStackSectionControl) && parent != null)
					parent = VisualTreeHelper.GetParent(parent);
				if (parent is PpsStackSectionControl pssc)
					return pssc;
			}

			return null;
		} // func FindParentStackSectionControl

		private static PpsStackSectionItem FindParentStackSection(DependencyObject d)
		{
			if (d is Visual v)
			{
				var parent = VisualTreeHelper.GetParent(v);
				while (!(parent is PpsStackSectionItem) && parent != null)
					parent = VisualTreeHelper.GetParent(parent);
				if (parent is PpsStackSectionItem pss)
					return pss;
			}

			return null;
		} // func FindParentStackSection

		private void ChangeIsOpen(DependencyObject d, DependencyPropertyChangedEventArgs e)
		{
			var element = (UIElement)d;
			var newvalue = (bool)e.NewValue;

			if (ExpanderStyle == ExpanderStyles.Accordeon && newvalue)
			{
				var openElements = from DependencyObject uielement in Items where GetIsOpen(FindParentStackSection(uielement)) select FindParentStackSection(uielement);

				foreach (var openElement in openElements)
					if (openElement != element)
						SetIsOpen(openElement, false);
			}
		} // proc ChangeIsOpen
	}
	
	#endregion

	#region -- class PpsStackSectionItem ----------------------------------------------

	/// <summary>Item that represent a section of the PpsStackSectionControl.</summary>
	public class PpsStackSectionItem : HeaderedContentControl
	{
		#region -- Properties --------------------------------------------------------

		/// <summary></summary>
		public static readonly DependencyProperty SubHeaderProperty = DependencyProperty.Register(nameof(SubHeader), typeof(object), typeof(PpsStackSectionItem), new FrameworkPropertyMetadata(null, new PropertyChangedCallback(OnUpdateSubHeader)));
		/// <summary></summary>
		public static readonly DependencyProperty SubHeaderTemplateProperty = DependencyProperty.Register(nameof(SubHeaderTemplate), typeof(DataTemplate), typeof(PpsStackSectionItem), new FrameworkPropertyMetadata(null));
		/// <summary></summary>
		public static readonly DependencyProperty SubHeaderTemplateSelectorProperty = DependencyProperty.Register(nameof(SubHeaderTemplateSelector), typeof(DataTemplateSelector), typeof(PpsStackSectionItem), new FrameworkPropertyMetadata(null));
		/// <summary></summary>
		public static readonly DependencyProperty SubHeaderStringFormatProperty = DependencyProperty.Register(nameof(SubHeaderStringFormat), typeof(string), typeof(PpsStackSectionItem), new FrameworkPropertyMetadata(null));

		private static readonly DependencyPropertyKey hasSubHeaderPropertyKey = DependencyProperty.RegisterReadOnly(nameof(HasSubHeader), typeof(bool), typeof(PpsStackSectionItem), new FrameworkPropertyMetadata(BooleanBox.False));
		/// <summary></summary>
		public static readonly DependencyProperty HasSubHeaderProperty = hasSubHeaderPropertyKey.DependencyProperty;

		/// <summary></summary>
		public object SubHeader { get => GetValue(SubHeaderProperty); set => SetValue(SubHeaderProperty, value); }
		/// <summary></summary>
		public DataTemplate SubHeaderTemplate { get => (DataTemplate)GetValue(SubHeaderTemplateProperty); set => SetValue(SubHeaderTemplateProperty, value); }
		/// <summary></summary>
		public DataTemplateSelector SubHeaderTemplateSelector { get => (DataTemplateSelector)GetValue(SubHeaderTemplateSelectorProperty); set => SetValue(SubHeaderTemplateSelectorProperty, value); }
		/// <summary></summary>
		public string SubHeaderStringFormat { get => (string)GetValue(SubHeaderStringFormatProperty); set => SetValue(SubHeaderStringFormatProperty, value); }
		/// <summary></summary>
		public bool HasSubHeader { get => BooleanBox.GetBool(GetValue(HasSubHeaderProperty)); private set => SetValue(hasSubHeaderPropertyKey, BooleanBox.GetObject(value)); }

		private static void OnUpdateSubHeader(DependencyObject d, DependencyPropertyChangedEventArgs e)
		{
			var ctrl = (PpsStackSectionItem)d;
			ctrl.HasSubHeader = e.NewValue != null;
			ctrl.OnUpdateSubHeader(e.NewValue, e.OldValue);
		} // proc OnUpdateSubHeader

		#endregion

		/// <summary></summary>
		/// <param name="newValue"></param>
		/// <param name="oldValue"></param>
		protected virtual void OnUpdateSubHeader(object newValue, object oldValue)
		{
			RemoveLogicalChild(oldValue);
			AddLogicalChild(newValue);
		} // proc OnUpdateSubHeader

		/// <summary></summary>
		protected override IEnumerator LogicalChildren
			=> LogicalElementEnumerator.GetLogicalEnumerator(this, base.LogicalChildren, () => SubHeader);

		/// <summary></summary>
		public static readonly DependencyProperty IsOpenProperty = DependencyProperty.Register(nameof(IsOpen), typeof(bool), typeof(PpsStackSectionItem));
		/// <summary></summary>
		public bool IsOpen { get => BooleanBox.GetBool(GetValue(IsOpenProperty)); set => SetValue(IsOpenProperty, value); }
	} // class PpsStackSectionItem

	#endregion
}

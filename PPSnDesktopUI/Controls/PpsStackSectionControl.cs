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
using System.Windows;
using System.Windows.Controls;
using TecWare.PPSn.UI;

namespace TecWare.PPSn.Controls
{
	#region ---- Enum ExpanderStyles --------------------------------------------------

	/// <summary>these Styles are used to enrich the Default Behavior of Collapsing/Expanding</summary>
	public enum ExpanderStyles
	{
		/// <summary>All items are open at the beginning, the user may close any</summary>
		AllOpen,
		/// <summary>All items are closed at the beginning, the user may expand any</summary>
		AllClosed,
		/// <summary>Only the first item is open at the beginning, if the user expands an item, all other will be closed</summary>
		Accordeon
	} // enum ExpanderStyles

	#endregion

	#region -- class PpsStackSectionControl -------------------------------------------

	/// <summary></summary>
	public class PpsStackSectionControl : ItemsControl
	{
		#region ---- DependencyProperties -----------------------------------------------

		/// <summary>DependencyProperty</summary>
		public static readonly DependencyProperty ExpanderStyleProperty = DependencyProperty.Register(nameof(ExpanderStyle), typeof(ExpanderStyles), typeof(PpsStackSectionControl), new FrameworkPropertyMetadata(ExpanderStyles.AllClosed, FrameworkPropertyMetadataOptions.AffectsMeasure));
		/// <summary>The Style of the Expanders</summary>
		public ExpanderStyles ExpanderStyle { get => (ExpanderStyles)GetValue(ExpanderStyleProperty); set => SetValue(ExpanderStyleProperty, value); }
		/// <summary>The Template for the Expander</summary>
		public static readonly DependencyProperty ExpanderTemplateProperty = DependencyProperty.Register(nameof(ExpanderTemplate), typeof(ControlTemplate), typeof(PpsStackSectionControl));
		/// <summary>The Template for the Expander</summary>
		public ControlTemplate ExpanderTemplate { get => (ControlTemplate)GetValue(ExpanderTemplateProperty); set => SetValue(ExpanderTemplateProperty, value); }

		#endregion DependencyProperties

		#region ---- Handler ------------------------------------------------------------

		/// <summary>
		/// Called when a PpsStackSectionItem is expanded - handles the AccordeonStyle</summary>
		/// <param name="item">Expanded PpsStackSectionItem</param>
		public void OnItemExpanded(PpsStackSectionItem item)
		{
			if (ExpanderStyle == ExpanderStyles.Accordeon)
			{
				//foreach (var cur in Items)
				//	item.IsExpanded = item == cur;
			}
		}

		#endregion Handler
	}

	#endregion PpsStackSectionControl

	#region -- class PpsStackSectionItem ----------------------------------------------

	/// <summary>Item that represent a section of the PpsStackSectionControl.</summary>
	public class PpsStackSectionItem : Expander
	{
		#region -- Properties --------------------------------------------------------

		/// <summary>Content of the Subheader</summary>
		public static readonly DependencyProperty SubHeaderProperty = DependencyProperty.Register(nameof(SubHeader), typeof(object), typeof(PpsStackSectionItem), new FrameworkPropertyMetadata(null, new PropertyChangedCallback(OnSubHeaderChanged)));

		private static void OnSubHeaderChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
		{
			var item = (PpsStackSectionItem)d;
			item.HasSubHeader = e.NewValue != null;
		} // OnSubHeaderChanged

		/// <summary>Content of the Subheader</summary>
		public object SubHeader { get => GetValue(SubHeaderProperty); set => SetValue(SubHeaderProperty, value); }

		/// <summary>Template for the Subheader</summary>
		public static readonly DependencyProperty SubHeaderTemplateProperty = DependencyProperty.Register(nameof(SubHeaderTemplate), typeof(DataTemplate), typeof(PpsStackSectionItem), new FrameworkPropertyMetadata(null));
		/// <summary>Template for the Subheader</summary>
		public DataTemplate SubHeaderTemplate { get => (DataTemplate)GetValue(SubHeaderTemplateProperty); set => SetValue(SubHeaderTemplateProperty, value); }

		/// <summary>TemplateSelector for the Subheader</summary>
		public static readonly DependencyProperty SubHeaderTemplateSelectorProperty = DependencyProperty.Register(nameof(SubHeaderTemplateSelector), typeof(DataTemplateSelector), typeof(PpsStackSectionItem), new FrameworkPropertyMetadata(null));
		/// <summary>TemplateSelector for the Subheader</summary>
		public DataTemplateSelector SubHeaderTemplateSelector { get => (DataTemplateSelector)GetValue(SubHeaderTemplateSelectorProperty); set => SetValue(SubHeaderTemplateSelectorProperty, value); }

		/// <summary>StringFormat for the Subheader</summary>
		public static readonly DependencyProperty SubHeaderStringFormatProperty = DependencyProperty.Register(nameof(SubHeaderStringFormat), typeof(string), typeof(PpsStackSectionItem), new FrameworkPropertyMetadata(null));
		/// <summary>StringFormat for the Subheader</summary>
		public string SubHeaderStringFormat { get => (string)GetValue(SubHeaderStringFormatProperty); set => SetValue(SubHeaderStringFormatProperty, value); }

		/// <summary>Returns if a Subheader is set</summary>
		private static readonly DependencyPropertyKey hasSubheaderPropertyKey = DependencyProperty.RegisterReadOnly(nameof(HasSubHeader), typeof(bool), typeof(PpsStackSectionItem), new FrameworkPropertyMetadata(BooleanBox.False));
		/// <summary>Returns if a Subheader is set</summary>
		public static readonly DependencyProperty HasSubheaderProperty = hasSubheaderPropertyKey.DependencyProperty;
		/// <summary>Returns if a Subheader is set</summary>
		public bool HasSubHeader { get => BooleanBox.GetBool(GetValue(HasSubheaderProperty)); private set => SetValue(hasSubheaderPropertyKey, BooleanBox.GetObject(value)); }

		#endregion

		/// <summary>Overridden to enqueue the Subheader into the LogigalChildren</summary>
		protected override IEnumerator LogicalChildren
			=> PpsLogicalContentEnumerator.GetLogicalEnumerator(this, base.LogicalChildren, () => SubHeader);
		
		#region ---- Handler ------------------------------------------------------------

		/// <summary>Overridden to support AccordeonStyle</summary>
		protected override void OnExpanded()
		{
			base.OnExpanded();
			StackSectionControl.OnItemExpanded(this);
		} // proc OnExpanded

		/// <summary>Overridden to support ExpanderStyles</summary>
		/// <param name="e"></param>
		protected override void OnInitialized(EventArgs e)
		{
			base.OnInitialized(e);

			IsExpanded = StackSectionControl.ExpanderStyle == ExpanderStyles.AllOpen;
		} // proc OnInitialized

		#endregion Handler

		private PpsStackSectionControl StackSectionControl
			=> (PpsStackSectionControl)ItemsControl.ItemsControlFromItemContainer(this);

		static PpsStackSectionItem()
		{
			DefaultStyleKeyProperty.OverrideMetadata(typeof(PpsStackSectionItem), new FrameworkPropertyMetadata(typeof(PpsStackSectionItem)));
		}
	} // class PpsStackSectionItem

	#endregion PpsStackSectionItem
}

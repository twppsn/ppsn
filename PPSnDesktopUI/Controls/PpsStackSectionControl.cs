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

	#region -- class PpsStackSectionControl -------------------------------------------

	/// <summary></summary>
	public class PpsStackSectionControl : ItemsControl
	{
		/// <summary>DependencyProperty</summary>
		public static readonly DependencyProperty ExpanderStyleProperty = DependencyProperty.Register(nameof(ExpanderStyle), typeof(ExpanderStyles), typeof(PpsStackSectionControl), new FrameworkPropertyMetadata(ExpanderStyles.AllClosed, FrameworkPropertyMetadataOptions.AffectsMeasure));
		/// <summary>The Style of the Expanders</summary>
		public ExpanderStyles ExpanderStyle { get => (ExpanderStyles)GetValue(ExpanderStyleProperty); set => SetValue(ExpanderStyleProperty, value); }

		public static readonly DependencyProperty ExpanderTemplateProperty = DependencyProperty.Register(nameof(ExpanderTemplate), typeof(ControlTemplate), typeof(PpsStackSectionControl));
		/// <summary>The Style of the Expanders</summary>
		public ControlTemplate ExpanderTemplate { get => (ControlTemplate)GetValue(ExpanderTemplateProperty); set => SetValue(ExpanderTemplateProperty, value); }

		public void OnItemExpanded(DependencyObject d)
		{
			if (ExpanderStyle == ExpanderStyles.Accordeon)
			{
				var openedItems = from PpsStackSectionItem item in Items where ((item.IsExpanded == true) && (item != (PpsStackSectionItem)d)) select item;
				foreach (var item in openedItems)
					item.IsExpanded = false;
			}
		}
		
		/// <summary></summary>
		static PpsStackSectionControl()
		{
			//DefaultStyleKeyProperty.OverrideMetadata(typeof(PpsStackSectionControl), new FrameworkPropertyMetadata(typeof(PpsStackSectionControl)));
		}

	}

	#endregion

	#region -- class PpsStackSectionItem ----------------------------------------------

	/// <summary>Item that represent a section of the PpsStackSectionControl.</summary>
	public class PpsStackSectionItem : Expander
	{
		#region -- Properties --------------------------------------------------------

		/// <summary></summary>
		public static readonly DependencyProperty SubheaderProperty = DependencyProperty.Register(nameof(Subheader), typeof(object), typeof(PpsStackSectionItem), new FrameworkPropertyMetadata(null));
		/// <summary></summary>
		public static readonly DependencyProperty SubheaderTemplateProperty = DependencyProperty.Register(nameof(SubheaderTemplate), typeof(DataTemplate), typeof(PpsStackSectionItem), new FrameworkPropertyMetadata(null));
		/// <summary></summary>
		public static readonly DependencyProperty SubheaderTemplateSelectorProperty = DependencyProperty.Register(nameof(SubheaderTemplateSelector), typeof(DataTemplateSelector), typeof(PpsStackSectionItem), new FrameworkPropertyMetadata(null));
		/// <summary></summary>
		public static readonly DependencyProperty SubheaderStringFormatProperty = DependencyProperty.Register(nameof(SubheaderStringFormat), typeof(string), typeof(PpsStackSectionItem), new FrameworkPropertyMetadata(null));

		private static readonly DependencyPropertyKey hasSubheaderPropertyKey = DependencyProperty.RegisterReadOnly(nameof(HasSubheader), typeof(bool), typeof(PpsStackSectionItem), new FrameworkPropertyMetadata(BooleanBox.False));
		/// <summary></summary>
		public static readonly DependencyProperty HasSubheaderProperty = hasSubheaderPropertyKey.DependencyProperty;

		/// <summary></summary>
		public object Subheader
		{
			get => GetValue(SubheaderProperty);
			set => SetValue(SubheaderProperty, value);
		}
		/// <summary></summary>
		public DataTemplate SubheaderTemplate { get => (DataTemplate)GetValue(SubheaderTemplateProperty); set => SetValue(SubheaderTemplateProperty, value); }
		/// <summary></summary>
		public DataTemplateSelector SubheaderTemplateSelector { get => (DataTemplateSelector)GetValue(SubheaderTemplateSelectorProperty); set => SetValue(SubheaderTemplateSelectorProperty, value); }
		/// <summary></summary>
		public string SubheaderStringFormat { get => (string)GetValue(SubheaderStringFormatProperty); set => SetValue(SubheaderStringFormatProperty, value); }
		/// <summary></summary>
		public bool HasSubheader { get => BooleanBox.GetBool(GetValue(HasSubheaderProperty)); private set => SetValue(hasSubheaderPropertyKey, BooleanBox.GetObject(value)); }

		private static void OnUpdateSubheader(DependencyObject d, DependencyPropertyChangedEventArgs e)
		{
			var ctrl = (PpsStackSectionItem)d;
			ctrl.HasSubheader = e.NewValue != null;
			ctrl.OnUpdateSubheader(e.NewValue, e.OldValue);
		} // proc OnUpdateSubheader

		#endregion

		/// <summary></summary>
		/// <param name="newValue"></param>
		/// <param name="oldValue"></param>
		protected virtual void OnUpdateSubheader(object newValue, object oldValue)
		{
			RemoveLogicalChild(oldValue);
			AddLogicalChild(newValue);
		} // proc OnUpdateSubheader

		protected override IEnumerator LogicalChildren
		{
			get => LogicalElementEnumerator.GetLogicalEnumerator(this, base.LogicalChildren, () => Subheader);
		}

		protected override void OnExpanded()
		{
			base.OnExpanded();
			((PpsStackSectionControl)this.GetLogicalParent()).OnItemExpanded(this);
		}

		protected override void OnInitialized(EventArgs e)
		{
			base.OnInitialized(e);
			if (((PpsStackSectionControl)this.GetLogicalParent()).ExpanderStyle == ExpanderStyles.AllOpen)
				this.IsExpanded = true;
			else
				this.IsExpanded = false;
		}

		public PpsStackSectionItem()
		{

		}

		static PpsStackSectionItem()
		{
			DefaultStyleKeyProperty.OverrideMetadata(typeof(PpsStackSectionItem), new FrameworkPropertyMetadata(typeof(PpsStackSectionItem)));
		}
	} // class PpsStackSectionItem

	#endregion PpsStackSectionItem
}

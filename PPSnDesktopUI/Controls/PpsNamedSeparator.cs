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
using System.Collections;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using TecWare.PPSn.UI;

namespace TecWare.PPSn.Controls
{
	/// <summary></summary>
	[ContentProperty(name: nameof(Content))]
	public class PpsNamedSeparator : Separator
	{
		#region ---- DependencyProperties -----------------------------------------------
		/// <summary>Content/Header of the Separator</summary>
		public static readonly DependencyProperty ContentProperty = ContentControl.ContentProperty.AddOwner(typeof(PpsNamedSeparator), new FrameworkPropertyMetadata(null, new PropertyChangedCallback(OnContentChanged)));
		/// <summary>Template for the Content/Header of the Separator</summary>
		public static readonly DependencyProperty ContentTemplateProperty = ContentControl.ContentTemplateProperty.AddOwner(typeof(PpsNamedSeparator), new FrameworkPropertyMetadata(null));
		/// <summary>Selector for the ContentTemplate </summary>
		public static readonly DependencyProperty ContentTemplateSelectorProperty = ContentControl.ContentTemplateSelectorProperty.AddOwner(typeof(PpsNamedSeparator), new FrameworkPropertyMetadata(null));
		/// <summary>StringFormat for the Content/Header</summary>
		public static readonly DependencyProperty ContentStringFormatProperty = ContentControl.ContentStringFormatProperty.AddOwner(typeof(PpsNamedSeparator), new FrameworkPropertyMetadata(null));

		private static void OnContentChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
		{
			var pns = (PpsNamedSeparator)d;
			pns.OnContentChanged(e.NewValue, e.OldValue);
		} // proc OnContentChanged

		/// <summary>Content/Header of the Separator</summary>
		public object Content { get => GetValue(ContentProperty); set => SetValue(ContentProperty, value); }
		/// <summary>Template for the Content/Header of the Separator</summary>
		public DataTemplate ContentTemplate { get => (DataTemplate)GetValue(ContentTemplateProperty); set => SetValue(ContentTemplateProperty, value); }
		/// <summary>Selector for the ContentTemplate </summary>
		public DataTemplateSelector ContentTemplateSelector { get => (DataTemplateSelector)GetValue(ContentTemplateSelectorProperty); set => SetValue(ContentTemplateSelectorProperty, value); }
		/// <summary>StringFormat for the Content/Header</summary>
		public string ContentStringFormat { get => (string)GetValue(ContentStringFormatProperty); set => SetValue(ContentStringFormatProperty, value); }

		#endregion DependencyProperties

		/// <summary></summary>
		/// <param name="newValue"></param>
		/// <param name="oldValue"></param>
		public void OnContentChanged(object newValue, object oldValue)
		{
			RemoveLogicalChild(oldValue);
			AddLogicalChild(newValue);
		} // proc OnContentChanged

		/// <summary></summary>
		protected override IEnumerator LogicalChildren 
			=> PpsLogicalContentEnumerator.GetLogicalEnumerator(this, base.LogicalChildren, () => Content);
		
		static PpsNamedSeparator()
		{
			DefaultStyleKeyProperty.OverrideMetadata(typeof(PpsNamedSeparator), new FrameworkPropertyMetadata(typeof(PpsNamedSeparator)));
		}
	} // class PpsNamedSeparator
}

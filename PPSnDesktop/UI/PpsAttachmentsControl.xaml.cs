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
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using TecWare.DE.Data;
using TecWare.PPSn.Data;

namespace TecWare.PPSn.UI
{
	#region -- class PpsAttachmentsControl --------------------------------------------

	/// <summary>Control to view and edit attachments.</summary>
	public partial class PpsAttachmentsControl : UserControl
	{
		#region -- AttachmentsSource - property ---------------------------------------

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
		public static readonly DependencyProperty AttachmentsSourceProperty = DependencyProperty.Register(nameof(AttachmentsSource), typeof(IPpsAttachments), typeof(PpsAttachmentsControl), new FrameworkPropertyMetadata(null, new PropertyChangedCallback(OnAttachmentsSourceChanged)));
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

		private static void OnAttachmentsSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
			=> ((PpsAttachmentsControl)d).OnAttachmentsSourceChanged((IPpsAttachments)e.NewValue, (IPpsAttachments)e.OldValue);

		private void OnAttachmentsSourceChanged(IPpsAttachments newValue, IPpsAttachments oldValue)
		{
			zusaList.ItemsSource = newValue;
		} // proc OnAttachmentsSourceChanged

		/// <summary>Attachment source.</summary>
		public IPpsAttachments AttachmentsSource { get => (IPpsAttachments)GetValue(AttachmentsSourceProperty); set => SetValue(AttachmentsSourceProperty, value); }

		#endregion

		/// <summary></summary>
		public PpsAttachmentsControl()
		{
			InitializeComponent();
		} // ctor
	} // class PpsAttachmentsControl

	#endregion
}

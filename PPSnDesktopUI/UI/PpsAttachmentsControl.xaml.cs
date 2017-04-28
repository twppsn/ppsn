using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using TecWare.PPSn.Data;

namespace TecWare.PPSn.UI
{
	public interface IPpsAttachmentItem
	{
		bool Remove();
	} // interface IPpsAttachmentItem

	public interface IPpsAttachments : IEnumerable<IPpsAttachmentItem>
	{
	} // interface IPpsAttachments

	public interface IPpsAttachmentSource
	{
		IPpsAttachments Attachments { get; }
	} // interface IPpsAttachmentSource

	  /// <summary>
	  /// Interaction logic for PpsAttachmentsControl.xaml
	  /// </summary>
	public partial class PpsAttachmentsControl : UserControl, IPpsAttachmentSource
	{
		public PpsAttachmentsControl()
		{
			InitializeComponent();

			CommandBindings.Add(
				new CommandBinding(RemoveAttachmentCommand,
					(sender, e) =>
					{
						var itm = (PpsDataRow)GetValue(AttachmentListSelectedItemProperty);
						var list = (PpsDataRelatedFilterDesktop)GetValue(AttachmentListProperty);
						list.Remove(itm);
						e.Handled = true;
					},
					(sender, e) => e.CanExecute = true
				)
			);
		}
		
		public static  DependencyProperty AttachmentListProperty =
			DependencyProperty.Register("AttachmentList", 
			typeof(PpsDataRelatedFilterDesktop),
			typeof(PpsAttachmentsControl));
		public static DependencyProperty AttachmentListSelectedItemProperty =
			DependencyProperty.Register("AttachmentListSelectedItem",
			typeof(PpsDataRow),
			typeof(PpsAttachmentsControl));
		public static RoutedUICommand RemoveAttachmentCommand { get; } = new RoutedUICommand("RemoveAttachment", "RemoveAttachment", typeof(PpsAttachmentsControl));

		public IPpsAttachments Attachments { get; } = null; // wird von ItemsSource abgeleitet


	}
}

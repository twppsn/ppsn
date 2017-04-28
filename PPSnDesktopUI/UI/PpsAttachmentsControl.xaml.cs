using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
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
					(sender, e) => e.CanExecute = ((PpsDataRow)GetValue(AttachmentListSelectedItemProperty) != null)
				)
			);
			CommandBindings.Add(
				new CommandBinding(AddFileAttachmentCommand,
					(sender, e) =>
					{
						var ofd = new OpenFileDialog();
						ofd.Multiselect = true;
						ofd.CheckFileExists = true;
						if (ofd.ShowDialog() == true)
						{
							var list = (PpsDataRelatedFilterDesktop)GetValue(AttachmentListProperty);
							var env = PpsEnvironment.GetEnvironment(this);
							foreach (var filename in ofd.FileNames)
							{
								var trans = env.MasterData.CreateTransaction();
								var oinf = new PpsObjectInfo(env, "Attachment");
								oinf.IsRev = false;
								//var obj = env.CreateNewObject(trans, Guid.NewGuid(), "Attachment", "1", false);
								var obj = env.CreateNewObject(trans, oinf);
								((PpsObjectBlobData)((dynamic)obj).Data).ReadFromFile(filename, trans).Wait();
								((PpsObjectBlobData)((dynamic)obj).Data).CommitAsync(trans).GetAwaiter().OnCompleted(() =>
								{
									list.Table.Add(obj);
									trans.Commit();
									trans.Dispose();
								});
							}
						}
						e.Handled = true;
					},
					(sender, e) => e.CanExecute = true
				)
			);
		}

		public static DependencyProperty AttachmentListProperty =
			DependencyProperty.Register("AttachmentList",
			typeof(PpsDataRelatedFilterDesktop),
			typeof(PpsAttachmentsControl));
		public static DependencyProperty AttachmentListSelectedItemProperty =
			DependencyProperty.Register("AttachmentListSelectedItem",
			typeof(PpsDataRow),
			typeof(PpsAttachmentsControl));
		public static RoutedUICommand RemoveAttachmentCommand { get; } = new RoutedUICommand("RemoveAttachment", "RemoveAttachment", typeof(PpsAttachmentsControl));
		public static RoutedUICommand AddFileAttachmentCommand { get; } = new RoutedUICommand("AddFileAttachment", "AddFileAttachment", typeof(PpsAttachmentsControl));
		public static RoutedUICommand AddLinkAttachmentCommand { get; } = new RoutedUICommand("AddLinkAttachment", "AddLinkAttachment", typeof(PpsAttachmentsControl));

		public IPpsAttachments Attachments { get; } = null; // wird von ItemsSource abgeleitet


	}
}

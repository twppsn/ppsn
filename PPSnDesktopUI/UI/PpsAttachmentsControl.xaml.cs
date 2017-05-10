using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using TecWare.PPSn.Data;
using System.IO;

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
		private readonly Lazy<PpsEnvironment> getEnvironment;

		public PpsAttachmentsControl()
		{
			InitializeComponent();

			this.getEnvironment = new Lazy<PpsEnvironment>(() => PpsEnvironment.GetEnvironment(this));

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
					async (sender, e) =>
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
								var trans = env.MasterData.CreateTransaction(PpsMasterDataTransactionLevel.Write);
								var obj = env.CreateNewObject(Environment.ObjectInfos[PpsEnvironment.AttachmentObjectTyp], StuffIO.MimeTypesFromExtension(Path.GetExtension(filename)));

								obj.Tags.UpdateTag(env.UserId, "Filename", PpsObjectTagClass.Text, filename);

								var data = await obj.GetDataAsync<PpsObjectBlobData>();
								await data.ReadFromFileAsync(filename);
								await data.CommitAsync();

								Dispatcher.Invoke(() => list.Table.Add(obj));

								trans.Commit();
								trans.Dispose();
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

		public PpsEnvironment Environment => getEnvironment.Value;
	} // class PpsAttachmentsControl

	public class PpsAttachmentConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			if (!(value is PpsDataRow))
				return String.Empty;
			var obj = ((PpsObject)((dynamic)value).Table.DataSet.Environment.GetObject(((dynamic)value).Id));
			if (obj == null)
				return String.Empty;

			var details = new List<string>();
			details.Add(obj.Nr);
			details.Add(obj.Typ);
			details.Add(obj.Id.ToString());
			foreach (var tag in obj.Tags)
				details.Add($"{tag.Name}:{((string)tag.Value)}");

			return details;
			
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
			=> throw new NotSupportedException();
	} // class PpsAttachmentConverter
}

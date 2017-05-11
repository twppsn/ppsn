using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using Microsoft.Win32;
using Neo.IronLua;
using TecWare.DE.Stuff;
using TecWare.PPSn.Data;
using System.Collections.Specialized;

namespace TecWare.PPSn.UI
{
	public interface IPpsAttachmentItem
	{
		bool Remove();

		string Name { get; }
		string MimeType { get; }

		object Data { get; }
	} // interface IPpsAttachmentItem

	public interface IPpsAttachments : IEnumerable<IPpsAttachmentItem>
	{
		void Append(PpsObject data);
	} // interface IPpsAttachments

	public interface IPpsAttachmentSource
	{
		IPpsAttachments AttachmentsSource { get; }
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
						if (SelectedAttachment != null)
							SelectedAttachment.Remove();
						e.Handled = true;
					},
					(sender, e) => e.CanExecute = SelectedAttachment != null
				)
			);
			CommandBindings.Add(
				new CommandBinding(AddFileAttachmentCommand,
					async (sender, e) =>
					{
						var ofd = new OpenFileDialog();
						ofd.Multiselect = true;
						ofd.CheckFileExists = true;
						if (ofd.ShowDialog() ?? false)
						{
							foreach (var filename in ofd.FileNames)
							{
								using (var trans = Environment.MasterData.CreateTransaction(PpsMasterDataTransactionLevel.Write))
								{
									var obj = Environment.CreateNewObject(Environment.ObjectInfos[PpsEnvironment.AttachmentObjectTyp]);
									obj.Tags.UpdateTag(Environment.UserId, "Filename", PpsObjectTagClass.Text, filename);

									var data = await obj.GetDataAsync<PpsObjectBlobData>();
									await data.ReadFromFileAsync(filename);
									await data.CommitAsync();

									Dispatcher.Invoke(() => AttachmentsSource.Append(obj));

									trans.Commit();
								}
							}
						}
						e.Handled = true;
					},
					(sender, e) => e.CanExecute = true
				)
			);
		} // ctor

		public readonly static DependencyProperty AttachmentsSourceProperty = DependencyProperty.Register(nameof(AttachmentsSource), typeof(IPpsAttachments), typeof(PpsAttachmentsControl));
		private readonly static DependencyPropertyKey SelectedAttachmentPropertyKey = DependencyProperty.RegisterReadOnly(nameof(SelectedAttachment), typeof(IPpsAttachmentItem), typeof(PpsAttachmentsControl), new UIPropertyMetadata(null));
		public readonly static DependencyProperty SelectedAttachmentProperty = SelectedAttachmentPropertyKey.DependencyProperty;

		public IPpsAttachments AttachmentsSource { get => (IPpsAttachments)GetValue(AttachmentsSourceProperty); set => SetValue(AttachmentsSourceProperty, value); }
		public IPpsAttachmentItem SelectedAttachment => (IPpsAttachmentItem)GetValue(SelectedAttachmentProperty);

		public readonly static RoutedUICommand RemoveAttachmentCommand = new RoutedUICommand("RemoveAttachment", "RemoveAttachment", typeof(PpsAttachmentsControl));
		public readonly static RoutedUICommand AddFileAttachmentCommand = new RoutedUICommand("AddFileAttachment", "AddFileAttachment", typeof(PpsAttachmentsControl));
		public readonly static RoutedUICommand AddLinkAttachmentCommand = new RoutedUICommand("AddLinkAttachment", "AddLinkAttachment", typeof(PpsAttachmentsControl));

		public PpsEnvironment Environment => getEnvironment.Value;
	} // class PpsAttachmentsControl

	#region -- class PpsDataTableAttachmentConverter ------------------------------------

	public sealed class PpsDataTableAttachmentConverter : IValueConverter
	{
		#region -- class PpsAttachmentItemImplementation --------------------------------

		private sealed class PpsAttachmentItemImplementation : IPpsAttachmentItem
		{
			private readonly PpsDataRow row;
			private readonly int linkColumnIndex;

			public PpsAttachmentItemImplementation(PpsDataRow row, int linkColumnIndex)
			{
				this.row = row;
				this.linkColumnIndex = linkColumnIndex;
			} // ctor

			private PpsObject GetLinkedObject()
				=> (PpsObject)row[linkColumnIndex];

			public bool Remove() 
				=> row.Remove();

			public string Name => GetLinkedObject()?.GetProperty("Filename", "noname");
			public string MimeType => GetLinkedObject()?.MimeType;

			public object Data => GetLinkedObject();
		} // class PpsAttachmentItemImplementation

		#endregion

		#region -- class PpsAttachmentImplementation ------------------------------------

		private sealed class PpsAttachmentImplementation : IPpsAttachments, INotifyCollectionChanged
		{
			private readonly IPpsDataView view;
			private readonly int linkColumnIndex;

			public PpsAttachmentImplementation(IPpsDataView view, string linkColumnName)
			{
				this.view = view;
				this.linkColumnIndex = view.Table.TableDefinition.FindColumnIndex(linkColumnName ?? throw new ArgumentNullException(nameof(linkColumnName)), true);
			} // ctor

			private PpsUndoManagerBase GetUndoManager(PpsDataSet ds)
				=> (PpsUndoManagerBase)ds.UndoSink;

			public void Append(PpsObject data)
			{
				using (var trans = GetUndoManager(view.Table.DataSet).BeginTransaction("Datei hinzugefügt."))
				{
					var row = view.NewRow(null, null);
					row[linkColumnIndex] = data;
					view.Add(row);

					trans.Commit();
				}
			} // proc Append

			public IEnumerator<IPpsAttachmentItem> GetEnumerator()
				=> view.Select(c => new PpsAttachmentItemImplementation(c, linkColumnIndex)).GetEnumerator();

			IEnumerator IEnumerable.GetEnumerator()
				=> GetEnumerator();

			public event NotifyCollectionChangedEventHandler CollectionChanged
			{
				add { view.CollectionChanged += value; }
				remove { view.CollectionChanged -= value; }
			} // event CollectionChanged

			public IPpsDataView View => view;
		} // class PpsAttachmentImplementation
		
		#endregion

		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			if (value is IPpsDataView v)
				return new PpsAttachmentImplementation(v, LinkColumnName);
			else
				throw new NotSupportedException();
		} // func Convert

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			if (value is PpsAttachmentImplementation v)
				return v.View;
			else
				throw new NotSupportedException();
		} // func ConvertBack

		public string LinkColumnName { get; set; }
	} // class PpsDataTableAttachmentConverter

	#endregion

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

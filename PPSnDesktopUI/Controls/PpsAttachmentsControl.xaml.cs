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
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using Microsoft.Win32;
using TecWare.DE.Stuff;
using TecWare.PPSn.Data;
using System.Collections.Specialized;

namespace TecWare.PPSn.Controls
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
								using (var trans = await Environment.MasterData.CreateTransactionAsync(PpsMasterDataTransactionLevel.Write))
								{
									var obj = await Environment.CreateNewObjectAsync(Environment.ObjectInfos[PpsEnvironment.AttachmentObjectTyp]);
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
		public readonly static DependencyProperty SelectedAttachmentProperty = DependencyProperty.Register(nameof(SelectedAttachment), typeof(IPpsAttachmentItem), typeof(PpsAttachmentsControl));
		public readonly static DependencyProperty AddFileButtonVisibleProperty = DependencyProperty.Register(nameof(AddFileButtonVisible), typeof(bool), typeof(PpsAttachmentsControl), new UIPropertyMetadata(true));
		public readonly static DependencyProperty AddLinkButtonVisibleProperty = DependencyProperty.Register(nameof(AddLinkButtonVisible), typeof(bool), typeof(PpsAttachmentsControl), new UIPropertyMetadata(true));
		public readonly static DependencyProperty RemoveButtonVisibleProperty = DependencyProperty.Register(nameof(RemoveButtonVisible), typeof(bool), typeof(PpsAttachmentsControl), new UIPropertyMetadata(true));
		public readonly static DependencyProperty CameraButtonVisibleProperty = DependencyProperty.Register(nameof(CameraButtonVisible), typeof(bool), typeof(PpsAttachmentsControl), new UIPropertyMetadata(true));
		public readonly static DependencyProperty ScannerButtonVisibleProperty = DependencyProperty.Register(nameof(ScannerButtonVisible), typeof(bool), typeof(PpsAttachmentsControl), new UIPropertyMetadata(true));
		public readonly static DependencyProperty SignatureButtonVisibleProperty = DependencyProperty.Register(nameof(SignatureButtonVisible), typeof(bool), typeof(PpsAttachmentsControl), new UIPropertyMetadata(true));
		public readonly static DependencyProperty ButtonSevenVisibleProperty = DependencyProperty.Register(nameof(ButtonSevenVisible), typeof(bool), typeof(PpsAttachmentsControl), new UIPropertyMetadata(true));
		public readonly static DependencyProperty ButtonSevenCaptionProperty = DependencyProperty.Register(nameof(ButtonSevenCaption), typeof(string), typeof(PpsAttachmentsControl), new UIPropertyMetadata(String.Empty));
		public readonly static DependencyProperty ButtonSevenCommandProperty = DependencyProperty.Register(nameof(ButtonSevenCommand), typeof(TecWare.PPSn.UI.PpsCommand), typeof(PpsAttachmentsControl));

		public IPpsAttachments AttachmentsSource { get => (IPpsAttachments)GetValue(AttachmentsSourceProperty); set => SetValue(AttachmentsSourceProperty, value); }
		public IPpsAttachmentItem SelectedAttachment => (IPpsAttachmentItem)GetValue(SelectedAttachmentProperty);

		/// <summary>sets the visibility of the AddFileButton - default true</summary>
		public bool AddFileButtonVisible => (bool)GetValue(AddFileButtonVisibleProperty);
		/// <summary>sets the visibility of the AddLinkButton - default true</summary>
		public bool AddLinkButtonVisible => (bool)GetValue(AddLinkButtonVisibleProperty);
		/// <summary>sets the visibility of the RemoveButton - default true</summary>
		public bool RemoveButtonVisible => (bool)GetValue(RemoveButtonVisibleProperty);
		/// <summary>sets the visibility of the CameraButton - default true</summary>
		public bool CameraButtonVisible => (bool)GetValue(RemoveButtonVisibleProperty);
		/// <summary>sets the visibility of the ScannerButton - default true</summary>
		public bool ScannerButtonVisible => (bool)GetValue(RemoveButtonVisibleProperty);
		/// <summary>sets the visibility of the SignatureButton - default true</summary>
		public bool SignatureButtonVisible => (bool)GetValue(RemoveButtonVisibleProperty);
		/// <summary>sets the visibility of the SignatureButton - default true</summary>
		public bool ButtonSevenVisible => (bool)GetValue(ButtonSevenVisibleProperty);
		/// <summary>sets the visibility of the SignatureButton - default true</summary>
		public string ButtonSevenCaption => (string)GetValue(ButtonSevenCaptionProperty);
		/// <summary>sets the visibility of the SignatureButton - default true</summary>
		public TecWare.PPSn.UI.PpsCommand ButtonSevenCommand => (TecWare.PPSn.UI.PpsCommand)GetValue(ButtonSevenCommandProperty);

		public readonly static RoutedUICommand RemoveAttachmentCommand = new RoutedUICommand("RemoveAttachment", "RemoveAttachment", typeof(PpsAttachmentsControl));
		public readonly static RoutedUICommand AddFileAttachmentCommand = new RoutedUICommand("AddFileAttachment", "AddFileAttachment", typeof(PpsAttachmentsControl));
		public readonly static RoutedUICommand AddLinkAttachmentCommand = new RoutedUICommand("AddLinkAttachment", "AddLinkAttachment", typeof(PpsAttachmentsControl));

		public PpsEnvironment Environment => getEnvironment.Value;
	} // class PpsAttachmentsControl

	#region -- class PpsDataTableAttachmentConverter ------------------------------------

	public sealed class PpsDataTableAttachmentConverter : IValueConverter
	{
		#region -- class PpsAttachmentItemImplementation --------------------------------

		private sealed class PpsAttachmentItemImplementation : IPpsAttachmentItem, IEquatable<PpsDataRow>
		{
			private readonly PpsDataRow row;
			private readonly int linkColumnIndex;

			public PpsAttachmentItemImplementation(PpsDataRow row, string linkColumnName)
			{
				this.row = row;
				this.linkColumnIndex = row.Table.TableDefinition.FindColumnIndex(linkColumnName ?? throw new ArgumentNullException(nameof(linkColumnName)), true);
			} // ctor

			public PpsAttachmentItemImplementation(PpsDataRow row, int linkColumnIndex)
			{
				this.row = row;
				this.linkColumnIndex = linkColumnIndex;
			} // ctor

			private PpsObject GetLinkedObject()
				=> (PpsObject)row[linkColumnIndex];

			public bool Remove() 
				=> row.Remove();

			public bool Equals(PpsDataRow other)
				=> other == row;

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
			else if (value is PpsDataRow i)
				return new PpsAttachmentItemImplementation(i, LinkColumnName);
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

	public class BoolToVisibilityConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			if ((bool)value)
				return Visibility.Visible;
			else return Visibility.Collapsed;
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			throw new NotSupportedException();
		}
	}
	#endregion
}

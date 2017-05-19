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
	#region -- Interfaces ---------------------------------------------------------------

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

	#endregion

	/// <summary>
	/// Interaction logic for PpsAttachmentsControl.xaml
	/// </summary>
	public partial class PpsAttachmentsControl : UserControl, IPpsAttachmentSource
	{
		private readonly Lazy<PpsEnvironment> getEnvironment;

		public PpsAttachmentsControl()
		{
			InitializeComponent();

			// binding overloads are first checkable after the control is finished
			Loaded += LoadBindings;

			this.getEnvironment = new Lazy<PpsEnvironment>(() => PpsEnvironment.GetEnvironment(this));
		} // ctor

		#region -- LoadBindings ---------------------------------------------------------

		private void LoadBindings(object sender, EventArgs e)
		{
			if (AddFileButtonCommand == null)
				CommandBindings.Add(
				new CommandBinding(AddFileAttachmentCommand,
					async (isender, ie) =>
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
						ie.Handled = true;
					},
					(isender, ie) => ie.CanExecute = true
				)
			);
			else
				CommandBindings.Add(new CommandBinding(AddFileAttachmentCommand, (isender, ie) => AddFileButtonCommand.ExecuteCommand(new UI.PpsCommandContext(Environment, isender, ie))));

			if (AddLinkButtonCommand == null)
				CommandBindings.Add(
					new CommandBinding(AddLinkAttachmentCommand,
						(isender, ie) =>
						{
							throw new NotImplementedException();
						},
						(isender, ie) => ie.CanExecute = true
					)
				);
			else
				CommandBindings.Add(new CommandBinding(AddLinkAttachmentCommand, (isender, ie) => AddLinkButtonCommand.ExecuteCommand(new UI.PpsCommandContext(Environment, isender, ie))));

			if (RemoveButtonCommand == null)
				CommandBindings.Add(
						new CommandBinding(RemoveAttachmentCommand,
							(isender, ie) =>
							{
								if (SelectedAttachment != null)
									SelectedAttachment.Remove();
								ie.Handled = true;
							},
							(isender, ie) => ie.CanExecute = SelectedAttachment != null
						)
					);
			else
				CommandBindings.Add(new CommandBinding(RemoveAttachmentCommand, (isender, ie) => RemoveButtonCommand.ExecuteCommand(new UI.PpsCommandContext(Environment, isender, ie))));

			if (ScannerButtonCommand == null)
				CommandBindings.Add(
					new CommandBinding(ScannerAttachmentCommand,
						(isender, ie) =>
						{
							throw new NotImplementedException();
						},
						(isender, ie) => ie.CanExecute = true
					)
				);
			else
				CommandBindings.Add(new CommandBinding(ScannerAttachmentCommand, (isender, ie) => ScannerButtonCommand.ExecuteCommand(new UI.PpsCommandContext(Environment, isender, ie))));

			if (CameraButtonCommand == null)
				CommandBindings.Add(
					new CommandBinding(CameraAttachmentCommand,
						(isender, ie) =>
						{
							throw new NotImplementedException();
						},
						(isender, ie) => ie.CanExecute = true
					)
				);
			else
				CommandBindings.Add(new CommandBinding(CameraAttachmentCommand, (isender, ie) => CameraButtonCommand.ExecuteCommand(new UI.PpsCommandContext(Environment, isender, ie))));

			if (SignatureButtonCommand == null)
				CommandBindings.Add(
					new CommandBinding(SignatureAttachmentCommand,
						(isender, ie) =>
						{
							throw new NotImplementedException();
						},
						(isender, ie) => ie.CanExecute = true
					)
				);
			else
				CommandBindings.Add(new CommandBinding(SignatureAttachmentCommand, (isender, ie) => SignatureButtonCommand.ExecuteCommand(new UI.PpsCommandContext(Environment, isender, ie))));

			if (SeventhButtonCommand == null)
				; // NOP
			else
				CommandBindings.Add(new CommandBinding(SeventhButtonAttachmentCommand, (isender, ie) => SeventhButtonCommand.ExecuteCommand(new UI.PpsCommandContext(Environment, isender, ie))));
		}

		#endregion

		#region -- Propertys ------------------------------------------------------------

		public readonly static DependencyProperty AttachmentsSourceProperty = DependencyProperty.Register(nameof(AttachmentsSource), typeof(IPpsAttachments), typeof(PpsAttachmentsControl));
		public readonly static DependencyProperty SelectedAttachmentProperty = DependencyProperty.Register(nameof(SelectedAttachment), typeof(IPpsAttachmentItem), typeof(PpsAttachmentsControl));

		public readonly static DependencyProperty AddFileButtonVisibleProperty = DependencyProperty.Register(nameof(AddFileButtonVisible), typeof(bool), typeof(PpsAttachmentsControl), new UIPropertyMetadata(true));
		public readonly static DependencyProperty AddFileButtonCommandProperty = DependencyProperty.Register(nameof(AddFileButtonCommand), typeof(TecWare.PPSn.UI.PpsCommand), typeof(PpsAttachmentsControl));
		public readonly static DependencyProperty AddLinkButtonVisibleProperty = DependencyProperty.Register(nameof(AddLinkButtonVisible), typeof(bool), typeof(PpsAttachmentsControl), new UIPropertyMetadata(true));
		public readonly static DependencyProperty AddLinkButtonCommandProperty = DependencyProperty.Register(nameof(AddLinkButtonCommand), typeof(TecWare.PPSn.UI.PpsCommand), typeof(PpsAttachmentsControl));
		public readonly static DependencyProperty RemoveButtonVisibleProperty = DependencyProperty.Register(nameof(RemoveButtonVisible), typeof(bool), typeof(PpsAttachmentsControl), new UIPropertyMetadata(true));
		public readonly static DependencyProperty RemoveButtonCommandProperty = DependencyProperty.Register(nameof(RemoveButtonCommand), typeof(TecWare.PPSn.UI.PpsCommand), typeof(PpsAttachmentsControl));
		public readonly static DependencyProperty CameraButtonVisibleProperty = DependencyProperty.Register(nameof(CameraButtonVisible), typeof(bool), typeof(PpsAttachmentsControl), new UIPropertyMetadata(true));
		public readonly static DependencyProperty CameraButtonCommandProperty = DependencyProperty.Register(nameof(CameraButtonCommand), typeof(TecWare.PPSn.UI.PpsCommand), typeof(PpsAttachmentsControl));
		public readonly static DependencyProperty ScannerButtonVisibleProperty = DependencyProperty.Register(nameof(ScannerButtonVisible), typeof(bool), typeof(PpsAttachmentsControl), new UIPropertyMetadata(true));
		public readonly static DependencyProperty ScannerButtonCommandProperty = DependencyProperty.Register(nameof(ScannerButtonCommand), typeof(TecWare.PPSn.UI.PpsCommand), typeof(PpsAttachmentsControl));
		public readonly static DependencyProperty SignatureButtonVisibleProperty = DependencyProperty.Register(nameof(SignatureButtonVisible), typeof(bool), typeof(PpsAttachmentsControl), new UIPropertyMetadata(true));
		public readonly static DependencyProperty SignatureButtonCommandProperty = DependencyProperty.Register(nameof(SignatureButtonCommand), typeof(TecWare.PPSn.UI.PpsCommand), typeof(PpsAttachmentsControl));
		public readonly static DependencyProperty SeventhButtonVisibleProperty = DependencyProperty.Register(nameof(SeventhButtonVisible), typeof(bool), typeof(PpsAttachmentsControl), new UIPropertyMetadata(true));
		public readonly static DependencyProperty SeventhButtonCaptionProperty = DependencyProperty.Register(nameof(SeventhButtonCaption), typeof(string), typeof(PpsAttachmentsControl), new UIPropertyMetadata(String.Empty));
		public readonly static DependencyProperty SeventhButtonCommandProperty = DependencyProperty.Register(nameof(SeventhButtonCommand), typeof(TecWare.PPSn.UI.PpsCommand), typeof(PpsAttachmentsControl));

		public IPpsAttachments AttachmentsSource { get => (IPpsAttachments)GetValue(AttachmentsSourceProperty); set => SetValue(AttachmentsSourceProperty, value); }
		public IPpsAttachmentItem SelectedAttachment => (IPpsAttachmentItem)GetValue(SelectedAttachmentProperty);

		/// <summary>sets the visibility of the AddFileButton - default true</summary>
		public bool AddFileButtonVisible => (bool)GetValue(AddFileButtonVisibleProperty);
		/// <summary>overloads the command of the AddFileButton</summary>
		public TecWare.PPSn.UI.PpsCommand AddFileButtonCommand => (TecWare.PPSn.UI.PpsCommand)GetValue(AddFileButtonCommandProperty);

		/// <summary>sets the visibility of the AddLinkButton - default true</summary>
		public bool AddLinkButtonVisible => (bool)GetValue(AddLinkButtonVisibleProperty);
		/// <summary>overloads the command of the AddFileButton</summary>
		public TecWare.PPSn.UI.PpsCommand AddLinkButtonCommand => (TecWare.PPSn.UI.PpsCommand)GetValue(AddLinkButtonCommandProperty);

		/// <summary>sets the visibility of the RemoveButton - default true</summary>
		public bool RemoveButtonVisible => (bool)GetValue(RemoveButtonVisibleProperty);
		/// <summary>overloads the command of the RemoveButton</summary>
		public TecWare.PPSn.UI.PpsCommand RemoveButtonCommand => (TecWare.PPSn.UI.PpsCommand)GetValue(RemoveButtonCommandProperty);

		/// <summary>sets the visibility of the CameraButton - default true</summary>
		public bool CameraButtonVisible => (bool)GetValue(RemoveButtonVisibleProperty);
		/// <summary>overloads the command of the CameraButton</summary>
		public TecWare.PPSn.UI.PpsCommand CameraButtonCommand => (TecWare.PPSn.UI.PpsCommand)GetValue(CameraButtonCommandProperty);

		/// <summary>sets the visibility of the ScannerButton - default true</summary>
		public bool ScannerButtonVisible => (bool)GetValue(RemoveButtonVisibleProperty);
		/// <summary>overloads the command of the ScannerButton</summary>
		public TecWare.PPSn.UI.PpsCommand ScannerButtonCommand => (TecWare.PPSn.UI.PpsCommand)GetValue(ScannerButtonCommandProperty);

		/// <summary>sets the visibility of the SignatureButton - default true</summary>
		public bool SignatureButtonVisible => (bool)GetValue(RemoveButtonVisibleProperty);
		/// <summary>overloads the command of the SignatureButton</summary>
		public TecWare.PPSn.UI.PpsCommand SignatureButtonCommand => (TecWare.PPSn.UI.PpsCommand)GetValue(SignatureButtonCommandProperty);

		/// <summary>sets the visibility of the SeventhButton - default true</summary>
		public bool SeventhButtonVisible => (bool)GetValue(SeventhButtonVisibleProperty);
		/// <summary>sets the caption of the SeventhButton - default true</summary>
		public string SeventhButtonCaption => (string)GetValue(SeventhButtonCaptionProperty);
		/// <summary>sets the command of the SeventhButton - default true</summary>
		public TecWare.PPSn.UI.PpsCommand SeventhButtonCommand => (TecWare.PPSn.UI.PpsCommand)GetValue(SeventhButtonCommandProperty);

		#endregion

		#region -- RoutedUICommands -----------------------------------------------------

		public readonly static RoutedUICommand RemoveAttachmentCommand = new RoutedUICommand("RemoveAttachment", "RemoveAttachment", typeof(PpsAttachmentsControl));
		public readonly static RoutedUICommand AddFileAttachmentCommand = new RoutedUICommand("AddFileAttachment", "AddFileAttachment", typeof(PpsAttachmentsControl));
		public readonly static RoutedUICommand AddLinkAttachmentCommand = new RoutedUICommand("AddLinkAttachment", "AddLinkAttachment", typeof(PpsAttachmentsControl));
		public readonly static RoutedUICommand ScannerAttachmentCommand = new RoutedUICommand("ScannerAttachment", "ScannerAttachment", typeof(PpsAttachmentsControl));
		public readonly static RoutedUICommand CameraAttachmentCommand = new RoutedUICommand("CameraAttachment", "CameraAttachment", typeof(PpsAttachmentsControl));
		public readonly static RoutedUICommand SignatureAttachmentCommand = new RoutedUICommand("SignatureAttachment", "SignatureAttachment", typeof(PpsAttachmentsControl));

		#endregion

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

	#endregion

	#region -- class BoolToVisibilityConverter ------------------------------------

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

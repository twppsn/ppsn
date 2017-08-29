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
using System.ComponentModel;
using TecWare.DE.Networking;

namespace TecWare.PPSn.Controls
{
	#region -- Interfaces ---------------------------------------------------------------

	public interface IPpsAttachmentItem
	{
		bool Remove();
		string Name { get; }
		string MimeType { get; }
		object Data { get; }
		void ShellExecute();
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

			Loaded += LoadBindings;
			this.getEnvironment = new Lazy<PpsEnvironment>(() => PpsEnvironment.GetEnvironment(this));
		} // ctor

		#region -- LoadBindings ---------------------------------------------------------

		private void ExecuteCommand(ICommand cmd, IInputElement target)
		{
			if (cmd is RoutedCommand r)
				r.Execute(null, target);
			else
				cmd.Execute(null);
		} // func ExecuteCommand

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

									AttachmentsSource.Append(obj);

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
				CommandBindings.Add(new CommandBinding(AddFileAttachmentCommand, (isender, ie) => ExecuteCommand(AddFileButtonCommand, this)));

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
				CommandBindings.Add(new CommandBinding(AddLinkAttachmentCommand, (isender, ie) => ExecuteCommand(AddLinkButtonCommand, this)));

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
				CommandBindings.Add(new CommandBinding(RemoveAttachmentCommand, (isender, ie) => ExecuteCommand(RemoveButtonCommand, this)));

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
				CommandBindings.Add(new CommandBinding(ScannerAttachmentCommand, (isender, ie) => ExecuteCommand(ScannerButtonCommand, this)));

			if (CameraButtonCommand == null)
				CommandBindings.Add(
					new CommandBinding(CameraAttachmentCommand,
						(isender, ie) =>
						{
						},
						(isender, ie) => ie.CanExecute = true
					)
				);
			else
				CommandBindings.Add(new CommandBinding(CameraAttachmentCommand, (isender, ie) => ExecuteCommand(CameraButtonCommand, this)));

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
				CommandBindings.Add(new CommandBinding(SignatureAttachmentCommand, (isender, ie) => ExecuteCommand(SignatureButtonCommand, this)));

			if (SeventhButtonCommand == null)
				; // NOP
			else
				CommandBindings.Add(new CommandBinding(SeventhButtonAttachmentCommand, (isender, ie) => ExecuteCommand(SeventhButtonCommand, this)));

			CommandBindings.Add(
					new CommandBinding(RunFileAttachmentCommand,
						(isender, ie) =>
						{
							SelectedAttachment.ShellExecute();
						},
						(isender, ie) => ie.CanExecute = SelectedAttachment?.MimeType != MimeTypes.Text.DataSet
					)
				);
		}

		#endregion

		#region -- Propertys ------------------------------------------------------------

		public readonly static DependencyProperty AttachmentsSourceProperty = DependencyProperty.Register(nameof(AttachmentsSource), typeof(IPpsAttachments), typeof(PpsAttachmentsControl));
		public readonly static DependencyProperty SelectedAttachmentProperty = DependencyProperty.Register(nameof(SelectedAttachment), typeof(IPpsAttachmentItem), typeof(PpsAttachmentsControl));

		public readonly static DependencyProperty AddFileButtonVisibleProperty = DependencyProperty.Register(nameof(AddFileButtonVisible), typeof(bool), typeof(PpsAttachmentsControl), new UIPropertyMetadata(true));
		public readonly static DependencyProperty AddFileButtonCommandProperty = DependencyProperty.Register(nameof(AddFileButtonCommand), typeof(ICommand), typeof(PpsAttachmentsControl));
		public readonly static DependencyProperty AddLinkButtonVisibleProperty = DependencyProperty.Register(nameof(AddLinkButtonVisible), typeof(bool), typeof(PpsAttachmentsControl), new UIPropertyMetadata(false));
		public readonly static DependencyProperty AddLinkButtonCommandProperty = DependencyProperty.Register(nameof(AddLinkButtonCommand), typeof(ICommand), typeof(PpsAttachmentsControl));
		public readonly static DependencyProperty RemoveButtonVisibleProperty = DependencyProperty.Register(nameof(RemoveButtonVisible), typeof(bool), typeof(PpsAttachmentsControl), new UIPropertyMetadata(true));
		public readonly static DependencyProperty RemoveButtonCommandProperty = DependencyProperty.Register(nameof(RemoveButtonCommand), typeof(ICommand), typeof(PpsAttachmentsControl));
		public readonly static DependencyProperty CameraButtonVisibleProperty = DependencyProperty.Register(nameof(CameraButtonVisible), typeof(bool), typeof(PpsAttachmentsControl), new UIPropertyMetadata(true));
		public readonly static DependencyProperty CameraButtonCommandProperty = DependencyProperty.Register(nameof(CameraButtonCommand), typeof(ICommand), typeof(PpsAttachmentsControl));
		public readonly static DependencyProperty ScannerButtonVisibleProperty = DependencyProperty.Register(nameof(ScannerButtonVisible), typeof(bool), typeof(PpsAttachmentsControl), new UIPropertyMetadata(false));
		public readonly static DependencyProperty ScannerButtonCommandProperty = DependencyProperty.Register(nameof(ScannerButtonCommand), typeof(ICommand), typeof(PpsAttachmentsControl));
		public readonly static DependencyProperty SignatureButtonVisibleProperty = DependencyProperty.Register(nameof(SignatureButtonVisible), typeof(bool), typeof(PpsAttachmentsControl), new UIPropertyMetadata(false));
		public readonly static DependencyProperty SignatureButtonCommandProperty = DependencyProperty.Register(nameof(SignatureButtonCommand), typeof(ICommand), typeof(PpsAttachmentsControl));
		private readonly static DependencyProperty SeventhButtonVisibleProperty = DependencyProperty.Register(nameof(SeventhButtonVisible), typeof(bool), typeof(PpsAttachmentsControl), new UIPropertyMetadata(false));
		private readonly static DependencyProperty SeventhButtonImagePathProperty = DependencyProperty.Register(nameof(SeventhButtonImagePath), typeof(object), typeof(PpsAttachmentsControl));
		private readonly static DependencyProperty SeventhButtonCommandProperty = DependencyProperty.Register(nameof(SeventhButtonCommand), typeof(ICommand), typeof(PpsAttachmentsControl));

		public IPpsAttachments AttachmentsSource { get => (IPpsAttachments)GetValue(AttachmentsSourceProperty); set => SetValue(AttachmentsSourceProperty, value); }
		public IPpsAttachmentItem SelectedAttachment => (IPpsAttachmentItem)GetValue(SelectedAttachmentProperty);

		/// <summary>sets the visibility of the AddFileButton - default true</summary>
		public bool AddFileButtonVisible => (bool)GetValue(AddFileButtonVisibleProperty);
		/// <summary>overloads the command of the AddFileButton</summary>
		public ICommand AddFileButtonCommand => (ICommand)GetValue(AddFileButtonCommandProperty);

		/// <summary>sets the visibility of the AddLinkButton - default true</summary>
		public bool AddLinkButtonVisible => (bool)GetValue(AddLinkButtonVisibleProperty);
		/// <summary>overloads the command of the AddFileButton</summary>
		public ICommand AddLinkButtonCommand => (ICommand)GetValue(AddLinkButtonCommandProperty);

		/// <summary>sets the visibility of the RemoveButton - default true</summary>
		public bool RemoveButtonVisible => (bool)GetValue(RemoveButtonVisibleProperty);
		/// <summary>overloads the command of the RemoveButton</summary>
		public ICommand RemoveButtonCommand => (ICommand)GetValue(RemoveButtonCommandProperty);

		/// <summary>sets the visibility of the CameraButton - default true</summary>
		public bool CameraButtonVisible => (bool)GetValue(RemoveButtonVisibleProperty);
		/// <summary>overloads the command of the CameraButton</summary>
		public ICommand CameraButtonCommand => (ICommand)GetValue(CameraButtonCommandProperty);

		/// <summary>sets the visibility of the ScannerButton - default true</summary>
		public bool ScannerButtonVisible => (bool)GetValue(RemoveButtonVisibleProperty);
		/// <summary>overloads the command of the ScannerButton</summary>
		public ICommand ScannerButtonCommand => (ICommand)GetValue(ScannerButtonCommandProperty);

		/// <summary>sets the visibility of the SignatureButton - default true</summary>
		public bool SignatureButtonVisible => (bool)GetValue(RemoveButtonVisibleProperty);
		/// <summary>overloads the command of the SignatureButton</summary>
		public ICommand SignatureButtonCommand => (ICommand)GetValue(SignatureButtonCommandProperty);

		/// <summary>sets the visibility of the SeventhButton - default true</summary>
		public bool SeventhButtonVisible { get => (bool)GetValue(SeventhButtonVisibleProperty); set => SetValue(SeventhButtonVisibleProperty, value); }
		/// <summary>sets the path for the image of the SeventhButton</summary>
		public object SeventhButtonImagePath { get => GetValue(SeventhButtonImagePathProperty); set => SetValue(SeventhButtonImagePathProperty, value); }
		/// <summary>sets the command of the SeventhButton - default true</summary>
		public ICommand SeventhButtonCommand { get => (ICommand)GetValue(SeventhButtonCommandProperty); set => SetValue(SeventhButtonCommandProperty, value); }

		#endregion

		#region -- RoutedUICommands -----------------------------------------------------

		public readonly static RoutedUICommand RemoveAttachmentCommand = new RoutedUICommand("RemoveAttachment", "RemoveAttachment", typeof(PpsAttachmentsControl));
		public readonly static RoutedUICommand AddFileAttachmentCommand = new RoutedUICommand("AddFileAttachment", "AddFileAttachment", typeof(PpsAttachmentsControl));
		public readonly static RoutedUICommand RunFileAttachmentCommand = new RoutedUICommand("RunFileAttachment", "RunFileAttachment", typeof(PpsAttachmentsControl));
		public readonly static RoutedUICommand AddLinkAttachmentCommand = new RoutedUICommand("AddLinkAttachment", "AddLinkAttachment", typeof(PpsAttachmentsControl));
		public readonly static RoutedUICommand ScannerAttachmentCommand = new RoutedUICommand("ScannerAttachment", "ScannerAttachment", typeof(PpsAttachmentsControl));
		public readonly static RoutedUICommand CameraAttachmentCommand = new RoutedUICommand("CameraAttachment", "CameraAttachment", typeof(PpsAttachmentsControl));
		public readonly static RoutedUICommand SignatureAttachmentCommand = new RoutedUICommand("SignatureAttachment", "SignatureAttachment", typeof(PpsAttachmentsControl));
		public readonly static RoutedUICommand SeventhButtonAttachmentCommand = new RoutedUICommand("SeventhButtonAttachment", "SeventhButtonAttachment", typeof(PpsAttachmentsControl));

		#endregion

		public PpsEnvironment Environment => getEnvironment.Value;
	} // class PpsAttachmentsControl

	#region -- class PpsDataTableAttachmentConverter ------------------------------------

	public sealed class PpsDataTableAttachmentConverter : IValueConverter
	{
		#region -- class PpsAttachmentItemImplementation --------------------------------

		private sealed class PpsAttachmentItemImplementation : IPpsAttachmentItem, IEquatable<PpsDataRow>, INotifyPropertyChanged
		{
			public event PropertyChangedEventHandler PropertyChanged;

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

			public void ShellExecute()
			{
				GetLinkedObject().ShellExecute();
			}


			private PpsObject GetLinkedObject()
				=> (PpsObject)row[linkColumnIndex];

			private PpsObject GetParentObject()
				=> ((IPpsObjectBasedDataSet)(row.Table.DataSet)).Object;

			public bool Remove()
			{
				if (row.Remove())
				{
					GetParentObject().Links.RemoveLink(GetLinkedObject().Id);
					return true;
				}

				return false;
			}

			public bool Equals(PpsDataRow other)
				=> other == row;

			public string Name => GetLinkedObject()?.GetProperty("Filename", "noname");
			public string MimeType => GetLinkedObject()?.MimeType;

			public object Data => GetLinkedObject();

			private void NotifyPropertyChanged(string propertyName = "")
				=> PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

			public object Picture
			{
				get
				{
					var obj = GetLinkedObject()?.GetDataAsync<PpsObjectImageData>().AwaitTask();
					if (obj == null)
						return null;
					obj.PropertyChanged += (sender, ob) => { if (ob.PropertyName == "Preview") NotifyPropertyChanged("Picture"); };
					return obj.Preview;
				}
			}

			public string Type => MimeType?.StartsWith("image") == true ? "picture" : "binary";
		} // class PpsAttachmentItemImplementation

		#endregion

		#region -- class PpsAttachmentImplementation ------------------------------------

		private sealed class PpsAttachmentImplementation : IPpsAttachments, INotifyCollectionChanged
		{
			public event NotifyCollectionChangedEventHandler CollectionChanged;

			private readonly IPpsDataView view;
			private readonly int linkColumnIndex;

			public PpsAttachmentImplementation(IPpsDataView view, string linkColumnName)
			{
				this.view = view;
				this.linkColumnIndex = view.Table.TableDefinition.FindColumnIndex(linkColumnName ?? throw new ArgumentNullException(nameof(linkColumnName)), true);

				WeakEventManager<INotifyCollectionChanged, NotifyCollectionChangedEventArgs>.AddHandler(view, nameof(INotifyCollectionChanged.CollectionChanged),
					OnCollectionChanged
				);
			} // ctor

			private PpsUndoManagerBase GetUndoManager(PpsDataSet ds)
				=> (PpsUndoManagerBase)ds.UndoSink;

			public void Append(PpsObject data)
			{
				using (var trans = GetUndoManager(view.Table.DataSet).BeginTransaction("Datei hinzugefügt."))
				{
					var r = view.NewRow(null, null);
					r[linkColumnIndex] = data;
					view.Add(r);

					OnCollectionChanged(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, new PpsDataRow[] { r }));

					trans.Commit();
				}
			} // proc Append

			public IEnumerator<IPpsAttachmentItem> GetEnumerator()
				=> view.Select(c => new PpsAttachmentItemImplementation(c, linkColumnIndex)).GetEnumerator();

			IEnumerator IEnumerable.GetEnumerator()
				=> GetEnumerator();

			private void OnCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
			{
				switch (e.Action)
				{
					case NotifyCollectionChangedAction.Add:
						if (e.NewItems.Count > 1)
							throw new NotSupportedException();

						CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(
							NotifyCollectionChangedAction.Add,
							new PpsAttachmentItemImplementation((PpsDataRow)e.NewItems[0], linkColumnIndex),
							e.NewStartingIndex)
						);
						break;
					case NotifyCollectionChangedAction.Remove:
						if (e.OldItems.Count > 1)
							throw new NotSupportedException();

						/*CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(
							NotifyCollectionChangedAction.Remove,
							new PpsAttachmentItemImplementation((PpsDataRow)e.OldItems[0], linkColumnIndex),
							e.OldStartingIndex)
						);*/
						CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));

						break;
					case NotifyCollectionChangedAction.Reset:
						CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
						break;
					case NotifyCollectionChangedAction.Replace:
					case NotifyCollectionChangedAction.Move:
					default:
						throw new NotSupportedException();
				}
			} // proc OnCollectionChanged

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
}
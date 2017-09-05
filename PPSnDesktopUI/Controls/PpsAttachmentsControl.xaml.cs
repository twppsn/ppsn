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
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using Microsoft.Win32;
using TecWare.DE.Networking;
using TecWare.DE.Stuff;
using TecWare.PPSn.Data;

namespace TecWare.PPSn.Controls
{
	#region -- interface IPpsAttachmentItem -------------------------------------------

	/// <summary>Representation for the attachments</summary>
	public interface IPpsAttachmentItem
	{
		/// <summary>Remove the current attachment</summary>
		/// <returns></returns>
		bool Remove();

		/// <summary>Displayname</summary>
		string Name { get; }
		
		/// <summary>Access the data column.</summary>
		PpsObject LinkedObject { get; }
	} // interface IPpsAttachmentItem

	#endregion

	#region -- interface IPpsAttachments ----------------------------------------------

	/// <summary>Access to the attachments of an object, or table.</summary>
	public interface IPpsAttachments : IEnumerable<IPpsAttachmentItem>
	{
		/// <summary>Add's a new attachment to the list.</summary>
		/// <param name="data">Object to connect.</param>
		void Append(PpsObject data);
	} // interface IPpsAttachments

	#endregion

	#region -- interface IPpsAttachmentSource -----------------------------------------

	/// <summary>Attachment provider</summary>
	public interface IPpsAttachmentSource
	{
		/// <summary>Access to the attachments</summary>
		IPpsAttachments AttachmentsSource { get; }
	} // interface IPpsAttachmentSource

	#endregion

	#region -- class PpsAttachmentsControl --------------------------------------------

	/// <summary>Wpf-Control to view attachments</summary>
	public partial class PpsAttachmentsControl : UserControl, IPpsAttachmentSource
	{
		private readonly Lazy<PpsEnvironment> getEnvironment;

		#region -- Ctor/Dtor ----------------------------------------------------------

		public PpsAttachmentsControl()
		{
			InitializeComponent();

			Loaded += LoadBindings;
			this.getEnvironment = new Lazy<PpsEnvironment>(() => PpsEnvironment.GetEnvironment(this));
		} // ctor

		protected override void OnInitialized(EventArgs e)
		{
			base.OnInitialized(e);
		} // proc OnInitialized

		#endregion

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
						var ofd = new OpenFileDialog
						{
							Multiselect = true,
							CheckFileExists = true
						};
						if (ofd.ShowDialog() ?? false)
						{
							// todo: get parent block provider?
							// UI should be show something

							foreach (var fileName in ofd.FileNames)
							{
								try
								{
									await AttachmentsSource.AppendAsync(Environment, fileName);
								}
								catch (Exception ex)
								{
									await Environment.ShowExceptionAsync(ex, String.Format("Datei konnte nicht importiert werden.\n" + fileName));
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
							SelectedAttachment.LinkedObject.ShellExecute();
						},
						(isender, ie) => ie.CanExecute = SelectedAttachment?.LinkedObject?.MimeType != MimeTypes.Text.DataSet
					)
				);
		}

		#endregion

		#region -- Propertys ------------------------------------------------------------

		public readonly static DependencyProperty AttachmentsSourceProperty = DependencyProperty.Register(nameof(AttachmentsSource), typeof(IPpsAttachments), typeof(PpsAttachmentsControl));
		public readonly static DependencyProperty SelectedAttachmentProperty = DependencyProperty.Register(nameof(SelectedAttachment), typeof(IPpsAttachmentItem), typeof(PpsAttachmentsControl));

		public readonly static DependencyProperty AddFileButtonVisibleProperty = DependencyProperty.Register(nameof(AddFileButtonVisible), typeof(bool), typeof(PpsAttachmentsControl), new UIPropertyMetadata(true));
		public readonly static DependencyProperty AddFileButtonCommandProperty = DependencyProperty.Register(nameof(AddFileButtonCommand), typeof(ICommand), typeof(PpsAttachmentsControl));
		public readonly static DependencyProperty AddLinkButtonVisibleProperty = DependencyProperty.Register(nameof(AddLinkButtonVisible), typeof(bool), typeof(PpsAttachmentsControl), new UIPropertyMetadata(true));
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

	#endregion

	#region -- class PpsDataTableAttachmentConverter ----------------------------------

	/// <summary>Converts a IPpsDataView, PpsDataRow (e.g. column) to the attachment interfaces.</summary>
	public sealed class PpsDataTableAttachmentConverter : IValueConverter
	{
		#region -- class PpsAttachmentItemImplementation --------------------------------
		
		private sealed class PpsAttachmentItemImplementation : IPpsAttachmentItem, IEquatable<PpsDataRow>, INotifyPropertyChanged
		{
			public event PropertyChangedEventHandler PropertyChanged;

			private readonly PpsDataRow row;        // row
			private readonly bool rowOwner;			// is the object owner of the row
			private readonly int linkColumnIndex;   // index of the object column

			private PpsObject currentLinkedObject = null;

			#region -- Ctor/Dtor --------------------------------------------------------

			public PpsAttachmentItemImplementation(PpsDataRow row, string linkColumnName, bool rowOwner)
			{
				this.row = row;
				this.rowOwner = rowOwner;
				this.linkColumnIndex = row.Table.TableDefinition.FindColumnIndex(linkColumnName ?? throw new ArgumentNullException(nameof(linkColumnName)), true);

				AttachRowPropertyChanged();
			} // ctor

			public PpsAttachmentItemImplementation(PpsDataRow row, int linkColumnIndex, bool rowOwner)
			{
				this.row = row;
				this.rowOwner = rowOwner;
				this.linkColumnIndex = linkColumnIndex;

				AttachRowPropertyChanged();
			} // ctor

			public override int GetHashCode() 
				=> row.GetHashCode();

			public override bool Equals(object obj)
				=> obj is PpsAttachmentItemImplementation a ? a.row == row : false;

			public bool Equals(PpsDataRow other)
				=> other == row;

			private void AttachRowPropertyChanged()
			{
				AttachObjectPropertyChanged();

				WeakEventManager<INotifyPropertyChanged, PropertyChangedEventArgs>.AddHandler(
					row, nameof(PropertyChanged), ForwardRowPropertyChanged
				);
			} // proc AttachRowPropertyChanged

			private void AttachObjectPropertyChanged()
			{
				// disconnect old
				if (currentLinkedObject != null)
					currentLinkedObject.PropertyChanged -= ForwarObjectPropertyChanged;

				// connect to new
				currentLinkedObject = row[linkColumnIndex] as PpsObject;
				if (currentLinkedObject != null)
					currentLinkedObject.PropertyChanged += ForwarObjectPropertyChanged;

				// raise events
				PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Name)));
				PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(LinkedObject)));
			} // proc AttachObjectPropertyChanged

			private void ForwardRowPropertyChanged(object sender, PropertyChangedEventArgs args)
			{
				if(args.PropertyName == row.Table.Columns[linkColumnIndex].Name)
				{
					if(currentLinkedObject != row[linkColumnIndex])
					{
						AttachObjectPropertyChanged();
					}
				}
			} // proc ForwardRowPropertyChanged

			private void ForwarObjectPropertyChanged(object sender, PropertyChangedEventArgs args)
			{
				switch(args.PropertyName)
				{
					case nameof(PpsObject.Nr):
					case "Name":
						PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Name)));
						break;
				}
			} // proc ForwarObjectPropertyChanged

			#endregion

			public bool Remove()
			{
				if (rowOwner)
					return row.Remove();
				else
				{
					row[linkColumnIndex] = null;
					return true;
				}
			} // proc Remove
			
			/// <summary>Access to the object.</summary>
			public PpsObject LinkedObject => (PpsObject)row[linkColumnIndex];

			/// <summary>Displayname for the object</summary>
			public string Name
				=> LinkedObject == null
					? null
					: (LinkedObject.GetProperty<string>("Name", null) ?? LinkedObject.Nr);
		} // class PpsAttachmentItemImplementation

		#endregion

		#region -- class PpsAttachmentsImplementation -----------------------------------

		private sealed class PpsAttachmentsImplementation : IPpsAttachments, INotifyCollectionChanged
		{
			public event NotifyCollectionChangedEventHandler CollectionChanged;

			private readonly IPpsDataView view;
			private readonly int linkColumnIndex;

			public PpsAttachmentsImplementation(IPpsDataView view, string linkColumnName)
			{
				this.view = view;
				this.linkColumnIndex = view.Table.TableDefinition.FindColumnIndex(linkColumnName ?? throw new ArgumentNullException(nameof(linkColumnName)), true);

				WeakEventManager<INotifyCollectionChanged, NotifyCollectionChangedEventArgs>.AddHandler(view, nameof(INotifyCollectionChanged.CollectionChanged),
					OnCollectionChanged
				);
			} // ctor

			private IPpsUndoSink GetUndoManager()
				=> view.Table.DataSet.UndoSink;

			public void Append(PpsObject data)
			{
				using (var trans = GetUndoManager()?.BeginTransaction("Datei hinzugefügt."))
				{
					var r = view.NewRow(null, null);
					r[linkColumnIndex] = data;
					view.Add(r);
					trans.Commit();
				}
			} // proc Append

			public IEnumerator<IPpsAttachmentItem> GetEnumerator()
				=> view.Select(c => new PpsAttachmentItemImplementation(c, linkColumnIndex, true)).GetEnumerator();

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
							new PpsAttachmentItemImplementation((PpsDataRow)e.NewItems[0], linkColumnIndex, true),
							e.NewStartingIndex)
						);
						break;
					case NotifyCollectionChangedAction.Remove:
						if (e.OldItems.Count > 1)
							throw new NotSupportedException();

						CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(
							NotifyCollectionChangedAction.Remove,
							new PpsAttachmentItemImplementation((PpsDataRow)e.OldItems[0], linkColumnIndex, true),
							e.OldStartingIndex)
						);
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
			switch (value)
			{
				case IPpsDataView v:
					return new PpsAttachmentsImplementation(v, LinkColumnName);
				case PpsDataRow i:
					return new PpsAttachmentItemImplementation(i, LinkColumnName, false);
				case null:
					throw new ArgumentNullException(nameof(value));
				default:
					throw new NotSupportedException($"Convert '{value.GetType().Name}' to a attachment interface.");
			}
		} // func Convert

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
			=> value is PpsAttachmentsImplementation v
				? v.View
				: throw new NotSupportedException();

		/// <summary>Column that contains the PpsObject's</summary>
		public string LinkColumnName { get; set; }
	} // class PpsDataTableAttachmentConverter

	#endregion

	#region -- class PpsAttachmentsHelper ---------------------------------------------

	public static class PpsAttachmentsHelper
	{
		public static async Task<PpsObject> AppendAsync(this IPpsAttachments attachments, PpsEnvironment environment, string fileName)
		{
			// every file one transaction, and exception handling
			using (var trans = await environment.MasterData.CreateTransactionAsync(PpsMasterDataTransactionLevel.Write))
			{
				var obj = await environment.CreateNewObjectFromFileAsync(fileName);
				attachments.Append(obj);
				trans.Commit();
				return obj;
			}
		} // proc AppendAsync
	} // class PpsAttachmentsHelper

	#endregion
}
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
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using Microsoft.Win32;
using Neo.IronLua;
using TecWare.DE.Networking;
using TecWare.DE.Stuff;
using TecWare.PPSn.Data;
using TecWare.PPSn.UI;

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
		/// <summary>Has the attachment a linked object.</summary>
		bool IsNull { get; }
		
		/// <summary>Access the data column.</summary>
		PpsObject LinkedObject { get; set; }
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
		private static readonly DependencyPropertyKey commandsPropertyKey = DependencyProperty.RegisterReadOnly(nameof(Commands), typeof(PpsUICommandCollection), typeof(PpsAttachmentsControl), new FrameworkPropertyMetadata(null));

		/// <summary>Dependencyproperty for Commands</summary>
		public static readonly DependencyProperty CommandsProperty = commandsPropertyKey.DependencyProperty;
		/// <summary>Dependencyproperty for the AttachmentsSource</summary>
		public static readonly DependencyProperty AttachmentsSourceProperty = DependencyProperty.Register(nameof(AttachmentsSource), typeof(IPpsAttachments), typeof(PpsAttachmentsControl));
		/// <summary>Dependencyproperty for the selected Attachment</summary>
		public static readonly DependencyProperty SelectedAttachmentProperty = DependencyProperty.Register(nameof(SelectedAttachment), typeof(IPpsAttachmentItem), typeof(PpsAttachmentsControl));

		/// <summary>Routed command for adding a File</summary>
		public static readonly RoutedCommand AddAttachmentAddFileCommand = new PpsAsyncCommand("Attachments.AddFile", typeof(PpsAttachmentsControl), ctx => AppendAttachmentFromFileDialogAsync(ctx), ctx => IsAttachmentControlEnabled(ctx));
		/// <summary>Routed command for adding a Link</summary>
		public static readonly RoutedCommand AddAttachmentAddLinkCommand = new PpsAsyncCommand("Attachments.AddLink", typeof(PpsAttachmentsControl), ctx => AppendAttachmentFromObjectAsync(ctx), ctx => IsAttachmentControlEnabled(ctx));
		/// <summary>Routed command for removing an attachment</summary>
		public static readonly RoutedCommand RemoveAttachmentCommand = new PpsCommand("Attachments.Remove", typeof(IPpsAttachmentItem), ctx => RemoveAttachment(ctx), ctx => IsAttachmentRemovable(ctx));
		/// <summary>Routed command for executing/showing of an attachment</summary>
		public static readonly RoutedCommand RunAttachmentCommand = new PpsAsyncCommand("Attachments.Run", typeof(IPpsAttachmentItem), ctx => RunAttachmentAsync(ctx), ctx => IsAttachmentControlEnabled(ctx));
		/// <summary>Routed command for opening the attachment in the internal editor</summary>
		public static readonly RoutedCommand RunPictureEditorCommand = new PpsAsyncCommand("Attachments.Edit", typeof(PpsAttachmentsControl), ctx => RunEditorAsync(ctx), ctx => IsAttachmentControlEnabled(ctx));


		private readonly Lazy<PpsEnvironment> getEnvironment;
		private readonly Lazy<IPpsWindowPane> getCurrentPane;

		#region -- Ctor/Dtor ----------------------------------------------------------

		public PpsAttachmentsControl()
		{
			InitializeComponent();

			this.getEnvironment = new Lazy<PpsEnvironment>(() => PpsEnvironment.GetEnvironment(this));
			this.getCurrentPane = new Lazy<IPpsWindowPane>(() => PpsEnvironment.GetCurrentPane(this));

			// initialize toolbar
			var commands = new PpsUICommandCollection
			{
				AddLogicalChildHandler = AddLogicalChild,
				RemoveLogicalChildHandler = RemoveLogicalChild
			};
			SetValue(commandsPropertyKey, commands);

			// Add default commands
			Commands.AddButton("100,110", "filePlus", AddAttachmentAddFileCommand, String.Empty, "Fügt einen Anhang hinzu");
			Commands.AddButton("100,120", "link", AddAttachmentAddLinkCommand, String.Empty, "Fügt eine Verknüpfung hinzu");
			Commands.AddButton("100,130", "camera", RunPictureEditorCommand, String.Empty, "Startet die Bearbeitung der Bildanhänge");

			Loaded += PpsAttachmentsControl_Loaded;
		} // ctor

		private void PpsAttachmentsControl_Loaded(object sender, RoutedEventArgs e)
		{
			// detach load event
			Loaded -= PpsAttachmentsControl_Loaded;

			// add command bindings
			Environment.CallMemberDirect("InitalizeAttachmentControl", new object[] { Commands, Tag, this }, ignoreNilFunction: true);
		} // proc PpsAttachmentsControl_Loaded

		#endregion

		#region -- Command implementations --------------------------------------------

		private static Task RunEditorAsync(PpsCommandContext context)
		{
			var pane = context.GetService<IPpsWindowPane>(true);
			var control = context.GetService<PpsAttachmentsControl>(true);

			return pane.PaneManager.OpenPaneAsync(
				typeof(PpsPicturePane),
				PpsOpenPaneMode.NewPane,
				new LuaTable()
				{
					[PpsPicturePane.AttachmentsSourceArgument] = control.AttachmentsSource,
					[PpsPicturePane.ParentPaneArgument] = control.CurrentPane
				}
			);
		} // func RunEditorAsync

		private static Task AppendAttachmentFromFileDialogAsync(PpsCommandContext context)
			=> context.GetService<PpsAttachmentsControl>(true).AppendAttachmentFromFileDialogAsync();

		private async Task AppendAttachmentFromFileDialogAsync()
		{
			var ofd = new OpenFileDialog
			{
				Multiselect = true,
				CheckFileExists = true
			};

			if (ofd.ShowDialog() ?? false)
			{
				using (var bar = CurrentPane?.DisableUI()) // disable pane ui
				{
					foreach (var fileName in ofd.FileNames)
					{
						try
						{
							if (bar != null)
								bar.Text = String.Format("Füge hinzu {0}...", Path.GetFileName(fileName));
							await AttachmentsSource.AppendAsync(Environment, fileName);
						}
						catch (Exception ex)
						{
							await Environment.ShowExceptionAsync(ex, String.Format("Datei konnte nicht importiert werden.\n" + fileName));
						}
					}
				}
			}
		} // proc AppendAttachmentFromFileDialogAsync

		private static Task AppendAttachmentFromObjectAsync(PpsCommandContext context)
			=> context.GetService<PpsAttachmentsControl>(true).AppendAttachmentFromObjectAsync();
		
		private Task AppendAttachmentFromObjectAsync()
		{
			MessageBox.Show("Todo");
			return Task.CompletedTask;
		} // proc AppendAttachmentFromObjectAsync

		private static bool IsAttachmentControlEnabled(PpsCommandContext context)
			=> true;

		private static IPpsAttachmentItem GetCurrentAttachmentItemFromContext(PpsCommandContext ctx)
			=> ctx.DataContext is IPpsAttachmentItem item ? item : ctx.GetService<PpsAttachmentsControl>()?.SelectedAttachment;

		private static void RemoveAttachment(PpsCommandContext ctx)
			=> GetCurrentAttachmentItemFromContext(ctx)?.Remove();

		private static bool IsAttachmentRemovable(PpsCommandContext ctx)
			=> true; // todo: (isender, ie) => ie.CanExecute = SelectedAttachment != null
		
		private static Task RunAttachmentAsync(PpsCommandContext ctx)
			=> GetCurrentAttachmentItemFromContext(ctx)?.LinkedObject.ViewAsync(ctx.Target) ?? Task.CompletedTask;

		#endregion

		#region -- Logical Children ---------------------------------------------------

		protected override IEnumerator LogicalChildren
			=> Commands == null ? base.LogicalChildren : Procs.CombineEnumerator(base.LogicalChildren, Commands.GetEnumerator());

		#endregion

		/// <summary>List of commands for the toolbar.</summary>
		public PpsUICommandCollection Commands => (PpsUICommandCollection)GetValue(CommandsProperty);

		/// <summary>Attachment source.</summary>
		public IPpsAttachments AttachmentsSource { get => (IPpsAttachments)GetValue(AttachmentsSourceProperty); set => SetValue(AttachmentsSourceProperty, value); }
		/// <summary>Current active attachment.</summary>
		public IPpsAttachmentItem SelectedAttachment => (IPpsAttachmentItem)GetValue(SelectedAttachmentProperty);

		/// <summary>Access to the environment.</summary>
		public PpsEnvironment Environment => getEnvironment.Value;
		/// <summary>Return the current pane.</summary>
		public IPpsWindowPane CurrentPane => getCurrentPane.Value;
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

			public bool IsNull => row[linkColumnIndex] == null;

			/// <summary>Access to the object.</summary>
			public PpsObject LinkedObject { get => (PpsObject)row[linkColumnIndex]; set => row[linkColumnIndex] = value; }

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

		public static IPpsAttachmentSource GetAttachmentSource(this DependencyObject dc)
			=> StuffUI.GetControlService<IPpsAttachmentSource>(dc, true);
	} // class PpsAttachmentsHelper

	#endregion
}
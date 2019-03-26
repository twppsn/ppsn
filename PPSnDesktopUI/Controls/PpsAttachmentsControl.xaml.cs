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
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using Neo.IronLua;
using TecWare.DE.Networking;
using TecWare.DE.Stuff;
using TecWare.PPSn.Data;
using TecWare.PPSn.UI;

namespace TecWare.PPSn.Controls
{
	//#region -- class PpsAttachmentsControl --------------------------------------------

	///// <summary>Wpf-Control to view attachments</summary>
	public partial class PpsAttachmentsControla : UserControl //, IPpsAttachmentSource
	{
		//	//	private static readonly DependencyPropertyKey commandsPropertyKey = DependencyProperty.RegisterReadOnly(nameof(Commands), typeof(PpsUICommandCollection), typeof(PpsAttachmentsControl), new FrameworkPropertyMetadata(null));

		//	//	/// <summary>Dependencyproperty for Commands</summary>
		//	//	public static readonly DependencyProperty CommandsProperty = commandsPropertyKey.DependencyProperty;
		//	/// <summary>Dependencyproperty for the AttachmentsSource</summary>
		//	public static readonly DependencyProperty AttachmentsSourceProperty = DependencyProperty.Register(nameof(AttachmentsSource), typeof(IPpsAttachments), typeof(PpsAttachmentsControla));
		//	///// <summary>Dependencyproperty for the selected Attachment</summary>
		//	//	public static readonly DependencyProperty SelectedAttachmentProperty = DependencyProperty.Register(nameof(SelectedAttachment), typeof(IPpsAttachmentItem), typeof(PpsAttachmentsControl));

		//	//	/// <summary>Routed command for adding a File</summary>
		//	//	public static readonly RoutedCommand AddAttachmentAddFileCommand = new PpsAsyncCommand("Attachments.AddFile", typeof(PpsAttachmentsControl), ctx => AppendAttachmentFromFileDialogAsync(ctx), ctx => IsAttachmentControlEnabled(ctx));
		//	/// <summary>Routed command for adding a Link</summary>
		//	public static readonly RoutedCommand AddAttachmentAddLinkCommand = new PpsAsyncCommand("Attachments.AddLink", typeof(PpsAttachmentsControl), ctx => AppendAttachmentFromObjectAsync(ctx), ctx => IsAttachmentControlEnabled(ctx));
		//	/// <summary></summary>
		//	public static readonly RoutedCommand AddAttachmentFromClipboardCommand = new PpsAsyncCommand("Attachments.AddClipboard", typeof(PpsAttachmentsControl), ctx => AppendAttachmentFromClipboardAsync(ctx), ctx => IsAddFromClipboardEnabled(ctx));
		//	/// <summary>Routed command for removing an attachment</summary>
		//	public static readonly RoutedCommand RemoveAttachmentCommand = new PpsCommand("Attachments.Remove", typeof(IPpsAttachmentItem), ctx => RemoveAttachment(ctx), ctx => IsAttachmentRemovable(ctx));
		//	/// <summary>Routed command for executing/showing of an attachment</summary>
		//	public static readonly RoutedCommand RunAttachmentCommand = new PpsAsyncCommand("Attachments.Run", typeof(IPpsAttachmentItem), ctx => RunAttachmentAsync(ctx), ctx => IsAttachmentControlEnabled(ctx));
		//	/// <summary>Routed command for opening the attachment in the internal editor</summary>
		//	public static readonly RoutedCommand RunPictureEditorCommand = new PpsAsyncCommand("Attachments.Edit", typeof(PpsAttachmentsControl), ctx => RunEditorAsync(ctx), ctx => IsAttachmentControlEnabled(ctx));


		//	private readonly Lazy<PpsEnvironment> getEnvironment;
		//	private readonly Lazy<IPpsWindowPane> getCurrentPane;

		//	#region -- Ctor/Dtor ----------------------------------------------------------

		//	public PpsAttachmentsControl()
		//	{
		//		InitializeComponent();

		//		this.getEnvironment = new Lazy<PpsEnvironment>(() => PpsEnvironment.GetEnvironment(this));
		//		this.getCurrentPane = new Lazy<IPpsWindowPane>(() => PpsEnvironment.GetCurrentPane(this));

		//		// initialize toolbar
		//		var commands = new PpsUICommandCollection
		//		{
		//			AddLogicalChildHandler = AddLogicalChild,
		//			RemoveLogicalChildHandler = RemoveLogicalChild
		//		};
		//		SetValue(commandsPropertyKey, commands);

		//		// Add default commands
		//		Commands.AddButton("100,110", "filePlus", AddAttachmentAddFileCommand, String.Empty, "Fügt einen Anhang hinzu");
		//		Commands.AddButton("100,120", "clipboardPaste", AddAttachmentFromClipboardCommand, String.Empty, "Füge ein Bild aus der Zwischenablage ein.");
		//		//Commands.AddButton("100,120", "link", AddAttachmentAddLinkCommand, String.Empty, "Fügt eine Verknüpfung hinzu");
		//		Commands.AddButton("100,130", "camera", RunPictureEditorCommand, String.Empty, "Startet die Bearbeitung der Bildanhänge");

		//		Loaded += PpsAttachmentsControl_Loaded;
		//	} // ctor

		//	private void PpsAttachmentsControl_Loaded(object sender, RoutedEventArgs e)
		//	{
		//		// detach load event
		//		Loaded -= PpsAttachmentsControl_Loaded;

		//		// add command bindings
		//		Environment.CallMemberDirect("InitalizeAttachmentControl", new object[] { Commands, Tag, this }, ignoreNilFunction: true);
		//	} // proc PpsAttachmentsControl_Loaded

		//	#endregion

		//	#region -- Command implementations --------------------------------------------

		//	private static Task RunEditorAsync(PpsCommandContext context)
		//	{
		//		var pane = context.GetService<IPpsWindowPane>(true);
		//		var control = context.GetService<PpsAttachmentsControl>(true);

		//		return pane.PaneManager.OpenPaneAsync(
		//			typeof(PpsPicturePane),
		//			PpsOpenPaneMode.NewPane,
		//			new LuaTable()
		//			{
		//				[PpsPicturePane.AttachmentsSourceArgument] = control.AttachmentsSource,
		//				[PpsPicturePane.ParentPaneArgument] = control.CurrentPane
		//			}
		//		);
		//	} // func RunEditorAsync

		//	private static Task AppendAttachmentFromFileDialogAsync(PpsCommandContext context)
		//		=> context.GetService<PpsAttachmentsControl>(true).AppendAttachmentFromFileDialogAsync();

		//	private async Task AppendAttachmentFromFileDialogAsync()
		//	{
		//		var ofd = new OpenFileDialog
		//		{
		//			Multiselect = true,
		//			CheckFileExists = true
		//		};

		//		if (ofd.ShowDialog() ?? false)
		//		{
		//			using (var bar = CurrentPane?.DisableUI()) // disable pane ui
		//			{
		//				foreach (var fileName in ofd.FileNames)
		//				{
		//					try
		//					{
		//						if (bar != null)
		//							bar.Text = String.Format("Füge hinzu {0}...", Path.GetFileName(fileName));
		//						await AttachmentsSource.AppendAsync(Environment, fileName);
		//					}
		//					catch (Exception ex)
		//					{
		//						await Environment.ShowExceptionAsync(ex, String.Format("Datei konnte nicht importiert werden.\n" + fileName));
		//					}
		//				}
		//			}
		//		}
		//	} // proc AppendAttachmentFromFileDialogAsync

		//	private static Task AppendAttachmentFromObjectAsync(PpsCommandContext context)
		//		=> context.GetService<PpsAttachmentsControl>(true).AppendAttachmentFromObjectAsync();

		//	private static Task AppendAttachmentFromClipboardAsync(PpsCommandContext context)
		//		=> context.GetService<PpsAttachmentsControl>(true).AppendAttachmentFromClipboardAsync();

		//	private async Task AppendAttachmentFromClipboardAsync()
		//	{
		//		if ((await Environment.MsgBoxAsync("Bild aus Zwischenanlage anfügen?", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.Yes)) != MessageBoxResult.Yes)
		//			return;

		//		// convert bitmap to png
		//		var bmp = Clipboard.GetImage();
		//		using (var bar = CurrentPane?.DisableUI("Füge Bild aus Zwischenablage ein..."))
		//		using (var dst = new MemoryStream())
		//		{
		//			var encoder = new PngBitmapEncoder();
		//			encoder.Frames.Add(BitmapFrame.Create(bmp));
		//			encoder.Save(dst);
		//			dst.Flush();

		//			dst.Position = 0;
		//			await AttachmentsSource.AppendAsync(Environment, dst, DateTime.Now.ToString("yyyy-MM-dd_HH:mm") + ".png", MimeTypes.Image.Png);
		//		}
		//	} // AppendAttachmentFromClipboardAsync

		//	private Task AppendAttachmentFromObjectAsync()
		//	{
		//		MessageBox.Show("Todo");
		//		return Task.CompletedTask;
		//	} // proc AppendAttachmentFromObjectAsync

		//	private static bool IsAttachmentControlEnabled(PpsCommandContext context)
		//		=> true;

		//	private static bool IsAddFromClipboardEnabled(PpsCommandContext context)
		//		=> IsAttachmentControlEnabled(context) && Clipboard.ContainsImage();

		//	private static IPpsAttachmentItem GetCurrentAttachmentItemFromContext(PpsCommandContext ctx)
		//		=> ctx.DataContext is IPpsAttachmentItem item ? item : ctx.GetService<PpsAttachmentsControl>()?.SelectedAttachment;

		//	private static void RemoveAttachment(PpsCommandContext ctx)
		//		=> GetCurrentAttachmentItemFromContext(ctx)?.Remove();

		//	private static bool IsAttachmentRemovable(PpsCommandContext ctx)
		//		=> true; // todo: (isender, ie) => ie.CanExecute = SelectedAttachment != null

		//	private static Task RunAttachmentAsync(PpsCommandContext ctx)
		//		=> GetCurrentAttachmentItemFromContext(ctx)?.LinkedObject.ViewAsync(ctx.Target) ?? Task.CompletedTask;

		//	#endregion

		//	#region -- Logical Children ---------------------------------------------------

		//	protected override IEnumerator LogicalChildren
		//		=> Commands == null ? base.LogicalChildren : Procs.CombineEnumerator(base.LogicalChildren, Commands.GetEnumerator());

		//	#endregion

		//	/// <summary>List of commands for the toolbar.</summary>
		//	public PpsUICommandCollection Commands => (PpsUICommandCollection)GetValue(CommandsProperty);

		/// <summary>Attachment source.</summary>
		//public IPpsAttachments AttachmentsSource { get => (IPpsAttachments)GetValue(AttachmentsSourceProperty); set => SetValue(AttachmentsSourceProperty, value); }
		//	/// <summary>Current active attachment.</summary>
		//	public IPpsAttachmentItem SelectedAttachment => (IPpsAttachmentItem)GetValue(SelectedAttachmentProperty);

		//	/// <summary>Access to the environment.</summary>
		//	public PpsEnvironment Environment => getEnvironment.Value;
		//	/// <summary>Return the current pane.</summary>
		//	public IPpsWindowPane CurrentPane => getCurrentPane.Value;
	} // class PpsAttachmentsControl

	//#endregion

	//#region -- class PpsDataTableAttachmentConverter ----------------------------------

	///// <summary>Converts a IPpsDataView, PpsDataRow (e.g. column) to the attachment interfaces.</summary>
	//public sealed class PpsDataTableAttachmentConverter : IValueConverter
	//{
	//	public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
	//	{
	//		switch (value)
	//		{
	//			case IPpsDataView v:
	//				return new PpsAttachmentsImplementation(v, LinkColumnName);
	//			case PpsDataRow i:
	//				return new PpsAttachmentItemImplementation(i, LinkColumnName, false);
	//			case null:
	//				throw new ArgumentNullException(nameof(value));
	//			default:
	//				throw new NotSupportedException($"Convert '{value.GetType().Name}' to a attachment interface.");
	//		}
	//	} // func Convert

	//	public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
	//		=> value is PpsAttachmentsImplementation v
	//			? v.View
	//			: throw new NotSupportedException();

	//	/// <summary>Column that contains the PpsObject's</summary>
	//	public string LinkColumnName { get; set; }
	//} // class PpsDataTableAttachmentConverter

	//#endregion

	//#region -- class PpsAttachmentsHelper ---------------------------------------------

	///// <summary>Helper for the attachments interface</summary>
	//public static class PpsAttachmentsHelper
	//{
	//	//private static async Task<PpsObject> AppendAsync(IPpsAttachments attachments, PpsEnvironment environment, Func<Task<PpsObject>> createObject)
	//	//{
	//	//	// every file one transaction, and exception handling
	//	//	using (var trans = await environment.MasterData.CreateTransactionAsync(PpsMasterDataTransactionLevel.Write))
	//	//	{
	//	//		var obj = await createObject();
	//	//		attachments.Append(obj);
	//	//		trans.Commit();
	//	//		return obj;
	//	//	}
	//	//} // func AppendAsync

	//	///// <summary>Append a file to the attachments list.</summary>
	//	///// <param name="attachments"></param>
	//	///// <param name="environment"></param>
	//	///// <param name="fileName"></param>
	//	///// <returns></returns>
	//	//public static Task<PpsObject> AppendAsync(this IPpsAttachments attachments, PpsEnvironment environment, string fileName)
	//	//	=> AppendAsync(attachments, environment, () => environment.CreateNewObjectFromFileAsync(fileName));

	//	///// <summary>Append a stream to the attachemnts list</summary>
	//	///// <param name="attachments"></param>
	//	///// <param name="environment"></param>
	//	///// <param name="source"></param>
	//	///// <param name="name"></param>
	//	///// <param name="mimeType"></param>
	//	///// <returns></returns>
	//	//public static Task<PpsObject> AppendAsync(this IPpsAttachments attachments, PpsEnvironment environment, Stream source, string name, string mimeType)
	//	//	=> AppendAsync(attachments, environment, () => environment.CreateNewObjectFromStreamAsync(source, name, mimeType));

	//	/// <summary>Get the attachments source from an wpf-object.</summary>
	//	/// <param name="dc"></param>
	//	/// <returns></returns>
	//	public static IPpsAttachmentSource GetAttachmentSource(this DependencyObject dc)
	//		=> StuffUI.GetControlService<IPpsAttachmentSource>(dc, true);
	//} // class PpsAttachmentsHelper

	//#endregion
}
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
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using TecWare.DE.Networking;

namespace TecWare.PPSn.UI
{
	#region -- class PpsAttachmentsControl --------------------------------------------

	/// <summary>Control to view and edit attachments.</summary>
	public partial class PpsAttachmentsControl : UserControl
	{
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
		public static RoutedCommand AddFromFileCommand = new RoutedCommand("Add", typeof(PpsAttachmentsControl));
		public static RoutedCommand AddFromClipboardCommand = new RoutedCommand("Add", typeof(PpsAttachmentsControl));
		public static RoutedCommand AddFromCameraCommand = new RoutedCommand("Add", typeof(PpsAttachmentsControl));

		public static RoutedCommand OpenCommand = new RoutedCommand("Open", typeof(PpsAttachmentsControl));
		public static RoutedCommand RemoveCommand = new RoutedCommand("Remove", typeof(PpsAttachmentsControl));
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

		#region -- AttachmentsSource - property ---------------------------------------

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
		public static readonly DependencyProperty AttachmentsSourceProperty = DependencyProperty.Register(nameof(AttachmentsSource), typeof(IPpsAttachments), typeof(PpsAttachmentsControl), new FrameworkPropertyMetadata(null, new PropertyChangedCallback(OnAttachmentsSourceChanged)));
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

		private static void OnAttachmentsSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
			=> ((PpsAttachmentsControl)d).OnAttachmentsSourceChanged((IPpsAttachments)e.NewValue, (IPpsAttachments)e.OldValue);

		private void OnAttachmentsSourceChanged(IPpsAttachments newValue, IPpsAttachments oldValue)
		{
			zusaList.ItemsSource = newValue;
		} // proc OnAttachmentsSourceChanged

		/// <summary>Attachment source.</summary>
		public IPpsAttachments AttachmentsSource { get => (IPpsAttachments)GetValue(AttachmentsSourceProperty); set => SetValue(AttachmentsSourceProperty, value); }

		#endregion

		#region -- SelectedAttachment - property --------------------------------------

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
		public static readonly DependencyProperty SelectedAttachmentProperty = DependencyProperty.Register(nameof(SelectedAttachment), typeof(IPpsAttachmentItem), typeof(PpsAttachmentsControl), new FrameworkPropertyMetadata(null, new PropertyChangedCallback(OnSelectedAttachmentChanged)));
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

		private static void OnSelectedAttachmentChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
			=> ((PpsAttachmentsControl)d).UpdateZusaSelection((IPpsAttachmentItem)e.NewValue);

		private static void OnSelectionChangedEvent(object sender, SelectionChangedEventArgs e)
			=> ((PpsAttachmentsControl)sender).UpdateSelectedAttachment();

		private void UpdateSelectedAttachment()
		{
			SelectedAttachment = zusaList.SelectedValue as IPpsAttachmentItem;
		} // proc UpdateSelectedAttachment

		private void UpdateZusaSelection(IPpsAttachmentItem value)
		{
			if (zusaList.SelectedValue == value)
				return;

			zusaList.SelectedValue = value;
		} // proc UpdateZusaSelection

		/// <summary>Set or get the current selected attachment</summary>
		public IPpsAttachmentItem SelectedAttachment { get => (IPpsAttachmentItem)GetValue(SelectedAttachmentProperty); set => SetValue(SelectedAttachmentProperty, value); }

		#endregion

		private Lazy<IPpsWindowPane> getCurrentPane;

		#region -- Ctor/Dtor ----------------------------------------------------------

		/// <summary></summary>
		public PpsAttachmentsControl()
		{
			InitializeComponent();

			getCurrentPane = new Lazy<IPpsWindowPane>(() => PpsShellWpf.GetCurrentPane(this));

			CommandBindings.Add(
				new CommandBinding(AddFromFileCommand,
					async (sender, e) =>
					{
						await AddFromFileAsync();
						e.Handled = true;
					},
					(sender, e) =>
					{
						e.CanExecute = GetEditAttachments();
						e.Handled = true;
					}
				)
			);

			CommandBindings.Add(
				new CommandBinding(AddFromClipboardCommand,
					async (sender, e) =>
					{
						await AddFromClipboardAsync();
						e.Handled = true;
					},
					(sender, e) =>
					{
						e.CanExecute = GetEditAttachments() && Clipboard.ContainsImage();
						e.Handled = true;
					}
				)
			);

			CommandBindings.Add(
				new CommandBinding(AddFromCameraCommand,
					async (sender, e) =>
					{
						await AddFromCameraAsync();
						e.Handled = true;
					},
					(sender, e) =>
					{
						e.CanExecute = GetEditAttachments();
						e.Handled = true;
					}
				)
			);

			CommandBindings.Add(
				new CommandBinding(OpenCommand,
					async (sender, e) =>
					{
						if (e.Parameter is IPpsAttachmentItem att)
							await OpenAttachmentAsync(att);
						e.Handled = true;
					},
					(sender, e) =>
					{
						e.CanExecute = GetEditAttachments() && e.Parameter is IPpsAttachmentItem;
						e.Handled = true;
					}
				)
			);

			CommandBindings.Add(
				new CommandBinding(RemoveCommand,
					(sender, e) =>
					{
						if (e.Parameter is IPpsAttachmentItem att)
							RemoveAttachment(att);
						e.Handled = true;
					},
					(sender, e) =>
					{
						e.CanExecute = GetEditAttachments() && e.Parameter is IPpsAttachmentItem;
						e.Handled = true;
					}
				)
			);
		} // ctor

		private bool GetEditAttachments() 
			=> AttachmentsSource != null && AttachmentsSource.CanAdd;

		#endregion

		#region -- Attachment operations ----------------------------------------------

		private async Task AddFromFileAsync()
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
							Environment.ShowException(ex, String.Format("Datei konnte nicht importiert werden.\n" + fileName));
						}
					}
				}
			}
		} // proc AddFromFile

		private async Task AddFromClipboardAsync()
		{
			if (Environment.MsgBox("Bild aus Zwischenanlage anfügen?", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.Yes) != MessageBoxResult.Yes)
				return;

			// convert bitmap to png
			var bmp = Clipboard.GetImage();
			using (var bar = CurrentPane?.DisableUI("Füge Bild aus Zwischenablage ein..."))
				await AddFromBitmapSourceAsync(bmp);
		} // proc AddFromClipboardAsync

		private async Task AddFromCameraAsync()
		{
			var bitmap = StuffUI.TakePicture(this);
			if (bitmap != null)
			{
				using (var bar = CurrentPane?.DisableUI("Füge Bild von Kamera..."))
					await AddFromBitmapSourceAsync(bitmap);
			}
		} // proc AddFromCameraAsync

		private async Task AddFromBitmapSourceAsync(BitmapSource bmp)
		{
			using (var dst = new MemoryStream())
			{
				await Task.Run(() =>
				{
					var encoder = new JpegBitmapEncoder();
					encoder.Frames.Add(BitmapFrame.Create(bmp));
					encoder.Save(dst);
					dst.Flush();
				});

				dst.Position = 0;
				await AttachmentsSource.AppendAsync(Environment, dst, DateTime.Now.ToString("yyyy-MM-dd_HH:mm") + ".jpg", MimeTypes.Image.Jpeg);
			}
		} // proc AddFromBitmapSourceAsync

		private async Task OpenAttachmentAsync(IPpsAttachmentItem item)
		{
			if (item.LinkedObject is PpsObject obj)
				await obj.OpenPaneAsync(CurrentPane.PaneHost.PaneManager);
		} // func OpenAttachmentAsync

		private void RemoveAttachment(IPpsAttachmentItem item) 
			=> item.Remove();

		private IPpsWindowPane CurrentPane => getCurrentPane.Value;
		private PpsEnvironment Environment => (PpsEnvironment)CurrentPane.PaneHost.PaneManager.Shell;

		#endregion

		static PpsAttachmentsControl()
		{
			EventManager.RegisterClassHandler(typeof(PpsAttachmentsControl), Selector.SelectionChangedEvent, new SelectionChangedEventHandler(OnSelectionChangedEvent), true);
		} // sctor
	} // class PpsAttachmentsControl

	#endregion

	#region -- class PpsAttachmentsHelper ---------------------------------------------

	/// <summary></summary>
	public static class PpsAttachmentsHelper
	{
		private static async Task<PpsObject> AppendAsync(IPpsAttachments attachments, PpsEnvironment environment, Func<Task<PpsObject>> createObject)
		{
			// every file one transaction, and exception handling
			using (var trans = await environment.MasterData.CreateTransactionAsync(PpsMasterDataTransactionLevel.Write))
			{
				var obj = await createObject();
				attachments.Append(obj);
				trans.Commit();
				return obj;
			}
		} // func AppendAsync

		/// <summary>Append a file to the attachments list.</summary>
		/// <param name="attachments"></param>
		/// <param name="environment"></param>
		/// <param name="fileName"></param>
		/// <returns></returns>
		public static Task<PpsObject> AppendAsync(this IPpsAttachments attachments, PpsEnvironment environment, string fileName)
			=> AppendAsync(attachments, environment, () => environment.CreateNewObjectFromFileAsync(fileName));

		/// <summary>Append a stream to the attachemnts list</summary>
		/// <param name="attachments"></param>
		/// <param name="environment"></param>
		/// <param name="source"></param>
		/// <param name="name"></param>
		/// <param name="mimeType"></param>
		/// <returns></returns>
		public static Task<PpsObject> AppendAsync(this IPpsAttachments attachments, PpsEnvironment environment, Stream source, string name, string mimeType)
			=> AppendAsync(attachments, environment, () => environment.CreateNewObjectFromStreamAsync(source, name, mimeType));
	} // class PpsAttachmentsHelper

	#endregion
}

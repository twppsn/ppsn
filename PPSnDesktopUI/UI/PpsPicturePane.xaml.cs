using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DirectShowLib;
using Neo.IronLua;
using TecWare.PPSn.Controls;
using TecWare.PPSn.Data;
using WPFMediaKit.DirectShow.Controls;

namespace TecWare.PPSn.UI
{
	/// <summary>
	/// Interaction logic for PpsPicturePane.xaml
	/// </summary>
	public partial class PpsPicturePane : UserControl, IPpsWindowPane
	{
		#region -- Helper Classes -------------------------------------------------------

		#region Data Representation

		public class PpsPecCamera
		{
			private string name;
			private string friendlyName;
			private object image;

			public PpsPecCamera(string Name, string FriendlyName, object Image = null)
			{
				this.name = Name;
				this.friendlyName = FriendlyName;
				this.image = Image;
			}

			public string Name => name;
			public string FriendlyName => friendlyName;
			public object Image { get { return image; } set { this.image = value; } }
		}

		public class PpsPecCommand
		{

			private string commandText;
			private int commandSize;
			private SolidColorBrush commandColor;
			private RoutedUICommand command;
			private UIElement commandIcon;

			public PpsPecCommand(string Text, int Size, SolidColorBrush Color, RoutedUICommand Command, UIElement Icon)
			{
				this.commandText = Text;
				this.commandSize = Size;
				this.commandColor = Color ?? new SolidColorBrush(System.Windows.Media.Color.FromRgb(100, 100, 255));
				this.command = Command;
				this.commandIcon = Icon;
			}

			public UIElement CommandIcon => commandIcon;

			public int CommandSize => commandSize;
			public string CommandText => commandText;
			public RoutedUICommand Command => command;
			public SolidColorBrush CommandColor => commandColor;
		}

		public class PpsPecStrokeThickness
		{
			private string name;
			private int thickness;

			public PpsPecStrokeThickness(string Name, int Thickness)
			{
				this.name = Name;
				this.thickness = Thickness;
			}

			public string Name => name;
			public int Size => thickness;
		}

		public class PpsPecStrokeColor
		{
			private string name;
			private Color color;

			public PpsPecStrokeColor(string Name, Color Color)
			{
				this.name = Name;
				this.color = Color;
			}

			public string Name => name;
			public Color Color => color;
			public Brush Brush => new SolidColorBrush(color);
		}

		public class PpsPecStrokeSettings
		{
			private IEnumerable<PpsPecStrokeColor> colors;
			private IEnumerable<PpsPecStrokeThickness> thicknesses;

			public PpsPecStrokeSettings(IEnumerable<PpsPecStrokeColor> Colors, IEnumerable<PpsPecStrokeThickness> Thicknesses)
			{
				this.colors = Colors;
				this.thicknesses = Thicknesses;
			}

			public IEnumerable<PpsPecStrokeColor> Colors => colors;
			public IEnumerable<PpsPecStrokeThickness> Thicknesses => thicknesses;
		}

		#endregion

		#region -- UnDo/ReDo ------------------------------------------------------------

		private class PpsAddStrokeUndoItem : IPpsUndoItem
		{
			private StrokeCollection collection;
			private Stroke stroke;

			public PpsAddStrokeUndoItem(StrokeCollection collection, Stroke strokeAdded)
			{
				this.collection = collection;
				this.stroke = strokeAdded;
			}

			//<summary>Unused</summary>
			public void Freeze()
			{
				//throw new NotImplementedException();
			}

			public void Redo()
			{
				collection.Add(stroke);
			}

			public void Undo()
			{
				collection.Remove(stroke);
			}
		}
		private class PpsRemoveStrokeUndoItem : IPpsUndoItem
		{
			private StrokeCollection collection;
			private Stroke stroke;

			public PpsRemoveStrokeUndoItem(StrokeCollection collection, Stroke strokeAdded)
			{
				this.collection = collection;
				this.stroke = strokeAdded;
			}

			//<summary>Unused</summary>
			public void Freeze()
			{
				//throw new NotImplementedException();
			}

			public void Redo()
			{
				collection.Remove(stroke);
			}

			public void Undo()
			{
				collection.Add(stroke);
			}
		}

		#endregion

		#endregion

		#region Fields

		private PpsUndoManager strokeUndoManager;
		private readonly PpsEnvironment environment;
		private List<string> captureSourceNames = new List<string>();

		#endregion

		#region ctor

		public PpsPicturePane()
		{
			InitializeComponent();

			Resources[PpsEnvironment.WindowPaneService] = this;

			environment = PpsEnvironment.GetEnvironment(this) ?? throw new ArgumentNullException("environment");

			CachedCameras = new ObservableCollection<PpsPecCamera>();

			DevelopmentSetConstants();

			AddCommandBindings();

			strokeUndoManager = new PpsUndoManager();

			strokeUndoManager.CollectionChanged += (sender, e) => { PropertyChanged?.Invoke(null, new PropertyChangedEventArgs("RedoM")); PropertyChanged?.Invoke(null, new PropertyChangedEventArgs("UndoM")); };

			SetValue(commandsPropertyKey, new PpsUICommandCollection());

			AddToolbarCommands();
		}

		#endregion

		#region Commands

		#region ---- CommandBindings ----------------------------------------------------------

		private void AddCommandBindings()
		{
			CommandBindings.Add(
				new CommandBinding(
					EditOverlayCommand,
					async (sender, e) =>
					{
						if (e.Parameter is IPpsAttachmentItem i)
						{
							if (SelectedAttachment != null && strokeUndoManager.CanUndo)
								switch (MessageBox.Show("Sie haben ungespeicherte Änderungen!\nMöchten Sie diese noch speichern?", "Warnung", MessageBoxButton.YesNoCancel))
								{
									case MessageBoxResult.Yes:
										ApplicationCommands.Save.Execute(null, null);
										break;
									case MessageBoxResult.Cancel:
										return;
								}
							SelectedAttachment = i;
							SelectedCamera = null;
							var imgData = await i.LinkedObject.GetDataAsync<PpsObjectBlobData>();

							var data = await SelectedAttachment.LinkedObject.GetDataAsync<PpsObjectBlobData>();
							InkStrokes = await data.GetOverlayAsync() ?? new StrokeCollection();

							InkStrokes.StrokesChanged += (chgsender, chge) =>
							{
								using (var trans = strokeUndoManager.BeginTransaction("Linie hinzugefügt"))
								{
									foreach (var stroke in chge.Added)
										strokeUndoManager.Append(new PpsAddStrokeUndoItem((StrokeCollection)GetValue(InkStrokesProperty), stroke));
									trans.Commit();
								}
								using (var trans = strokeUndoManager.BeginTransaction("Linie entfernt"))
								{
									foreach (var stroke in chge.Removed)
										strokeUndoManager.Append(new PpsRemoveStrokeUndoItem((StrokeCollection)GetValue(InkStrokesProperty), stroke));
									trans.Commit();
								}
							};
							SetCharmObject(i.LinkedObject);
						}

						strokeUndoManager.Clear();
					}));

			CommandBindings.Add(new CommandBinding(
				ApplicationCommands.Save,
				async (sender, e) =>
				{
					if (SelectedAttachment != null)
					{
						var data = await SelectedAttachment.LinkedObject.GetDataAsync<PpsObjectBlobData>();

						await data.SetOverlayAsync(InkStrokes);
						strokeUndoManager.Clear();
					}
					else if (SelectedCamera != null)
					{
						// Aufnehmen
						var path = System.IO.Path.GetTempPath() + DateTime.Now.ToUniversalTime().ToString("yyyy-MM-dd_HHmmss") + ".jpg";
						// ToDo, RK: clean me
						RenderTargetBitmap bmp = new RenderTargetBitmap(
							(int)videoElement.ActualWidth, (int)videoElement.ActualHeight, 96, 96,
							PixelFormats.Default
						);
						bmp.Render(videoElement);
						BitmapEncoder encoder = new JpegBitmapEncoder();
						encoder.Frames.Add(BitmapFrame.Create(bmp));
						using (var fs = new FileStream(path, FileMode.CreateNew))
							encoder.Save(fs);
						var obj = await IncludePictureAsync(path);

						Attachments.Append(obj);

						File.Delete(path);
					}
				},
				(sender, e) => e.CanExecute = !String.IsNullOrEmpty(SelectedCamera) || strokeUndoManager.CanUndo));


			CommandBindings.Add(new CommandBinding(
				ApplicationCommands.Delete,
				(sender, e) =>
				{
					if (SelectedAttachment is IPpsAttachmentItem item)
					{
						item.Remove();
					}
				},
				(sender, e) => e.CanExecute = SelectedAttachment != null));

			AddCameraCommandBindings();

			AddStrokeCommandBindings();
		}

		private void AddCameraCommandBindings()
		{
			CommandBindings.Add(new CommandBinding(
				ChangeCameraCommand,
				(sender, e) =>
				{
					var preview = (VideoCaptureElement)FindChildElement(typeof(VideoCaptureElement), (Button)e.OriginalSource);

					if (preview == null)
					{
						SelectedAttachment = null;
						SelectedCamera = (string)e.Parameter;
						return;
					}

					RenderTargetBitmap bmp = new RenderTargetBitmap(
							(int)preview.ActualWidth, (int)preview.ActualHeight, 96, 96,
							PixelFormats.Default
						);
					bmp.Render(preview);

					bmp.Freeze();

					var tmp = new List<PpsPecCamera>() { new PpsPecCamera(preview.VideoCaptureSource, preview.VideoCaptureSource, bmp) };

					CachedCameras.Insert(0, new PpsPecCamera(preview.VideoCaptureSource, preview.VideoCaptureSource, bmp));
					CameraEnum.RemoveAt(0);

					ShowCamera(preview, (string)e.Parameter);

					SetCharmObject(null);
				}));
		}

		private void AddStrokeCommandBindings()
		{
			CommandBindings.Add(new CommandBinding(
				OverlayEditFreehandCommand,
				(sender, e) =>
				{
					InkEditMode = InkCanvasEditingMode.Ink;
				}));

			CommandBindings.Add(new CommandBinding(
				OverlayRemoveStrokeCommand,
				(sender, e) =>
				{
					InkEditMode = InkCanvasEditingMode.EraseByStroke;
				}));

			CommandBindings.Add(new CommandBinding(
				ApplicationCommands.Undo,
				(sender, e) =>
				{
					strokeUndoManager.Undo();
				},
				(sender, e) => e.CanExecute = strokeUndoManager.CanUndo));

			CommandBindings.Add(new CommandBinding(
				ApplicationCommands.Redo,
				(sender, e) =>
				{
					strokeUndoManager.Redo();
				},
				(sender, e) => e.CanExecute = strokeUndoManager.CanRedo));

			CommandBindings.Add(new CommandBinding(
				OverlaySetThicknessCommand,
				(sender, e) =>
				{
					var thickness = (PpsPecStrokeThickness)e.Parameter;

					InkDrawingAttributes.Width = InkDrawingAttributes.Height = (double)thickness.Size;
				}));

			CommandBindings.Add(new CommandBinding(
				OverlaySetColorCommand,
				(sender, e) =>
				{
					var color = (PpsPecStrokeColor)e.Parameter;

					InkDrawingAttributes.Color = color.Color;
				},
				(sender, e) => e.CanExecute = true));
		}

		#region Helper Functions

		private async void ShowCamera(VideoCaptureElement preview, string CameraName)
		{
			await Task.Run(() =>
			{
				var mustwait = true;
				Dispatcher.Invoke(() => mustwait = preview.IsPlaying);
				Dispatcher.Invoke(() => preview.Stop());
				if (mustwait)
					Thread.Sleep(1000);
				Dispatcher.Invoke(() => SelectedCamera = CameraName);
				Dispatcher.Invoke(() => SelectedAttachment = null);
			});
		}

		private DependencyObject FindChildElement(Type t, DependencyObject parent)
		{
			if (parent.GetType() == t)
				return parent;

			DependencyObject ret = null;
			var i = 0;

			while (ret == null && i < VisualTreeHelper.GetChildrenCount(parent))
			{
				ret = FindChildElement(t, VisualTreeHelper.GetChild(parent, i));
				i++;
			}

			return ret;
		}

		#endregion

		#region UICommands

		private static readonly DependencyPropertyKey commandsPropertyKey = DependencyProperty.RegisterReadOnly(nameof(Commands), typeof(PpsUICommandCollection), typeof(PpsPicturePane), new FrameworkPropertyMetadata(null));
		public static readonly DependencyProperty CommandsProperty = commandsPropertyKey.DependencyProperty;

		public static readonly RoutedUICommand EditOverlayCommand = new RoutedUICommand("EditOverlay", "EditOverlay", typeof(PpsPicturePane));
		public static readonly RoutedUICommand OverlayEditFreehandCommand = new RoutedUICommand("EditFreeForm", "EditFreeForm", typeof(PpsPicturePane));
		public static readonly RoutedUICommand OverlayRemoveStrokeCommand = new RoutedUICommand("EditRubber", "EditRubber", typeof(PpsPicturePane));
		public static readonly RoutedUICommand OverlaySetThicknessCommand = new RoutedUICommand("SetThickness", "Set Thickness", typeof(PpsPicturePane));
		public static readonly RoutedUICommand OverlaySetColorCommand = new RoutedUICommand("SetColor", "Set Color", typeof(PpsPicturePane));
		public readonly static RoutedUICommand ChangeCameraCommand = new RoutedUICommand("ChangeCamera", "ChangeCamera", typeof(PpsPicturePane));

		#endregion

		#endregion

		#region Toolbar

		public PpsUICommandCollection Commands => (PpsUICommandCollection)GetValue(CommandsProperty);

		private void AddToolbarCommands()
		{
			#region Undo/Redo
			UndoManagerListBox listBox;

			var undoCommand = new PpsUISplitCommandButton()
			{
				Order = new PpsCommandOrder(200, 130),
				DisplayText = "Rückgängig",
				Description = "Rückgängig",
				Image = "undoImage",
				DataContext = this,
				Command = new PpsCommand(
					(args) =>
					{
						strokeUndoManager.Undo();
					},
					(args) => strokeUndoManager?.CanUndo ?? false
				),
				Popup = new System.Windows.Controls.Primitives.Popup()
				{
					Child = listBox = new UndoManagerListBox()
					{
						Style = (Style)Application.Current.FindResource("UndoManagerListBoxStyle")
					}
				}
			};
			listBox.SetBinding(FrameworkElement.DataContextProperty, new Binding("DataContext.UndoM"));

			var redoCommand = new PpsUISplitCommandButton()
			{
				Order = new PpsCommandOrder(200, 140),
				DisplayText = "Wiederholen",
				Description = "Wiederholen",
				Image = "redoImage",
				DataContext = this,
				Command = new PpsCommand(
					(args) =>
					{
						strokeUndoManager.Redo();
					},
					(args) => strokeUndoManager?.CanRedo ?? false
				),
				Popup = new System.Windows.Controls.Primitives.Popup()
				{
					Child = listBox = new UndoManagerListBox()
					{
						Style = (Style)Application.Current.FindResource("UndoManagerListBoxStyle")
					}
				}
			};
			listBox.SetBinding(FrameworkElement.DataContextProperty, new Binding("DataContext.RedoM"));

			Commands.Add(undoCommand);
			Commands.Add(redoCommand);
			#endregion

			#region Strokes

			var freeformeditCommandButton = new PpsUISplitCommandButton()
			{
				Order = new PpsCommandOrder(300, 110),
				DisplayText = "Freihand",
				Description = "Kennzeichnungen hinzufügen",
				Image = "freeformeditImage",
				Command = new PpsCommand(
						(args) =>
						{
							InkEditMode = InkCanvasEditingMode.Ink;
						},
						(args) => SelectedAttachment != null
					),
				Popup = new System.Windows.Controls.Primitives.Popup()
				{
					Child = new UserControl()
					{
						Style = (Style)this.FindResource("PPSnStrokeSettingsControlStyle"),
						DataContext = StrokeSettings
					}
				}
			};
			Commands.Add(freeformeditCommandButton);

			var removestrokeCommandButton = new PpsUICommandButton()
			{
				Order = new PpsCommandOrder(300, 120),
				DisplayText = "Löschen",
				Description = "Linienzug entfernen",
				Image = "removestrokeImage",
				Command = new PpsCommand(
						(args) =>
						{
							InkEditMode = InkCanvasEditingMode.EraseByStroke;
						},
						(args) => SelectedAttachment != null && InkStrokes.Count > 0
					)
			};
			Commands.Add(removestrokeCommandButton);

			#endregion

			#region Misc
			var saveCommandButton = new PpsUICommandButton()
			{
				Order = new PpsCommandOrder(400, 110),
				DisplayText = "Speichern",
				Description = "Bild speichern",
				Image = "floppy_diskImage",
				Command = new PpsCommand(
						(args) =>
						{
							ApplicationCommands.Save.Execute(args, this);
						},
						(args) => ApplicationCommands.Save.CanExecute(args, this)
					)
			};
			Commands.Add(saveCommandButton);

			#endregion
		}

		#endregion

		#endregion

		#region IPpsWindowPane

		public string Title => "Bildeditor";

		// not useable - name of the Object is unknown on creation and after that read-only
		public string SubTitle => String.Empty;

		public object Control => this;

		public IPpsPWindowPaneControl PaneControl => null;

		public bool IsDirty => false;

		public bool HasSideBar => false;

		public event PropertyChangedEventHandler PropertyChanged;

		public PpsWindowPaneCompareResult CompareArguments(LuaTable otherArgumens)
		{
			return PpsWindowPaneCompareResult.Reload;
		}

		public void Dispose()
		{
			ResetCharmObject();
		}

		public Task LoadAsync(LuaTable args)
		{
			var environment = (args["Environment"] as PpsEnvironment) ?? PpsEnvironment.GetEnvironment(this);
			//DataContext = environment;

			Attachments = (args["Attachments"] as IPpsAttachments);

			return Task.CompletedTask;
		} // proc LoadAsync

		public Task<bool> UnloadAsync(bool? commit = null)
		{
			if (SelectedAttachment != null && strokeUndoManager.CanUndo)
				switch (MessageBox.Show("Sie haben ungespeicherte Änderungen!\nMöchten Sie diese noch speichern?", "Warnung", MessageBoxButton.YesNoCancel))
				{
					case MessageBoxResult.Yes:
						ApplicationCommands.Save.Execute(null, null);
						break;
					case MessageBoxResult.Cancel:
						return Task.FromResult(false);
				}

			ResetCharmObject();
			return Task.FromResult(true);
		}

		#endregion

		#region Charmbar

		private PpsObject originalObject;

		private void ResetCharmObject()
		{
			var wnd = (PpsWindow)Application.Current.Windows.OfType<Window>().FirstOrDefault(c => c.IsActive);

			((dynamic)wnd).CharmObject = originalObject;
		}
		private void SetCharmObject(PpsObject obj)
		{
			var wnd = (PpsWindow)Application.Current.Windows.OfType<Window>().FirstOrDefault(c => c.IsActive);

			if (originalObject == null)
				originalObject = ((dynamic)wnd).CharmObject;

			((dynamic)wnd).CharmObject = obj;
		}

		#endregion

		#region Development

		private void DevelopmentSetConstants()
		{
			var StrokeThicknesses = new List<PpsPecStrokeThickness>
			{
				new PpsPecStrokeThickness("1", 1),
				new PpsPecStrokeThickness("5", 5),
				new PpsPecStrokeThickness("10", 10),
				new PpsPecStrokeThickness("15", 15)
			};
			Debug("added thickness constants");

			var StrokeColors = new List<PpsPecStrokeColor>
			{
				new PpsPecStrokeColor("Weiß", Colors.White),
				new PpsPecStrokeColor("Schwarz", Colors.Black),
				new PpsPecStrokeColor("Rot", Colors.Red),
				new PpsPecStrokeColor("Grün", Colors.Green),
				new PpsPecStrokeColor("Blau", Colors.Blue)
			};
			Debug("added color constants");

			StrokeSettings = new PpsPecStrokeSettings(StrokeColors, StrokeThicknesses);

			var cameraPreviews = new ObservableCollection<PpsPecCamera>();
			var devices = DsDevice.GetDevicesOfCat(DirectShowLib.FilterCategory.VideoInputDevice);
			foreach (var dev in devices)
			{
				cameraPreviews.Add(new PpsPecCamera(dev.Name, dev.Name));
				Debug($"added camera \"{dev.Name}\"");
			}

			CameraEnum = cameraPreviews;

			InkStrokes = new StrokeCollection();
			Debug("init InkStrokes");


			InkDrawingAttributes = new DrawingAttributes();
			Debug("init DrawingAttributes");
		}

		[Obsolete("Remove Debug Messages")]
		private void Debug(string msg)
		{
			if (environment != null)
				environment.Traces.AppendText(PpsTraceItemType.Debug, msg);
		}


		#endregion

		private async Task<PpsObject> IncludePictureAsync(string imagePath)
		{
			PpsObject obj;

			using (var trans = await environment.MasterData.CreateTransactionAsync(PpsMasterDataTransactionLevel.Write))
			{
				obj = await environment.CreateNewObjectFromFileAsync(imagePath);

				trans.Commit();
			}

			return obj;
		} // proc CapturePicutureAsync 

		private void ShowOnlyObjectImageDataFilter(object sender, FilterEventArgs e)
		{
			e.Accepted =
				e.Item is IPpsAttachmentItem item
				&& item.LinkedObject != null
				&& item.LinkedObject.Typ == PpsEnvironment.AttachmentObjectTyp
				&& item.LinkedObject.MimeType.StartsWith("image/", StringComparison.OrdinalIgnoreCase);
		} // proc ShowOnlyObjectImageDataFilter

		#region Propertys

		public IEnumerable<object> UndoM => (from un in strokeUndoManager where un.Type == PpsUndoStepType.Undo orderby un.Index descending select un).ToArray();
		public IEnumerable<object> RedoM => (from un in strokeUndoManager where un.Type == PpsUndoStepType.Redo orderby un.Index select un).ToArray();

		public IPpsAttachments Attachments
		{
			get { return (IPpsAttachments)GetValue(AttachmentsProperty); }
			set { SetValue(AttachmentsProperty, value); }
		}

		public ObservableCollection<PpsPecCamera> CachedCameras
		{
			get { return (ObservableCollection<PpsPecCamera>)GetValue(CachedCamerasProperty); }
			set { SetValue(CachedCamerasProperty, value); }
		}

		public IPpsAttachmentItem SelectedAttachment
		{
			get { return (IPpsAttachmentItem)GetValue(SelectedAttachmentProperty); }
			set { SetValue(SelectedAttachmentProperty, value); }
		}

		public string SelectedCamera
		{
			get { return (string)GetValue(SelectedCameraProperty); }
			set { SetValue(SelectedCameraProperty, value); }
		}

		public ObservableCollection<PpsPecCamera> CameraEnum
		{
			get { return (ObservableCollection<PpsPecCamera>)GetValue(CameraEnumProperty); }
			set { SetValue(CameraEnumProperty, value); }
		}

		public StrokeCollection InkStrokes
		{
			get { return (StrokeCollection)GetValue(InkStrokesProperty); }
			set { SetValue(InkStrokesProperty, value); }
		}

		public InkCanvasEditingMode InkEditMode
		{
			get { return (InkCanvasEditingMode)GetValue(InkEditModeProperty); }
			private set
			{
				SetValue(InkEditModeProperty, value);
				var t = (InkCanvas)FindChildElement(typeof(InkCanvas), this);
				switch ((InkCanvasEditingMode)value)
				{
					case InkCanvasEditingMode.Ink:
						t.MouseMove -= InkCanvasRemoveHitTest;
						InkEditCursor = Cursors.Pen;
						break;
					case InkCanvasEditingMode.EraseByStroke:
						InkEditCursor = Cursors.Cross;
						t.MouseMove += InkCanvasRemoveHitTest;
						break;
					case InkCanvasEditingMode.None:
						t.MouseMove -= InkCanvasRemoveHitTest;
						InkEditCursor = Cursors.Hand;
						break;
				}
			}
		}

		private void InkCanvasRemoveHitTest(object sender, MouseEventArgs e)
		{
			var hit = false;
			var pos = e.GetPosition((InkCanvas)sender);
			foreach (var stroke in InkStrokes)
				if (stroke.HitTest(pos))
				{
					hit = true;
					break;
				}
			InkEditCursor = hit ? Cursors.No : Cursors.Cross;
		}

		public Cursor InkEditCursor
		{
			get { return (Cursor)GetValue(InkEditCursorProperty); }
			private set { SetValue(InkEditCursorProperty, value); }
		}

		public DrawingAttributes InkDrawingAttributes
		{
			get { return (DrawingAttributes)GetValue(InkDrawingAttributesProperty); }
			private set { SetValue(InkDrawingAttributesProperty, value); }
		}

		public Matrix ScaleMatrix
		{
			get { return GetValue(ScaleMatrixProperty) != null ? (Matrix)GetValue(ScaleMatrixProperty) : new Matrix(1, 0, 0, 1, 0, 0); }
			private set { SetValue(ScaleMatrixProperty, value); }
		}

		public PpsPecStrokeSettings StrokeSettings
		{
			get { return (PpsPecStrokeSettings)GetValue(StrokeSettingsProperty); }
			private set { SetValue(StrokeSettingsProperty, value); }
		}

		#region DependencyPropertys

		public static readonly DependencyProperty AttachmentsProperty = DependencyProperty.Register(nameof(Attachments), typeof(IPpsAttachments), typeof(PpsPicturePane));
		public static readonly DependencyProperty CachedCamerasProperty = DependencyProperty.Register(nameof(CachedCameras), typeof(ObservableCollection<PpsPecCamera>), typeof(PpsPicturePane));

		public readonly static DependencyProperty SelectedAttachmentProperty = DependencyProperty.Register(nameof(SelectedAttachment), typeof(IPpsAttachmentItem), typeof(PpsPicturePane));
		public readonly static DependencyProperty SelectedCameraProperty = DependencyProperty.Register(nameof(SelectedCamera), typeof(string), typeof(PpsPicturePane));
		public readonly static DependencyProperty CameraEnumProperty = DependencyProperty.Register(nameof(CameraEnum), typeof(ObservableCollection<PpsPecCamera>), typeof(PpsPicturePane));
		public readonly static DependencyProperty InkDrawingAttributesProperty = DependencyProperty.Register(nameof(InkDrawingAttributes), typeof(DrawingAttributes), typeof(PpsPicturePane));
		public readonly static DependencyProperty InkStrokesProperty = DependencyProperty.Register(nameof(InkStrokes), typeof(StrokeCollection), typeof(PpsPicturePane));
		public readonly static DependencyProperty InkEditModeProperty = DependencyProperty.Register(nameof(InkEditMode), typeof(InkCanvasEditingMode), typeof(PpsPicturePane));
		public readonly static DependencyProperty InkEditCursorProperty = DependencyProperty.Register(nameof(InkEditCursor), typeof(Cursor), typeof(PpsPicturePane));
		public readonly static DependencyProperty ScaleMatrixProperty = DependencyProperty.Register(nameof(ScaleMatrix), typeof(Matrix), typeof(PpsPicturePane));
		public readonly static DependencyProperty StrokeSettingsProperty = DependencyProperty.Register(nameof(StrokeSettings), typeof(PpsPecStrokeSettings), typeof(PpsPicturePane));

		#endregion

		#endregion

		/// <summary>
		/// THis function calculates the Matrix to overlay the InkCanvas onto the Image
		/// </summary>
		/// <param name="sender">main image</param>
		/// <param name="e">unused</param>
		private void CurrentObjectImageMax_SizeChanged(object sender, SizeChangedEventArgs e)
		{
			var xfact = (double)GetValue(Window.ActualWidthProperty) / ((Image)sender).ActualWidth;
			var yfact = ((double)GetValue(Window.ActualHeightProperty) - 200) / ((Image)sender).ActualHeight;
			// the factors may become NaN (sender.Actual was zero) or infinity - thus scaling would fail
			xfact = (xfact > 0 && xfact < 100) ? xfact : 1;
			yfact = (yfact > 0 && yfact < 100) ? yfact : 1;
			ScaleMatrix = new Matrix(xfact, 0, 0, yfact, 0, 0);
		}
	}
}

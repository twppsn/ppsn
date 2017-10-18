using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
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

		public class PpsPecCamera
		{
			private string name;
			private string friendlyName;

			public PpsPecCamera(string Name, string FriendlyName)
			{
				this.name = Name;
				this.friendlyName = FriendlyName;
			}

			public string Name => name;
			public string FriendlyName => friendlyName;
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

		public PpsUndoManager strokeUndoManager;

		#endregion

		private readonly PpsEnvironment environment;

		private List<string> captureSourceNames = new List<string>();

		public PpsPicturePane()
		{
			InitializeComponent();

			Resources[PpsEnvironment.WindowPaneService] = this;

			environment = PpsEnvironment.GetEnvironment(this) ?? throw new ArgumentNullException("environment");

			DevelopmentSetConstants();

			AddCommandBindings();

			strokeUndoManager = new PpsUndoManager();

			strokeUndoManager.CollectionChanged += (sender, e) => { PropertyChanged?.Invoke(null, new PropertyChangedEventArgs("RedoM")); PropertyChanged?.Invoke(null, new PropertyChangedEventArgs("UndoM")); };

			SetValue(commandsPropertyKey, new PpsUICommandCollection());

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
			UndoManagerListBox listBox1;
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
					Child = listBox1 = new UndoManagerListBox()
					{
						Style = (Style)Application.Current.FindResource("UndoManagerListBoxStyle")
					}
				}
			};

			listBox1.SetBinding(FrameworkElement.DataContextProperty, new Binding("DataContext.RedoM"));

			Commands.Add(undoCommand);
			Commands.Add(redoCommand);
			#endregion

			ListBox lb;

			var freeformeditCommand = new PpsUISplitCommandButton()
			{
				Order = new PpsCommandOrder(200, 140),
				DisplayText = "Freihand",
				Description = "Kennzeichnungen hinzufügen",
				Image = "freeformeditImage",
				DataContext = this,
				Command = new PpsCommand(
						(args) =>
						{
							InkEditMode = InkCanvasEditingMode.Ink;
						},
						(args) => SelectedAttachment != null
					),
				Popup = new System.Windows.Controls.Primitives.Popup()
				{
					Child = lb = new ListBox()
					{
						Style = (Style)this.FindResource("PPSnColorListBoxStyle"),
						ItemsSource = StrokeColors
					}
				}
			};
			Commands.Add(freeformeditCommand);

			var removestrokeCommand = new PpsUICommandButton()
			{
				Order = new PpsCommandOrder(200, 140),
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

			Commands.Add(removestrokeCommand);
		}

		public object UndoM => (from un in strokeUndoManager where un.Type == PpsUndoStepType.Undo orderby un.Index select un).ToArray();
		public object RedoM => (from un in strokeUndoManager where un.Type == PpsUndoStepType.Redo orderby un.Index select un).ToArray();

		public PpsUICommandCollection Commands => (PpsUICommandCollection)GetValue(CommandsProperty);

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
			if (strokeUndoManager.CanUndo)
				return Task.FromResult(false);

			ResetCharmObject();
			return Task.FromResult(true);
		}

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

		// -- ORIGINAL PpsPicturePane.xaml.cs --------------------------------------------------------------
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
							SelectedAttachment = i;
							SelectedCamera = null;
							var imgData = await i.LinkedObject.GetDataAsync<PpsObjectBlobData>();

							ShowEditCommands();
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
					var preview = (VideoCaptureElement)e.OriginalSource;

					ShowCamera(preview, (string)e.Parameter);
					ShowLiveCommands();

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

			CommandBindings.Add(new CommandBinding(
				OverlayRevertCommand,
				(sender, e) =>
				{
					strokeUndoManager.Undo(strokeUndoManager.Count());
				},
				(sender, e) => e.CanExecute = strokeUndoManager.CanUndo));
		}

		#endregion

		#region Development

		private void DevelopmentSetConstants()
		{
			StrokeThicknesses = new List<PpsPecStrokeThickness>
			{
				new PpsPecStrokeThickness("1", 1),
				new PpsPecStrokeThickness("5", 5),
				new PpsPecStrokeThickness("10", 10),
				new PpsPecStrokeThickness("15", 15)
			};
			Debug("added thickness constants");

			StrokeColors = new List<PpsPecStrokeColor>
			{
				new PpsPecStrokeColor("Weiß", Colors.White),
				new PpsPecStrokeColor("Schwarz", Colors.Black),
				new PpsPecStrokeColor("Rot", Colors.Red),
				new PpsPecStrokeColor("Grün", Colors.Green),
				new PpsPecStrokeColor("Blau", Colors.Blue)
			};
			Debug("added color constants");

			var cameraPreviews = new List<PpsPecCamera>();
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
				Dispatcher.Invoke((() => ShowLiveCommands()));
			});
		}

		private void ShowLiveCommands()
		{
			var actCommands = new List<PpsPecCommand>
			{
				new PpsPecCommand("Speichern", 150, null, ApplicationCommands.Save, null)
			};

			ShowToolPropertys = false;
			PictureTools = actCommands;
		}

		private void ShowEditCommands()
		{
			var actCommands = new List<PpsPecCommand>();

			BezierSegment curve = new BezierSegment(new Point(0, 0), new Point(50, 50), new Point(30, 50), true);
			BezierSegment curve1 = new BezierSegment(new Point(50, 50), new Point(100, 100), new Point(90, 100), true);
			// Set up the Path to insert the segments
			PathGeometry path = new PathGeometry();

			PathFigure pathFigure = new PathFigure
			{
				StartPoint = new Point(0, 0),
				IsClosed = false
			};
			path.Figures.Add(pathFigure);

			pathFigure.Segments.Add(curve);
			pathFigure.Segments.Add(curve1);
			var p = new System.Windows.Shapes.Path
			{
				Data = path,
				Stroke = new SolidColorBrush(System.Windows.Media.Color.FromRgb(100, 100, 255)),
				StrokeThickness = 3
			};

			actCommands.Add(new PpsPecCommand("Freihand", 200, null, OverlayEditFreehandCommand, p));
			/*
			var rect = new System.Windows.Shapes.Rectangle();
			rect.Width = 100;
			rect.Height = 75;
			rect.Fill = new SolidColorBrush(System.Windows.Media.Color.FromRgb(100, 100, 255));

			actCommands.Add(new PpsPictureCommand("Rechteck", 200, null, null, rect));

			var line = new System.Windows.Shapes.Polyline();
			line.Points.Add(new Point(20, 0));
			line.Points.Add(new Point(20, 40));
			line.Points.Add(new Point(0, 40));
			line.Points.Add(new Point(0, 75));
			line.Points.Add(new Point(30, 100));
			line.Points.Add(new Point(100, 100));
			line.Points.Add(new Point(100, 0));
			line.Points.Add(new Point(30, 0));
			line.Stroke = new SolidColorBrush(System.Windows.Media.Color.FromRgb(100, 100, 255));
			line.StrokeThickness = 3;

			actCommands.Add(new PpsPictureCommand("Linie", 200, null, null, line));
			*/
			actCommands.Add(new PpsPecCommand("Radiergummi", 200, null, OverlayRemoveStrokeCommand, null));

			actCommands.Add(new PpsPecCommand("Undo", 100, null, ApplicationCommands.Undo, null));
			actCommands.Add(new PpsPecCommand("Redo", 100, null, ApplicationCommands.Redo, null));

			PictureTools = actCommands;

			InkStrokes = new StrokeCollection(); // if edit mode is entered - empty the strokes
		}

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

		public IPpsAttachments Attachments
		{
			get { return (IPpsAttachments)GetValue(AttachmentsProperty); }
			set { SetValue(AttachmentsProperty, value); }
		} // prop Attachments

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

		public List<PpsPecCamera> CameraEnum
		{
			get { return (List<PpsPecCamera>)GetValue(CameraEnumProperty); }
			set { SetValue(CameraEnumProperty, value); }
		}

		public List<PpsPecCommand> PictureTools
		{
			get { return (List<PpsPecCommand>)GetValue(PictureToolsProperty); }
			set { SetValue(PictureToolsProperty, value); }
		}
		public PpsPecCommand SelectedCommand
		{
			get { return (PpsPecCommand)GetValue(SelectedCommandProperty); }
			set { SetValue(SelectedCommandProperty, value); }
		}

		public StrokeCollection InkStrokes
		{
			get { return (StrokeCollection)GetValue(InkStrokesProperty); }
			set { SetValue(InkStrokesProperty, value); }
		}

		public InkCanvasEditingMode InkEditMode
		{
			get { return (InkCanvasEditingMode)GetValue(InkEditModeProperty); }
			set
			{
				SetValue(InkEditModeProperty, value);
				switch ((InkCanvasEditingMode)value)
				{
					case InkCanvasEditingMode.Ink:
						InkEditCursor = Cursors.Pen;
						ShowToolPropertys = true;
						break;
					case InkCanvasEditingMode.EraseByStroke:
						InkEditCursor = Cursors.No;
						ShowToolPropertys = false;
						break;
					case InkCanvasEditingMode.None:
						InkEditCursor = Cursors.Hand;
						ShowToolPropertys = false;
						break;
				}
			}
		}

		public Cursor InkEditCursor
		{
			get { return (Cursor)GetValue(InkEditCursorProperty); }
			set { SetValue(InkEditCursorProperty, value); }
		}

		public List<PpsPecStrokeThickness> StrokeThicknesses
		{
			get { return (List<PpsPecStrokeThickness>)GetValue(StrokeThicknessesProperty); }
			set { SetValue(StrokeThicknessesProperty, value); }
		}
		public List<PpsPecStrokeColor> StrokeColors
		{
			get { return (List<PpsPecStrokeColor>)GetValue(StrokeColorsProperty); }
			set { SetValue(StrokeColorsProperty, value); }
		}


		public DrawingAttributes InkDrawingAttributes
		{
			get { return (DrawingAttributes)GetValue(InkDrawingAttributesProperty); }
			set { SetValue(InkDrawingAttributesProperty, value); }
		}

		public bool ShowToolPropertys
		{
			get { return (bool)GetValue(ShowToolPropertysProperty) && String.IsNullOrEmpty(SelectedCamera); }
			set { SetValue(ShowToolPropertysProperty, value); }
		}

		public Matrix ScaleMatrix
		{
			get { return GetValue(ScaleMatrixProperty) != null ? (Matrix)GetValue(ScaleMatrixProperty) : new Matrix(1, 0, 0, 1, 0, 0); }
			set { SetValue(ScaleMatrixProperty, value); }
		}

		// -- Static --------------------------------------------------------------

		public static readonly DependencyProperty AttachmentsProperty = DependencyProperty.Register(nameof(Attachments), typeof(IPpsAttachments), typeof(PpsPicturePane));
		public readonly static DependencyProperty SelectedAttachmentProperty = DependencyProperty.Register(nameof(SelectedAttachment), typeof(IPpsAttachmentItem), typeof(PpsPicturePane));
		public readonly static DependencyProperty SelectedCameraProperty = DependencyProperty.Register(nameof(SelectedCamera), typeof(string), typeof(PpsPicturePane));
		public readonly static DependencyProperty CameraEnumProperty = DependencyProperty.Register(nameof(CameraEnum), typeof(List<PpsPecCamera>), typeof(PpsPicturePane));
		public readonly static DependencyProperty PictureToolsProperty = DependencyProperty.Register(nameof(PictureTools), typeof(List<PpsPecCommand>), typeof(PpsPicturePane));
		public readonly static DependencyProperty StrokeThicknessesProperty = DependencyProperty.Register(nameof(StrokeThicknesses), typeof(List<PpsPecStrokeThickness>), typeof(PpsPicturePane));
		public readonly static DependencyProperty StrokeColorsProperty = DependencyProperty.Register(nameof(StrokeColors), typeof(List<PpsPecStrokeColor>), typeof(PpsPicturePane));
		public readonly static DependencyProperty InkDrawingAttributesProperty = DependencyProperty.Register(nameof(InkDrawingAttributes), typeof(DrawingAttributes), typeof(PpsPicturePane));
		public readonly static DependencyProperty SelectedCommandProperty = DependencyProperty.Register(nameof(SelectedCommand), typeof(PpsPecCommand), typeof(PpsPicturePane));
		public readonly static DependencyProperty InkStrokesProperty = DependencyProperty.Register(nameof(InkStrokes), typeof(StrokeCollection), typeof(PpsPicturePane));
		public readonly static DependencyProperty InkEditModeProperty = DependencyProperty.Register(nameof(InkEditMode), typeof(InkCanvasEditingMode), typeof(PpsPicturePane));
		public readonly static DependencyProperty InkEditCursorProperty = DependencyProperty.Register(nameof(InkEditCursor), typeof(Cursor), typeof(PpsPicturePane));
		public readonly static DependencyProperty ShowToolPropertysProperty = DependencyProperty.Register(nameof(ShowToolPropertys), typeof(bool), typeof(PpsPicturePane));
		public readonly static DependencyProperty ScaleMatrixProperty = DependencyProperty.Register(nameof(ScaleMatrix), typeof(Matrix), typeof(PpsPicturePane));

		private static readonly DependencyPropertyKey commandsPropertyKey = DependencyProperty.RegisterReadOnly(nameof(Commands), typeof(PpsUICommandCollection), typeof(PpsPicturePane), new FrameworkPropertyMetadata(null));
		public static readonly DependencyProperty CommandsProperty = commandsPropertyKey.DependencyProperty;

		public static readonly RoutedUICommand EditOverlayCommand = new RoutedUICommand("EditOverlay", "EditOverlay", typeof(PpsPicturePane));
		public static readonly RoutedUICommand OverlayEditFreehandCommand = new RoutedUICommand("EditFreeForm", "EditFreeForm", typeof(PpsPicturePane));
		public static readonly RoutedUICommand OverlayRemoveStrokeCommand = new RoutedUICommand("EditRubber", "EditRubber", typeof(PpsPicturePane));
		public static readonly RoutedUICommand OverlaySetThicknessCommand = new RoutedUICommand("SetThickness", "SetThickness", typeof(PpsPicturePane));
		public static readonly RoutedUICommand OverlaySetColorCommand = new RoutedUICommand("SetColor", "SetColor", typeof(PpsPicturePane));
		public static readonly RoutedUICommand OverlayRevertCommand = new RoutedUICommand("RevertAllChanges", "RevertAllChanges", typeof(PpsPicturePane));
		public readonly static RoutedUICommand ChangeCameraCommand = new RoutedUICommand("ChangeCamera", "ChangeCamera", typeof(PpsPicturePane));

		private void CurrentObjectImageMax_SizeChanged(object sender, SizeChangedEventArgs e)
		{
			var xfact = (((double)GetValue(Window.ActualWidthProperty) / 5) * 4) / ((Image)sender).ActualWidth;
			var yfact = ((((double)GetValue(Window.ActualHeightProperty) - 30) / 4) * 3) / ((Image)sender).ActualHeight;
			// the factors may become NaN (sender.Actual was zero) or infinity - thus scaling would fail
			xfact = (xfact > 0 && xfact < 100) ? xfact : 1;
			yfact = (yfact > 0 && yfact < 100) ? yfact : 1;
			ScaleMatrix = new Matrix(xfact, 0, 0, yfact, 0, 0);
		}
	}
}

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
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using AForge.Video;
using AForge.Video.DirectShow;
using Neo.IronLua;
using TecWare.PPSn.Controls;
using TecWare.PPSn.Data;

namespace TecWare.PPSn.UI
{
	/// <summary>
	/// Interaction logic for PpsPicturePane.xaml
	/// </summary>
	public partial class PpsPicturePane : UserControl, IPpsWindowPane
	{
		#region -- Helper Classes -----------------------------------------------------

		#region -- Data Representation ------------------------------------------------

		public class PpsAforgeCamera : INotifyPropertyChanged, IDisposable
		{
			#region ---- Helper Classes ----------------------------------------------------------

			public class CameraProperty : INotifyPropertyChanged
			{
				#region ---- Readonly ---------------------------------------------------------------

				private readonly AForge.Video.DirectShow.CameraControlProperty property;
				private readonly VideoCaptureDevice device;

				private readonly int minValue;
				private readonly int maxValue;
				private readonly int defaultValue;
				private readonly int stepSize;
				private readonly bool flagable;

				#endregion

				#region ---- Events -----------------------------------------------------------------

				public event PropertyChangedEventHandler PropertyChanged;

				#endregion

				#region ---- Constructor ------------------------------------------------------------

				public CameraProperty(VideoCaptureDevice device, AForge.Video.DirectShow.CameraControlProperty property)
				{
					this.device = device;
					this.property = property;

					AForge.Video.DirectShow.CameraControlFlags flags;
					device.GetCameraPropertyRange(property, out minValue, out maxValue, out stepSize, out defaultValue, out flags);
					this.flagable = flags != AForge.Video.DirectShow.CameraControlFlags.None;
				}

				#endregion

				#region ---- Properties -------------------------------------------------------------

				public string Name => Enum.GetName(typeof(AForge.Video.DirectShow.CameraControlProperty), property);
				public int MinValue => minValue;
				public int MaxValue => maxValue;
				public int DefaultValue => defaultValue;
				public int StepSize => stepSize;
				public bool Flagable => flagable;
				public bool AutomaticValue
				{
					get
					{
						if (!flagable)
							return true;
						int tmp;
						AForge.Video.DirectShow.CameraControlFlags flag;
						device.GetCameraProperty(property, out tmp, out flag);
						return (flag == AForge.Video.DirectShow.CameraControlFlags.Auto);
					}
					set
					{
						if (!flagable)
							throw new FieldAccessException();

						// there is no use in setting the flag to manual
						if (value)
							device.SetCameraProperty(property, defaultValue, AForge.Video.DirectShow.CameraControlFlags.Auto);

						PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(AutomaticValue)));
					}
				}
				public int Value
				{
					get
					{
						int tmp;
						AForge.Video.DirectShow.CameraControlFlags flag;
						device.GetCameraProperty(property, out tmp, out flag);
						return tmp;
					}
					set
					{
						device.SetCameraProperty(property, value, AForge.Video.DirectShow.CameraControlFlags.Manual);
						PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(AutomaticValue)));
					}
				}

				#endregion
			}

			#endregion

			#region ---- Readonly ----------------------------------------------------------------

			private readonly IEnumerable<CameraProperty> properties;

			#endregion

			#region ---- Events ------------------------------------------------------------------

			public event PropertyChangedEventHandler PropertyChanged;

			#endregion

			#region ---- Fields ------------------------------------------------------------------

			private VideoCaptureDevice device;
			private object preview;
			private PpsTraceLog traces;
			private VideoCapabilities previewResolution;
			private string name;

			#endregion

			#region ---- Constructor -------------------------------------------------------------

			public PpsAforgeCamera(AForge.Video.DirectShow.FilterInfo deviceFilter, PpsTraceLog traceLog, int previewMaxWidth = 800, int previewMinFPS = 15)
			{
				this.traces = traceLog;

				this.name = deviceFilter.Name;

				// initialize the device
				try
				{
					device = new VideoCaptureDevice(deviceFilter.MonikerString);
				}
				catch (Exception)
				{
					traces.AppendText(PpsTraceItemType.Fail, $"Camera \"{deviceFilter.Name}({deviceFilter.MonikerString})\" is (currently) not useable.");
					device = null;
					return;
				}

				// attach failure handling
				device.VideoSourceError += (sender, e) => traces.AppendText(PpsTraceItemType.Fail, "Camera: " + e.Description);

				// find the highest snapshot resolution
				var maxSnapshotResolution = (from vc in device.SnapshotCapabilities orderby vc.FrameSize.Width * vc.FrameSize.Height descending select vc).FirstOrDefault();

				// there are cameras without snapshot capability
				if (maxSnapshotResolution != null)
				{
					device.ProvideSnapshots = true;
					device.SnapshotResolution = maxSnapshotResolution;

					// attach the event handler for snapshots
					device.SnapshotFrame += SnapshotEvent;
				}

				// find a preview resolution - according to the requirements
				previewResolution = (from vc in device.VideoCapabilities orderby vc.FrameSize.Width descending where vc.FrameSize.Width <= previewMaxWidth where vc.AverageFrameRate >= previewMinFPS select vc).FirstOrDefault();
				if (previewResolution == null)
				{
					// no resolution to the requirements, try to set the highest possible FPS (best for preview)
					traces.AppendText(PpsTraceItemType.Fail, $"Camera \"{deviceFilter.Name}({deviceFilter.MonikerString})\" does not support required quality. Trying fallback.");
					previewResolution = (from vc in device.VideoCapabilities orderby vc.AverageFrameRate descending select vc).FirstOrDefault();

					if (previewResolution == null)
					{
						traces.AppendText(PpsTraceItemType.Fail, $"Camera \"{deviceFilter.Name}({deviceFilter.MonikerString})\" does not publish useable resolutions.");
					}
				}

				if (!device.ProvideSnapshots)
				{
					traces.AppendText(PpsTraceItemType.Information, $"Camera \"{deviceFilter.Name}({deviceFilter.MonikerString})\" does not have Snapshot functionality. Using Framegrabber instead.");
					device.VideoResolution = (from vc in device.VideoCapabilities orderby vc.FrameSize.Width * vc.FrameSize.Height descending select vc).First();
				}
				else
				{
					device.VideoResolution = previewResolution;
				}

				// attach the handler for incoming images
				device.NewFrame += PreviewNewframeEvent;
				device.Start();

				// collect the useable Propertys
				properties = new List<CameraProperty>();
				foreach (AForge.Video.DirectShow.CameraControlProperty prop in Enum.GetValues(typeof(AForge.Video.DirectShow.CameraControlProperty)))
				{
					var property = new CameraProperty(device, prop);
					property.PropertyChanged += (sender, e) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(AutomaticSettings)));
					if (property.MinValue != property.MaxValue)
						((List<CameraProperty>)properties).Add(property);
				}
			}

			#endregion

			#region ---- Methods -----------------------------------------------------------------

			public void MakePhoto()
			{
				if (device.ProvideSnapshots)
				{
					device.SimulateTrigger();
				}
				else
				{
					// remove the regular Frame handler to suppress UI-flickering caused by the changed resolution
					device.NewFrame -= PreviewNewframeEvent;
					// attach the Snaphot event to the regular newFrame
					device.NewFrame += SnapshotEvent;
				}
			}

			public void Dispose()
			{
				device.Stop();
				device.WaitForStop();
			}

			#region ---- Event Handler -----------------------------------------------------------

			private void PreviewNewframeEvent(object sender, NewFrameEventArgs eventArgs)
			{
				using (var ms = new MemoryStream())
				{
					eventArgs.Frame.Save(ms, ImageFormat.Bmp);
					ms.Position = 0;
					preview = ms.ToArray();
				}
				eventArgs.Frame.Dispose();

				PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Preview)));
			}

			private void SnapshotEvent(object sender, NewFrameEventArgs eventArgs)
			{
				SnapShot.Invoke(this, eventArgs);
				if (!device.ProvideSnapshots)
				{
					// remove the Snapshot handler
					device.NewFrame -= SnapshotEvent;
					// reattach the regular Frame handler
					device.NewFrame += PreviewNewframeEvent;
				}
			}

			#endregion

			#endregion

			#region ---- Properties --------------------------------------------------------------

			public object Preview => preview;

			public string Name => name;

			public IEnumerable<CameraProperty> Properties => properties;

			public bool AutomaticSettings => (from prop in properties where !prop.AutomaticValue select prop).Count() == 0;

			public NewFrameEventHandler SnapShot;

			#endregion
		}

		public class PpsPecStrokeThickness
		{
			private string name;
			private double thickness;

			public PpsPecStrokeThickness(string Name, double Thickness)
			{
				this.name = Name;
				this.thickness = Thickness;
			}

			public string Name => name;
			public double Size => thickness;
		}

		public class PpsPecStrokeColor
		{
			private string name;
			private Brush brush;

			public PpsPecStrokeColor(string Name, Brush ColorBrush)
			{
				this.name = Name;
				this.brush = ColorBrush;
			}

			public string Name => name;
			public Color Color
			{
				get
				{
					if (brush is SolidColorBrush scb) return scb.Color;
					if (brush is LinearGradientBrush lgb) return lgb.GradientStops.FirstOrDefault().Color;
					if (brush is RadialGradientBrush rgb) return rgb.GradientStops.FirstOrDefault().Color;
					return Colors.Black;
				}
			}
			public Brush Brush => brush;
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

		/// <summary>This StrokeCollection owns a property if ChangedActions should be traced (ref: https://msdn.microsoft.com/en-US/library/aa972158.aspx )</summary>
		private class PpsDetraceableStrokeCollection : StrokeCollection
		{
			private bool disableTracing = false;

			public PpsDetraceableStrokeCollection(StrokeCollection strokes) : base(strokes)
			{

			}

			/// <summary>If true item changes should not be passed to a UndoManager</summary>
			public bool DisableTracing
			{
				get => disableTracing;
				set => disableTracing = value;
			}
		}

		private class PpsAddStrokeUndoItem : IPpsUndoItem
		{
			private PpsDetraceableStrokeCollection collection;
			private Stroke stroke;

			public PpsAddStrokeUndoItem(PpsDetraceableStrokeCollection collection, Stroke strokeAdded)
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
				collection.DisableTracing = true;
				try
				{
					collection.Add(stroke);
				}
				finally
				{
					collection.DisableTracing = false;
				}
			}

			public void Undo()
			{
				collection.DisableTracing = true;
				try
				{
					collection.Remove(stroke);
				}
				finally
				{
					collection.DisableTracing = false;
				}
			}
		}

		private class PpsRemoveStrokeUndoItem : IPpsUndoItem
		{
			private PpsDetraceableStrokeCollection collection;
			private Stroke stroke;

			public PpsRemoveStrokeUndoItem(PpsDetraceableStrokeCollection collection, Stroke strokeAdded)
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
				collection.DisableTracing = true;
				try
				{
					collection.Remove(stroke);
				}
				finally
				{
					collection.DisableTracing = false;
				}
			}

			public void Undo()
			{
				collection.DisableTracing = true;
				try
				{
					collection.Add(stroke);
				}
				finally
				{
					collection.DisableTracing = false;
				}
			}
		}

		#endregion

		#endregion

		#region -- Events -------------------------------------------------------------

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

		/// <summary>
		/// Checks, if the mouse is over an InkStroke and changes the cursor according
		/// </summary>
		/// <param name="sender">InkCanvas</param>
		/// <param name="e"></param>
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

		#endregion

		#region -- Fields -------------------------------------------------------------

		private PpsUndoManager strokeUndoManager;
		private readonly PpsEnvironment environment;
		private List<string> captureSourceNames = new List<string>();

		#endregion

		#region -- Constructor --------------------------------------------------------

		public PpsPicturePane()
		{
			InitializeComponent();

			Resources[PpsEnvironment.WindowPaneService] = this;

			environment = PpsEnvironment.GetEnvironment(this) ?? throw new ArgumentNullException("environment");

			InitializePenSettings();
			InitializeCameras();
			InitializeStrokes();

			AddCommandBindings();

			strokeUndoManager = new PpsUndoManager();

			strokeUndoManager.CollectionChanged += (sender, e) => { PropertyChanged?.Invoke(null, new PropertyChangedEventArgs("RedoM")); PropertyChanged?.Invoke(null, new PropertyChangedEventArgs("UndoM")); };

			SetValue(commandsPropertyKey, new PpsUICommandCollection());

			AddToolbarCommands();
		}

		#endregion

		#region -- Commands -----------------------------------------------------------

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
							// check if there is already an image displayed and changed
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
							// request the full-sized image
							var imgData = await i.LinkedObject.GetDataAsync<PpsObjectBlobData>();

							var data = await SelectedAttachment.LinkedObject.GetDataAsync<PpsObjectBlobData>();
							InkStrokes = new PpsDetraceableStrokeCollection(await data.GetOverlayAsync() ?? new StrokeCollection());

							InkStrokes.StrokesChanged += (chgsender, chge) =>
							{
								// tracing is disabled, if a undo/redo action caused the changed event, thus preventing it to appear in the undomanager itself
								if (!InkStrokes.DisableTracing)
								{
									using (var trans = strokeUndoManager.BeginTransaction("Linie hinzugefügt"))
									{
										foreach (var stroke in chge.Added)
											strokeUndoManager.Append(new PpsAddStrokeUndoItem((PpsDetraceableStrokeCollection)GetValue(InkStrokesProperty), stroke));
										trans.Commit();
									}
									using (var trans = strokeUndoManager.BeginTransaction("Linie entfernt"))
									{
										foreach (var stroke in chge.Removed)
											strokeUndoManager.Append(new PpsRemoveStrokeUndoItem((PpsDetraceableStrokeCollection)GetValue(InkStrokesProperty), stroke));
										trans.Commit();
									}
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

				},
				(sender, e) => e.CanExecute = strokeUndoManager.CanUndo));

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
				SaveCameraImageCommand,
				(sender, e) =>
				{
					if (SelectedCamera != null)
					{
						SelectedCamera.MakePhoto();
					}
				},
				(sender, e) => e.CanExecute = SelectedCamera != null));

			CommandBindings.Add(new CommandBinding(
				ChangeCameraCommand,
				(sender, e) =>
				{
					SelectedCamera = (PpsAforgeCamera)e.Parameter;
					SelectedAttachment = null;
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

		/// <summary>
		/// Finds the UIElement of a given type in the childs of another control
		/// </summary>
		/// <param name="t">Type of Control</param>
		/// <param name="parent">Parent Control</param>
		/// <returns></returns>
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
		public readonly static RoutedUICommand SaveCameraImageCommand = new RoutedUICommand("SaveCameraImage", "SaveCameraImage", typeof(PpsPicturePane));

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

			var penSettingsPopup = new System.Windows.Controls.Primitives.Popup()
			{
				Child = new UserControl()
				{
					Style = (Style)this.FindResource("PPSnStrokeSettingsControlStyle"),
					DataContext = StrokeSettings
				}
			};
			penSettingsPopup.Opened += (sender, e) => { if (SelectedAttachment != null) InkEditMode = InkCanvasEditingMode.Ink; };

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
				Popup = penSettingsPopup
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

		#region -- IPpsWindowPane -----------------------------------------------------

		public string Title => "Bildeditor";

		private string subTitle;
		public string SubTitle
		{
			get
			{
				if (!String.IsNullOrEmpty(subTitle))
					return subTitle;

				if (originalObject != null)
				{
					subTitle = (string)originalObject["Name"];
					return subTitle;
				}

				var window = Application.Current.Windows.OfType<Window>().FirstOrDefault(c => c.IsActive);
				if (window is PpsWindow ppswindow)
				{
					subTitle = (string)(((PpsObject)((dynamic)ppswindow).CharmObject) ?? originalObject)?["Name"];
					return subTitle;
				}
				return String.Empty;
			}
		}

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
			foreach (var camera in CameraEnum)
				camera.Dispose();
			ResetCharmObject();
		}

		/// <summary>
		/// Loads the content of the panel
		/// </summary>
		/// <param name="args">The LuaTable must at least contain ''environment'' and ''Attachments''</param>
		/// <returns></returns>
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
				switch (MessageBox.Show("Sie haben ungespeicherte Änderungen!\nMöchten Sie diese vor dem Schließen noch speichern?", "Warnung", MessageBoxButton.YesNoCancel))
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

		#region -- Charmbar -----------------------------------------------------------

		/// <summary>variable saving the object, which was loaded before opening the PicturePane</summary>
		private PpsObject originalObject;

		/// <summary>restores the object before loading the PicturePane</summary>
		private void ResetCharmObject()
		{
			if (originalObject == null)
				return;

			var wnd = (PpsWindow)Application.Current.Windows.OfType<Window>().FirstOrDefault(c => c.IsActive);

			((dynamic)wnd).CharmObject = originalObject;
		}

		/// <summary>sets the object of the CharmBar - makes a backup, if it was already set (from the pane requesting the PicturePane)</summary>
		/// <param name="obj">new PpsObject</param>
		private void SetCharmObject(PpsObject obj)
		{
			var wnd = (PpsWindow)Application.Current.Windows.OfType<Window>().FirstOrDefault(c => c.IsActive);

			if (originalObject == null)
				originalObject = ((dynamic)wnd).CharmObject;

			((dynamic)wnd).CharmObject = obj;
		}

		#endregion

		#region -- Methods ------------------------------------------------------------

		#region -- Pen Settings -------------------------------------------------------

		private static LuaTable GetPenColorTable(PpsEnvironment environment)
			=> (LuaTable)environment.GetMemberValue("pictureEditorPenColorTable");

		private static LuaTable GetPenThicknessTable(PpsEnvironment environment)
			=> (LuaTable)environment.GetMemberValue("pictureEditorPenThicknessTable");

		private void InitializePenSettings()
		{
			var StrokeThicknesses = new List<PpsPecStrokeThickness>();
			foreach (var tab in GetPenThicknessTable(environment).ArrayList)
			{
				if (tab is LuaTable lt) StrokeThicknesses.Add(new PpsPecStrokeThickness((string)lt["Name"], (double)lt["Thickness"]));
			}

			var StrokeColors = new List<PpsPecStrokeColor>();
			foreach (var tab in GetPenColorTable(environment).ArrayList)
			{
				if (tab is LuaTable lt) StrokeColors.Add(new PpsPecStrokeColor((string)lt["Name"], (Brush)lt["Brush"]));
			}

			if (StrokeColors.Count == 0)
				environment.Traces.AppendText(PpsTraceItemType.Fail, "Failed to load Brushes for drawing.");
			if (StrokeThicknesses.Count == 0)
				environment.Traces.AppendText(PpsTraceItemType.Fail, "Failed to load Thicknesses for drawing.");

			StrokeSettings = new PpsPecStrokeSettings(StrokeColors, StrokeThicknesses);
		}

		#endregion

		#region -- Hardware / Cameras -------------------------------------------------

		private void InitializeCameras()
		{
			var LocalWebCamsCollection = new FilterInfoCollection(AForge.Video.DirectShow.FilterCategory.VideoInputDevice);
			var cameraPreviews = new ObservableCollection<PpsAforgeCamera>();
			foreach (AForge.Video.DirectShow.FilterInfo cam in LocalWebCamsCollection)
			{
				var pac = new PpsAforgeCamera(cam, environment.Traces);
				pac.SnapShot += (s, e) =>
				{
					var path = System.IO.Path.GetTempPath() + DateTime.Now.ToUniversalTime().ToString("yyyy-MM-dd_HHmmss") + ".jpg";

					e.Frame.Save(path, ImageFormat.Jpeg);
					e.Frame.Dispose();
					PpsObject obj;
					Dispatcher.Invoke(async () =>
					{
						obj = await IncludePictureAsync(path);

						Attachments.Append(obj);

						File.Delete(path);
					}).AwaitTask();
				};
				cameraPreviews.Add(pac);
			}
			CameraEnum = cameraPreviews;
		}

		#endregion

		#region -- Strokes ------------------------------------------------------------

		private void InitializeStrokes()
		{
			InkStrokes = new PpsDetraceableStrokeCollection(new StrokeCollection());

			InkDrawingAttributes = new DrawingAttributes();
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

		#endregion

		#region -- Propertys ----------------------------------------------------------

		public IEnumerable<object> UndoM => (from un in strokeUndoManager where un.Type == PpsUndoStepType.Undo orderby un.Index descending select un).ToArray();
		public IEnumerable<object> RedoM => (from un in strokeUndoManager where un.Type == PpsUndoStepType.Redo orderby un.Index select un).ToArray();

		/// <summary>Binding Point for caller to set the shown attachments</summary>
		public IPpsAttachments Attachments
		{
			get { return (IPpsAttachments)GetValue(AttachmentsProperty); }
			set { SetValue(AttachmentsProperty, value); }
		}

		/// <summary>The Attachmnet which is shown in the editor</summary>
		private IPpsAttachmentItem SelectedAttachment
		{
			get { return (IPpsAttachmentItem)GetValue(SelectedAttachmentProperty); }
			set { SetValue(SelectedAttachmentProperty, value); }
		}

		/// <summary>The camera which is shown in the editor</summary>
		private PpsAforgeCamera SelectedCamera
		{
			get { return (PpsAforgeCamera)GetValue(SelectedCameraProperty); }
			set { SetValue(SelectedCameraProperty, value); }
		}

		/// <summary>The List of cameras which are known to the system - after one is selected it moves to ChachedCameras</summary>
		private ObservableCollection<PpsAforgeCamera> CameraEnum
		{
			get { return (ObservableCollection<PpsAforgeCamera>)GetValue(CameraEnumProperty); }
			set { SetValue(CameraEnumProperty, value); }
		}

		/// <summary>The Strokes made on the shown Image</summary>
		private PpsDetraceableStrokeCollection InkStrokes
		{
			get { return (PpsDetraceableStrokeCollection)GetValue(InkStrokesProperty); }
			set { SetValue(InkStrokesProperty, value); }
		}

		/// <summary>The state of the Editor</summary>
		private InkCanvasEditingMode InkEditMode
		{
			get { return (InkCanvasEditingMode)GetValue(InkEditModeProperty); }
			set
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

		/// <summary>Binding for the Cursor used by the Editor</summary>
		private Cursor InkEditCursor
		{
			get { return (Cursor)GetValue(InkEditCursorProperty); }
			set { SetValue(InkEditCursorProperty, value); }
		}

		/// <summary>The Binding point for Color and Thickness for the Pen</summary>
		private DrawingAttributes InkDrawingAttributes
		{
			get { return (DrawingAttributes)GetValue(InkDrawingAttributesProperty); }
			set { SetValue(InkDrawingAttributesProperty, value); }
		}

		/// <summary>This mAtrix handles the Mapping of the Strokes to the Image resolution-wise</summary>
		private Matrix ScaleMatrix
		{
			get { return GetValue(ScaleMatrixProperty) != null ? (Matrix)GetValue(ScaleMatrixProperty) : new Matrix(1, 0, 0, 1, 0, 0); }
			set { SetValue(ScaleMatrixProperty, value); }
		}

		/// <summary>The Binding point for Color and Thickness possibilities for the Settings Control</summary>
		private PpsPecStrokeSettings StrokeSettings
		{
			get { return (PpsPecStrokeSettings)GetValue(StrokeSettingsProperty); }
			set { SetValue(StrokeSettingsProperty, value); }
		}

		#region DependencyPropertys

		public static readonly DependencyProperty AttachmentsProperty = DependencyProperty.Register(nameof(Attachments), typeof(IPpsAttachments), typeof(PpsPicturePane));

		private readonly static DependencyProperty SelectedAttachmentProperty = DependencyProperty.Register(nameof(SelectedAttachment), typeof(IPpsAttachmentItem), typeof(PpsPicturePane));
		private readonly static DependencyProperty SelectedCameraProperty = DependencyProperty.Register(nameof(SelectedCamera), typeof(PpsAforgeCamera), typeof(PpsPicturePane));
		private readonly static DependencyProperty CameraEnumProperty = DependencyProperty.Register(nameof(CameraEnum), typeof(ObservableCollection<PpsAforgeCamera>), typeof(PpsPicturePane));
		private readonly static DependencyProperty InkDrawingAttributesProperty = DependencyProperty.Register(nameof(InkDrawingAttributes), typeof(DrawingAttributes), typeof(PpsPicturePane));
		private readonly static DependencyProperty InkStrokesProperty = DependencyProperty.Register(nameof(InkStrokes), typeof(PpsDetraceableStrokeCollection), typeof(PpsPicturePane));
		private readonly static DependencyProperty InkEditModeProperty = DependencyProperty.Register(nameof(InkEditMode), typeof(InkCanvasEditingMode), typeof(PpsPicturePane));
		private readonly static DependencyProperty InkEditCursorProperty = DependencyProperty.Register(nameof(InkEditCursor), typeof(Cursor), typeof(PpsPicturePane));
		private readonly static DependencyProperty ScaleMatrixProperty = DependencyProperty.Register(nameof(ScaleMatrix), typeof(Matrix), typeof(PpsPicturePane));
		private readonly static DependencyProperty StrokeSettingsProperty = DependencyProperty.Register(nameof(StrokeSettings), typeof(PpsPecStrokeSettings), typeof(PpsPicturePane));

		#endregion

		#endregion
	}
}

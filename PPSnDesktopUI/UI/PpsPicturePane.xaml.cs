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
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Neo.IronLua;
using TecWare.DE.Networking;
using TecWare.DE.Stuff;
using TecWare.PPSn.Controls;
using TecWare.PPSn.Data;

namespace TecWare.PPSn.UI
{
	#region -- class PpsShapeResourceKey ----------------------------------------------

	/// <summary>Resource key to mark shapes</summary>
	public sealed class PpsShapeResourceKey : PpsTypedResourceKey
	{
		/// <summary></summary>
		/// <param name="name"></param>
		public PpsShapeResourceKey(string name)
			:base(name)
		{
		} // ctor
	} // class PpsShapeResourceKey

	#endregion

	#region -- class PpsColorResourceKey ----------------------------------------------

	/// <summary>Resource key to mark shapes</summary>
	public sealed class PpsColorResourceKey : PpsTypedResourceKey
	{
		/// <summary></summary>
		/// <param name="name"></param>
		public PpsColorResourceKey(string name)
			: base(name)
		{
		} // ctor
	} // class PpsColorResourceKey

	#endregion

	#region -- class PpsThicknessResourceKey ------------------------------------------

	/// <summary>Resource key to mark shapes</summary>
	public sealed class PpsThicknessResourceKey : PpsTypedResourceKey
	{
		/// <summary></summary>
		/// <param name="name"></param>
		public PpsThicknessResourceKey(string name)
			: base(name)
		{
		} // ctor
	} // class PpsThicknessResourceKey

	#endregion

	#region -- class PpsPicturePane ---------------------------------------------------

	/// <summary>Picture view and editor for one image.</summary>
	public partial class PpsPicturePane : PpsWindowPaneControl
	{
		#region -- class GaleryItem ---------------------------------------------------

		private sealed class GaleryItem
		{
			private readonly int zusaId;
			private readonly string name;
			private readonly Uri uri;

			public GaleryItem(int index, int zusaId, string name, Uri uri)
			{
				this.zusaId = zusaId;
				this.name = name ?? $"image{index}";
				this.uri= uri ?? throw new ArgumentNullException(nameof(uri));
			} // ctor

			public int ZusaId => zusaId;
			public string Name => name;
			public Uri Uri => uri;

			public static GaleryItem Create(int index, LuaTable t)
			{
				if (t.TryGetValue<int>("Id", out var zusaId)
						&& t.TryGetValue<string>("Uri", out var uriInfo)
						&& Uri.TryCreate(uriInfo, UriKind.RelativeOrAbsolute, out var uri))
				{
					return new GaleryItem(++index, zusaId, t.GetOptionalValue<string>("Name", null), uri);
				}
				else
					return null;
			} // func Create
		} // class GaleryItem

		#endregion

		#region -- class GaleryInfo ---------------------------------------------------

		private sealed class GaleryInfo
		{
			private readonly string path;
			private readonly GaleryItem[] items;
			private int currentIndex = -1;

			public GaleryInfo(string path, GaleryItem[] items)
			{
				this.path = path ?? throw new ArgumentNullException(nameof(path));
				this.items = items ?? throw new ArgumentNullException(nameof(items));
			} // ctor

			public bool IsSameGalery(string otherPath) 
				=> path == otherPath;

			public bool Update(int id)
			{
				var idx = Array.FindIndex(items, c => c.ZusaId == id);
				if (idx == -1)
				{
					currentIndex = -1;
					return false;
				}
				else
				{
					currentIndex = idx;
					return true;
				}
			} //  proc Update

			public int Count => items.Length;
			public int CurrentIndex => currentIndex;

			public GaleryItem Prev => currentIndex > 0 ? this[currentIndex - 1] : null;
			public GaleryItem Next => currentIndex < items.Length - 1 ? this[currentIndex + 1] : null;

			public GaleryItem this[int index] => items[index];

			public static async Task<GaleryInfo> LoadAsync(DEHttpClient http, string path)
			{
				var galeryInfo = await http.GetTableAsync(path);
				
				var list = new List<GaleryItem>();
				foreach (var cur in galeryInfo.ArrayList.OfType<LuaTable>())
				{
					var item = GaleryItem.Create(list.Count, cur);
					if (item != null)
						list.Add(item);
				}

				return new GaleryInfo(path, list.Count == 0 ? null : list.ToArray());
			} // func LoadAsync
		} // class GaleryInfo

		#endregion

		#region -- CanEdit - Property -------------------------------------------------

		private static readonly DependencyPropertyKey canEditPropertyKey = DependencyProperty.RegisterReadOnly(nameof(CanEdit), typeof(bool), typeof(PpsPicturePane), new FrameworkPropertyMetadata(false));
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
		public static readonly DependencyProperty CanEditProperty = canEditPropertyKey.DependencyProperty;
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

		/// <summary>Can the current image changed.</summary>
		public bool CanEdit { get => (bool)GetValue(CanEditProperty); private set => SetValue(canEditPropertyKey, value); }

		#endregion

		#region -- Images - Property --------------------------------------------------

		private static readonly DependencyPropertyKey imagesPropertyKey = DependencyProperty.RegisterReadOnly(nameof(ImagesProperty), typeof(IEnumerable), typeof(PpsPicturePane), new FrameworkPropertyMetadata(null));
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
		public static readonly DependencyProperty ImagesProperty = imagesPropertyKey.DependencyProperty;
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

		/// <summary>Current visible image.</summary>
		public IEnumerable Images { get => (IEnumerable)GetValue(ImagesProperty); private set => SetValue(imagesPropertyKey, value); }

		#endregion

		#region -- CurrentImage - Property --------------------------------------------

		private static readonly DependencyPropertyKey currentImagePropertyKey = DependencyProperty.RegisterReadOnly(nameof(CurrentImage), typeof(ImageSource), typeof(PpsPicturePane), new FrameworkPropertyMetadata(null));
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
		public static readonly DependencyProperty CurrentImageProperty = currentImagePropertyKey.DependencyProperty;
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

		/// <summary>Current visible image.</summary>
		public ImageSource CurrentImage { get => (ImageSource)GetValue(CurrentImageProperty); private set => SetValue(currentImagePropertyKey, value); }

		#endregion
		
		private IPpsDataInfo currentPictureInfo = null;
		private IPpsDataObject currentPicture = null;

		private double fitToImageAspect = double.NaN;
		private GaleryInfo galeryInfo = null;

		#region -- Ctor/Dtor ----------------------------------------------------------

		/// <summary>initializes the cameras, the settings and the events</summary>
		/// <param name="paneHost"></param>
		public PpsPicturePane(IPpsWindowPaneHost paneHost)
			: base(paneHost)
		{
			InitializeComponent();

			DataContext = this;

			this.AddCommandBinding(paneHost.PaneManager.Shell, ApplicationCommands.New,
				new PpsCommand(
					ctx =>
					{
						shapeCanvas.NewShapeType = ctx.Parameter as IPpsShapeFactory;
						scrollViewer.IsZoomAllowed =
							scrollViewer.IsPanningAllowed = shapeCanvas.NewShapeType == null; // todo: binding
					},
					ctx => !shapeCanvas.IsReadOnly
				)
			);

			this.AddCommandBinding(Shell, MediaCommands.NextTrack, new PpsAsyncCommand(OpenNextImageAsync, CanOpenNextImage));
			this.AddCommandBinding(Shell, MediaCommands.PreviousTrack, new PpsAsyncCommand(OpenPrevImageAsync, CanOpenPrevImage));

			//AddCommandBindings();

			//InitializePenSettings();
			//InitializeStrokes();


			//strokeUndoManager = new PpsUndoManager();

			//strokeUndoManager.CollectionChanged += (sender, e) => { PropertyChanged?.Invoke(null, new PropertyChangedEventArgs("RedoM")); PropertyChanged?.Invoke(null, new PropertyChangedEventArgs("UndoM")); };
		} // ctor

		/// <summary></summary>
		/// <param name="disposing"></param>
		protected override void Dispose(bool disposing)
			=> base.Dispose(disposing);

		/// <summary>Loads the content of the panel</summary>
		/// <param name="args">Load arguments for the picture pane, it is possible to set one object and/or a list of objects.</param>
		/// <returns></returns>
		protected override async Task OnLoadAsync(LuaTable args)
		{
			// set current picture
			if (args["Object"] is IPpsDataInfo dataInfo)
				await OpenPictureAsync(dataInfo);
			else if (args["Object"] is byte[] bytes)
			{
				CurrentImage = BitmapFrame.Create(new MemoryStream(bytes));
				SubTitle = "Bild";
				CanEdit = false;
			}
			else if (args["Source"] is string uriString)
				await OpenPictureFromSourceAsync(new Uri(uriString, UriKind.RelativeOrAbsolute));
			else if (args["Source"] is Uri uri)
				await OpenPictureFromSourceAsync(uri);

			if (args["SubTitle"] is string subTitle)
				SubTitle = subTitle;
		} // proc OnLoadAsync

		/// <summary>Used to destroy the Panel - if there a unsaved changes the user is asked</summary>
		/// <param name="commit">to fulfill the interface</param>
		/// <returns></returns>
		protected override Task<bool> OnUnloadAsync(bool? commit = null)
		{
			Images = null;
			return ClosePictureAsync(commit);
		} // func OnUnloadAsync

		/// <summary></summary>
		/// <param name="otherArguments"></param>
		/// <returns></returns>
		protected override PpsWindowPaneCompareResult CompareArguments(LuaTable otherArguments)
		{
			// check source
			return currentPictureInfo == (otherArguments["Object"] as IPpsDataInfo) ? PpsWindowPaneCompareResult.Same : PpsWindowPaneCompareResult.Reload;
		} // func CompareArguments

		#endregion

		#region -- Galery -------------------------------------------------------------

		private void RefreshGaleryInfo()
		{
			if (galeryInfo != null)
			{
				imageGaleryInfo.Content = new Tuple<int, int>(galeryInfo.CurrentIndex + 1, galeryInfo.Count);
				nextImage.IsVisible = true;
				prevImage.IsVisible = true;
			}
			else
			{
				nextImage.IsVisible = false;
				prevImage.IsVisible = false;
			}
		} // proc RefreshGaleryInfo

		private async Task LoadGaleryImageAsync(GaleryItem item)
		{
			using (this.CreateProgress(progressText: $"Lade {item.Name}..."))
			{
				try
				{
					await OpenPictureFromSourceAsync(item.Uri);
				}
				catch (Exception ex)
				{
					// Update der Galery
					if (galeryInfo.Update(item.ZusaId))
						RefreshGaleryInfo();
					CurrentImage = null;

					// Show Exception
					this.GetControlService<IPpsUIService>(true).ShowException(ex);
				}
			}
		} // func LoadGaleryImageAsync

		private Task OpenPrevImageAsync(PpsCommandContext context)
			=> CanOpenPrevImage(context) ? LoadGaleryImageAsync(galeryInfo.Prev) : Task.CompletedTask;
		
		private Task OpenNextImageAsync(PpsCommandContext context)
			=> CanOpenNextImage(context) ? LoadGaleryImageAsync(galeryInfo.Next) : Task.CompletedTask;

		private bool CanOpenNextImage(PpsCommandContext context)
			=> galeryInfo != null && galeryInfo.Next != null;

		private bool CanOpenPrevImage(PpsCommandContext context)
			=> galeryInfo != null && galeryInfo.Prev != null;

		#endregion

		#region -- Picture ------------------------------------------------------------

		private async Task<bool> OpenPictureAsync(IPpsDataInfo open)
		{
			if (currentPicture != null)
			{
				if (!await ClosePictureAsync())
					return false;
			}

			// load image
			currentPictureInfo = open;
			currentPicture = await open.LoadAsync();
			currentPicture.DisableUI = () => this.CreateProgress(progressText: "Bild wird bearbeitet...");
			currentPicture.DataChanged += CurrentImage_DataChanged;

			// image galerie
			if (currentPicture.TryGetProperty<string>("zusa-group", out var groupUrl)
				&& Uri.TryCreate(groupUrl, UriKind.RelativeOrAbsolute, out var groupUri)
				&& Shell.Http.TryMakeRelative(groupUri, out var galeryPath))
			{
				var currentId = currentPicture.GetProperty("Id", -1);

				if (galeryInfo == null || !galeryInfo.IsSameGalery(galeryPath))
					galeryInfo = await GaleryInfo.LoadAsync(Shell.Http, galeryPath);

				if (galeryInfo.Update(currentId))
					RefreshGaleryInfo();
			}
			else
			{
				galeryInfo = null;
				RefreshGaleryInfo();
			}

			// update subtitle to image
			SubTitle = currentPictureInfo.Name;

			// update pane host
			NotifyWindowPanePropertyChanged(nameof(IPpsWindowPane.CurrentData));

			// load image
			await RefreshCurrentImageAsync();

			// FitImage
			FitToImage();

			return true;
		} // func OpenPictureAsync

		private async Task OpenPictureFromSourceAsync(Uri uri)
		{
			// todo: ObjectManager
			if (Shell.Http.TryMakeRelative(uri, out var path))
			{
				using (var http = await Shell.Http.GetResponseAsync(path))
					await OpenPictureAsync(await PpsDataInfo.ToPpsDataInfoAsync(http));
			}
			else
				throw new NotSupportedException();
		} // proc OpenPictureFromSourceAsync

		private void FitToImage()
		{
			if (CurrentImage == null)
				return;

			var renderSize = scrollViewer.RenderSize;
			var imageSize = new Size(CurrentImage.Width, CurrentImage.Height);
			if (imageSize.Height < 0.1 || imageSize.Width < 0.1 || renderSize.Height < 0.1 || renderSize.Width < 0.1)
				return;

			var aspectWidth = renderSize.Width / imageSize.Width;
			var aspectHeight = renderSize.Height / imageSize.Height;
			var aspect = (aspectWidth < aspectHeight) ? aspectWidth : aspectHeight;

			scrollViewer.ScaleFactor = aspect;
			fitToImageAspect = aspect;
		} // proc FitToImage

		private async Task RefreshCurrentImageAsync()
		{
			if (currentPicture.Data is IPpsDataStream imgSrc)
			{
				// update image
				CurrentImage = await Task.Run(
					() =>
					{
						var src = imgSrc.OpenStream(FileAccess.Read);
						return BitmapFrame.Create(src, BitmapCreateOptions.None, BitmapCacheOption.Default);
					}
				);

				CanEdit = false;// !currentPicture.IsReadOnly;
			}
			else
				CanEdit = false;
		} // proc RefreshCurrentImageAsync

		private Task<bool> ClosePictureAsync(bool? commit = null)
		{
			if (currentPicture != null)
			{
				// check for changes

				// close image
				currentPicture?.Dispose();
				currentPicture = null;
			}
			currentPictureInfo = null;

			NotifyWindowPanePropertyChanged(nameof(IPpsWindowPane.CurrentData));

			return Task.FromResult(true);
		} // proc ClosePictureAsync

		/// <summary></summary>
		/// <param name="sizeInfo"></param>
		protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
		{
			base.OnRenderSizeChanged(sizeInfo);

			if (Math.Abs(fitToImageAspect - scrollViewer.ScaleFactor) <= Double.Epsilon) // is fit to image active
				FitToImage();
		} // proc OnRenderSizeChanged

		private void CurrentImage_DataChanged(object sender, EventArgs e)
			=> RefreshCurrentImageAsync().Spawn(Shell);

		#endregion

		/// <summary>Return current image object data.</summary>
		protected override IPpsDataInfo CurrentData => currentPictureInfo;
		




		//private static readonly DependencyPropertyKey currentStrokeColorPropertyKey = DependencyProperty.RegisterReadOnly(nameof(CurrentStrokeColor), typeof(PpsPecStrokeColor), typeof(PpsPicturePane), new FrameworkPropertyMetadata(null));
		///// <summary></summary>
		//public static readonly DependencyProperty CurrentStrokeColorProperty = currentStrokeColorPropertyKey.DependencyProperty;

		//private static readonly DependencyPropertyKey currentStrokeThicknessPropertyKey = DependencyProperty.RegisterReadOnly(nameof(CurrentStrokeThickness), typeof(PpsPecStrokeThickness), typeof(PpsPicturePane), new FrameworkPropertyMetadata(null));
		///// <summary></summary>
		//public static readonly DependencyProperty CurrentStrokeThicknessProperty = currentStrokeThicknessPropertyKey.DependencyProperty;

		//private IPpsWindowPane parentPane;
		//private LoggerProxy log;

		//private PpsUndoManager strokeUndoManager;
		//private List<string> captureSourceNames = new List<string>();

		#region -- Helper Classes -----------------------------------------------------

		//#region -- Data Representation ------------------------------------------------
			
		///// <summary>representation for thicknesses</summary>
		//public class PpsPecStrokeThickness
		//{
		//	private string name;
		//	private double thickness;

		//	/// <summary>Constructor</summary>
		//	/// <param name="Name">friendly name of the Thickness</param>
		//	/// <param name="Thickness">value</param>
		//	public PpsPecStrokeThickness(string Name, double Thickness)
		//	{
		//		this.name = Name;
		//		this.thickness = Thickness;
		//	}

		//	/// <summary>friendly name</summary>
		//	public string Name => name;
		//	/// <summary>thickness</summary>
		//	public double Size => thickness;
		//}

		///// <summary>representation for the color</summary>
		//public class PpsPecStrokeColor
		//{
		//	private string name;
		//	private Brush brush;

		//	/// <summary>Constructor</summary>
		//	/// <param name="Name">friendly name</param>
		//	/// <param name="ColorBrush">brush</param>
		//	public PpsPecStrokeColor(string Name, Brush ColorBrush)
		//	{
		//		this.name = Name;
		//		this.brush = ColorBrush;
		//	}

		//	/// <summary>friendly name</summary>
		//	public string Name => name;
		//	/// <summary>color of the brush</summary>
		//	public Color Color
		//	{
		//		get
		//		{
		//			if (brush is SolidColorBrush scb) return scb.Color;
		//			if (brush is LinearGradientBrush lgb) return lgb.GradientStops.FirstOrDefault().Color;
		//			if (brush is RadialGradientBrush rgb) return rgb.GradientStops.FirstOrDefault().Color;
		//			return Colors.Black;
		//		}
		//	}
		//	/// <summary>brush itself</summary>
		//	public Brush Brush => brush;
		//}

		///// <summary>handler for the settings</summary>
		//public class PpsPecStrokeSettings
		//{
		//	private IEnumerable<PpsPecStrokeColor> colors;
		//	private IEnumerable<PpsPecStrokeThickness> thicknesses;

		//	/// <summary>Constructor</summary>
		//	/// <param name="Colors"></param>
		//	/// <param name="Thicknesses"></param>
		//	public PpsPecStrokeSettings(IEnumerable<PpsPecStrokeColor> Colors, IEnumerable<PpsPecStrokeThickness> Thicknesses)
		//	{
		//		this.colors = Colors;
		//		this.thicknesses = Thicknesses;
		//	}

		//	/// <summary>list of colors</summary>
		//	public IEnumerable<PpsPecStrokeColor> Colors => colors;
		//	/// <summary>list of thicknesses</summary>
		//	public IEnumerable<PpsPecStrokeThickness> Thicknesses => thicknesses;
		//}

		//#endregion

		//#region -- UnDo/ReDo ------------------------------------------------------------

		///// <summary>This StrokeCollection owns a property if ChangedActions should be traced (ref: https://msdn.microsoft.com/en-US/library/aa972158.aspx )</summary>
		//public class PpsDetraceableStrokeCollection : StrokeCollection
		//{
		//	private bool disableTracing = false;

		//	/// <summary></summary>
		//	/// <param name="strokes"></param>
		//	public PpsDetraceableStrokeCollection(StrokeCollection strokes) : base(strokes)
		//	{
		//	}

		//	/// <summary>If true item changes should not be passed to a UndoManager</summary>
		//	public bool DisableTracing
		//	{
		//		get => disableTracing;
		//		set => disableTracing = value;
		//	}
		//}

		//private class PpsAddStrokeUndoItem : IPpsUndoItem
		//{
		//	private PpsDetraceableStrokeCollection collection;
		//	private Stroke stroke;

		//	public PpsAddStrokeUndoItem(PpsDetraceableStrokeCollection collection, Stroke strokeAdded)
		//	{
		//		this.collection = collection;
		//		this.stroke = strokeAdded;
		//	}

		//	//<summary>Unused</summary>
		//	public void Freeze()
		//	{
		//		//throw new NotImplementedException();
		//	}

		//	public void Redo()
		//	{
		//		collection.DisableTracing = true;
		//		try
		//		{
		//			collection.Add(stroke);
		//		}
		//		finally
		//		{
		//			collection.DisableTracing = false;
		//		}
		//	}

		//	public void Undo()
		//	{
		//		collection.DisableTracing = true;
		//		try
		//		{
		//			collection.Remove(stroke);
		//		}
		//		finally
		//		{
		//			collection.DisableTracing = false;
		//		}
		//	}
		//}

		//private class PpsRemoveStrokeUndoItem : IPpsUndoItem
		//{
		//	private PpsDetraceableStrokeCollection collection;
		//	private Stroke stroke;

		//	public PpsRemoveStrokeUndoItem(PpsDetraceableStrokeCollection collection, Stroke strokeAdded)
		//	{
		//		this.collection = collection;
		//		this.stroke = strokeAdded;
		//	}

		//	//<summary>Unused</summary>
		//	public void Freeze()
		//	{
		//		//throw new NotImplementedException();
		//	}

		//	public void Redo()
		//	{
		//		collection.DisableTracing = true;
		//		try
		//		{
		//			collection.Remove(stroke);
		//		}
		//		finally
		//		{
		//			collection.DisableTracing = false;
		//		}
		//	}

		//	public void Undo()
		//	{
		//		collection.DisableTracing = true;
		//		try
		//		{
		//			collection.Add(stroke);
		//		}
		//		finally
		//		{
		//			collection.DisableTracing = false;
		//		}
		//	}
		//}

		//#endregion

		#endregion

		#region -- Commands -----------------------------------------------------------

		#region ---- CommandBindings ----------------------------------------------------------

		//private void AddCommandBindings()
		//{
		//	CommandBindings.Add(
		//		new CommandBinding(
		//			EditOverlayCommand,
		//			(sender, e) =>
		//			{
		//				//if (e.Parameter is IPpsAttachmentItem i)
		//				//{
		//				//	if (i == SelectedAttachment)
		//				//		return;

		//				//	SelectedAttachment = i;

		//				//	// if the previous set failed. the user canceled the operation, so exit
		//				//	if (SelectedAttachment != i)
		//				//		return;

		//					//// request the full-sized image
		//					//var imgData = await i.LinkedObject.GetDataAsync<PpsObjectBlobData>();

		//					//var data = await SelectedAttachment.LinkedObject.GetDataAsync<PpsObjectBlobData>();
		//					//InkStrokes = new PpsDetraceableStrokeCollection(await data.GetOverlayAsync() ?? new StrokeCollection());

		//					//InkStrokes.StrokesChanged += (chgsender, chge) =>
		//					//{
		//					//	// tracing is disabled, if a undo/redo action caused the changed event, thus preventing it to appear in the undomanager itself
		//					//	if (!InkStrokes.DisableTracing)
		//					//	{
		//					//		using (var trans = strokeUndoManager.BeginTransaction("Linie hinzugefügt"))
		//					//		{
		//					//			foreach (var stroke in chge.Added)
		//					//				strokeUndoManager.Append(new PpsAddStrokeUndoItem((PpsDetraceableStrokeCollection)GetValue(InkStrokesProperty), stroke));
		//					//			trans.Commit();
		//					//		}
		//					//		using (var trans = strokeUndoManager.BeginTransaction("Linie entfernt"))
		//					//		{
		//					//			foreach (var stroke in chge.Removed)
		//					//				strokeUndoManager.Append(new PpsRemoveStrokeUndoItem((PpsDetraceableStrokeCollection)GetValue(InkStrokesProperty), stroke));
		//					//			trans.Commit();
		//					//		}
		//					//	}
		//					//};
		//					//SetCharmObject(i.LinkedObject);
		//				//}
		//				strokeUndoManager.Clear();
		//			}));

		//	CommandBindings.Add(new CommandBinding(
		//		ApplicationCommands.Save,
		//		(sender, e) =>
		//		{
		//			//if (SelectedAttachment != null)
		//			//{
		//			//	var data = await SelectedAttachment.LinkedObject.GetDataAsync<PpsObjectBlobData>();

		//			//	await data.SetOverlayAsync(InkStrokes);
		//			//	strokeUndoManager.Clear();
		//			//}

		//		},
		//		(sender, e) => e.CanExecute = strokeUndoManager.CanUndo));

		//	CommandBindings.Add(new CommandBinding(
		//		ApplicationCommands.Delete,
		//		(sender, e) =>
		//		{
		//			//if (e.Parameter is IPpsAttachmentItem pitem)
		//			//{
		//			//	pitem.Remove();
		//			//}
		//			//else if (SelectedAttachment is IPpsAttachmentItem sitem)
		//			//{
		//			//	sitem.Remove();
		//			//}
		//			//SelectedAttachment = null;
		//		},
		//		(sender, e) => e.CanExecute = true));

		//	AddCameraCommandBindings();

		//	AddStrokeCommandBindings();
		//}
		
		//private void AddStrokeCommandBindings()
		//{
		//CommandBindings.Add(new CommandBinding(
		//	OverlayEditFreehandCommand,
		//	(sender, e) =>
		//	{
		//		InkEditMode = InkCanvasEditingMode.Ink;
		//	}));

		//CommandBindings.Add(new CommandBinding(
		//	OverlayRemoveStrokeCommand,
		//	(sender, e) =>
		//	{
		//		InkEditMode = InkCanvasEditingMode.EraseByStroke;
		//	},
		//	(sender, e) => e.CanExecute = InkStrokes.Count > 0));

		//CommandBindings.Add(new CommandBinding(
		//	OverlayCancelEditModeCommand,
		//	(sender, e) =>
		//	{
		//		InkEditMode = InkCanvasEditingMode.None;
		//	},
		//	(sender, e) => e.CanExecute = true));

		//CommandBindings.Add(new CommandBinding(
		//	ApplicationCommands.Undo,
		//	(sender, e) =>
		//	{
		//		strokeUndoManager.Undo();
		//	},
		//	(sender, e) => e.CanExecute = strokeUndoManager?.CanUndo ?? false));

		//CommandBindings.Add(new CommandBinding(
		//	ApplicationCommands.Redo,
		//	(sender, e) =>
		//	{
		//		strokeUndoManager.Redo();
		//	},
		//	(sender, e) => e.CanExecute = strokeUndoManager?.CanRedo ?? false));

		//CommandBindings.Add(new CommandBinding(
		//	OverlaySetThicknessCommand,
		//	(sender, e) =>
		//	{
		//		var thickness = (PpsPecStrokeThickness)e.Parameter;
		//		InkDrawingAttributes.Width = InkDrawingAttributes.Height = (double)thickness.Size;
		//		SetValue(currentStrokeThicknessPropertyKey, thickness);
		//	},
		//	(sender, e) => e.CanExecute = true));

		//CommandBindings.Add(new CommandBinding(
		//	OverlaySetColorCommand,
		//	(sender, e) =>
		//	{
		//		var color = (PpsPecStrokeColor)e.Parameter;
		//		InkDrawingAttributes.Color = color.Color;
		//		SetValue(currentStrokeColorPropertyKey, color);
		//	},
		//	(sender, e) => e.CanExecute = true));
		//}
		
		#region UICommands

		/// <summary>sets the Mode to Edit</summary>
		public static readonly RoutedUICommand EditOverlayCommand = new RoutedUICommand("EditOverlay", "EditOverlay", typeof(PpsPicturePane));
		/// <summary>sets the Mode to Freehand drawing</summary>
		public static readonly RoutedUICommand OverlayEditFreehandCommand = new RoutedUICommand("EditFreeForm", "EditFreeForm", typeof(PpsPicturePane));
		/// <summary>sets the Mode to Delete</summary>
		public static readonly RoutedUICommand OverlayRemoveStrokeCommand = new RoutedUICommand("EditRubber", "EditRubber", typeof(PpsPicturePane));
		/// <summary>sets the Mode to None</summary>
		public static readonly RoutedUICommand OverlayCancelEditModeCommand = new RoutedUICommand("CancelEdit", "CancelEdit", typeof(PpsPicturePane));
		/// <summary>sets a given Thickness</summary>
		public static readonly RoutedUICommand OverlaySetThicknessCommand = new RoutedUICommand("SetThickness", "Set Thickness", typeof(PpsPicturePane));
		/// <summary>sets a given Color</summary>
		public static readonly RoutedUICommand OverlaySetColorCommand = new RoutedUICommand("SetColor", "Set Color", typeof(PpsPicturePane));
		/// <summary>changes (to) the given Camera</summary>
		public readonly static RoutedUICommand ChangeCameraCommand = new RoutedUICommand("ChangeCamera", "ChangeCamera", typeof(PpsPicturePane));

		#endregion

		#endregion

		#region Toolbar

		//private void RemoveToolbarCommands()
		//{
		//	Commands.Clear();
		//}

		//private void AddToolbarCommands()
		//{
		//	if (Commands.Count > 0)
		//		return;

		//	#region Misc

		//	var saveCommandButton = new PpsUICommandButton()
		//	{
		//		Order = new PpsCommandOrder(100, 110),
		//		DisplayText = "Speichern",
		//		Description = "Bild speichern",
		//		Image = "save",
		//		Command = new PpsCommand(
		//			(args) =>
		//			{
		//				ApplicationCommands.Save.Execute(args, this);
		//			},
		//			(args) => ApplicationCommands.Save.CanExecute(args, this)
		//		)
		//	};
		//	Commands.Add(saveCommandButton);

		//	#endregion

		//	#region Undo/Redo

		//	//UndoManagerListBox listBox;

		//	var undoCommand = new PpsUICommandButton()
		//	{
		//		Order = new PpsCommandOrder(200, 130),
		//		DisplayText = "Rückgängig",
		//		Description = "Rückgängig",
		//		Image = "undo",
		//		DataContext = this,
		//		Command = new PpsCommand(
		//			(args) =>
		//			{
		//				ApplicationCommands.Undo.Execute(args, this);
		//			},
		//			(args) => strokeUndoManager?.CanUndo ?? false
		//		),
		//		//Popup = new System.Windows.Controls.Primitives.Popup()
		//		//{
		//		//	Child = listBox = new UndoManagerListBox()
		//		//	{
		//		//		Style = (Style)Application.Current.FindResource("UndoManagerListBoxStyle")
		//		//	}
		//		//}
		//	};
		//	//listBox.SetBinding(FrameworkElement.DataContextProperty, new Binding("DataContext.UndoM"));

		//	var redoCommand = new PpsUICommandButton()
		//	{
		//		Order = new PpsCommandOrder(200, 140),
		//		DisplayText = "Wiederholen",
		//		Description = "Wiederholen",
		//		Image = "redo",
		//		DataContext = this,
		//		Command = new PpsCommand(
		//			(args) =>
		//			{
		//				ApplicationCommands.Redo.Execute(args, this);
		//			},
		//			(args) => strokeUndoManager?.CanRedo ?? false
		//		),
		//		//Popup = new System.Windows.Controls.Primitives.Popup()
		//		//{
		//		//	Child = listBox = new UndoManagerListBox()
		//		//	{
		//		//		Style = (Style)Application.Current.FindResource("UndoManagerListBoxStyle")
		//		//	}
		//		//}
		//	};
		//	//listBox.SetBinding(FrameworkElement.DataContextProperty, new Binding("DataContext.RedoM"));

		//	Commands.Add(undoCommand);
		//	Commands.Add(redoCommand);

		//	#endregion
		//}

		#endregion

		#endregion

		#region -- Methods ------------------------------------------------------------

		#region -- Pen Settings -------------------------------------------------------

		//private static LuaTable GetPenColorTable(PpsShell environment)
		//	=> (LuaTable)environment.GetMemberValue("pictureEditorPenColorTable");

		//private static LuaTable GetPenThicknessTable(PpsShell environment)
		//	=> (LuaTable)environment.GetMemberValue("pictureEditorPenThicknessTable");

		//private void InitializePenSettings()
		//{
		//	var StrokeThicknesses = new List<PpsPecStrokeThickness>();
		//	try
		//	{
		//		foreach (var tab in GetPenThicknessTable(paneManager.Shell)?.ArrayList)
		//		{
		//			if (tab is LuaTable lt) StrokeThicknesses.Add(new PpsPecStrokeThickness((string)lt["Name"], (double)lt["Thickness"]));
		//		}
		//	}
		//	catch (NullReferenceException)
		//	{ }

		//	var StrokeColors = new List<PpsPecStrokeColor>();
		//	try
		//	{
		//		foreach (var tab in GetPenColorTable(paneManager.Shell)?.ArrayList)
		//		{
		//			if (tab is LuaTable lt) StrokeColors.Add(new PpsPecStrokeColor((string)lt["Name"], (Brush)lt["Brush"]));
		//		}
		//	}
		//	catch (NullReferenceException)
		//	{ }

		//	if (StrokeColors.Count == 0)
		//	{
		//		log.Except("Failed to load Brushes from environment for drawing. Using Fallback.");
		//		StrokeColors = new List<PpsPecStrokeColor>
		//		{
		//			new PpsPecStrokeColor("Weiß", new SolidColorBrush( Colors.White)),
		//			new PpsPecStrokeColor("Schwarz", new SolidColorBrush( Colors.Black)),
		//			new PpsPecStrokeColor("Rot",new SolidColorBrush( Colors.Red)),
		//			new PpsPecStrokeColor("Grün",new SolidColorBrush( Colors.Green)),
		//			new PpsPecStrokeColor("Blau", new SolidColorBrush(Colors.Blue))
		//		};
		//	}


		//	if (StrokeThicknesses.Count == 0)
		//	{
		//		log.Except("Failed to load Thicknesses from environment for drawing. Using Fallback.");
		//		StrokeThicknesses = new List<PpsPecStrokeThickness>
		//		{
		//			new PpsPecStrokeThickness("1", 1),
		//			new PpsPecStrokeThickness("5", 5),
		//			new PpsPecStrokeThickness("10", 10),
		//			new PpsPecStrokeThickness("15", 15)
		//		};
		//	}

		//	StrokeSettings = new PpsPecStrokeSettings(StrokeColors, StrokeThicknesses);
		//	// start values
		//	SetValue(currentStrokeColorPropertyKey, StrokeColors[0]);
		//	SetValue(currentStrokeThicknessPropertyKey, StrokeThicknesses[0]);
		//} // proc InitializePenSettings

		#endregion
		
		#region -- Strokes ------------------------------------------------------------

		//private bool LeaveCurrentImage()
		//{
		//	//if (SelectedAttachment != null && strokeUndoManager.CanUndo)
		//	//	switch (MessageBox.Show("Sie haben ungespeicherte Änderungen!\nMöchten Sie diese vor dem Schließen noch speichern?", "Warnung", MessageBoxButton.YesNoCancel))
		//	//	{
		//	//		case MessageBoxResult.Yes:
		//	//			ApplicationCommands.Save.Execute(null, null);
		//	//			SetValue(selectedAttachmentPropertyKey, null); ;
		//	//			return true;
		//	//		case MessageBoxResult.No:
		//	//			while (strokeUndoManager.CanUndo)
		//	//				strokeUndoManager.Undo();
		//	//			SetValue(selectedAttachmentPropertyKey, null); ;
		//	//			return true;
		//	//		default:
		//	//			return false;
		//	//	}
		//	return true;
		//}

		//private void InitializeStrokes()
		//{
		//	InkStrokes = new PpsDetraceableStrokeCollection(new StrokeCollection());

		//	InkDrawingAttributes = new DrawingAttributes();
		//}

		#endregion

		//private async Task<PpsObject> IncludePictureAsync(string imagePath)
		//{
		//	PpsObject obj;

		//	using (var trans = await shell.MasterData.CreateTransactionAsync(PpsMasterDataTransactionLevel.Write))
		//	{
		//		obj = await shell.CreateNewObjectFromFileAsync(imagePath);

		//		trans.Commit();
		//	}

		//	return obj;
		//} // proc CapturePicutureAsync 

		//private void ShowOnlyObjectImageDataFilter(object sender, FilterEventArgs e)
		//{
		//	e.Accepted = false;
		//		//e.Item is IPpsAttachmentItem item
		//		//&& item.LinkedObject != null
		//		//&& item.LinkedObject.Typ == PpsEnvironment.AttachmentObjectTyp
		//		//&& item.LinkedObject.MimeType.StartsWith("image/", StringComparison.OrdinalIgnoreCase);
		//} // proc ShowOnlyObjectImageDataFilter

		#endregion

		#region -- Propertys ----------------------------------------------------------

		///// <summary>Property for the ToolBar which references the available Undo items</summary>
		//public IEnumerable<object> UndoM => (from un in strokeUndoManager where un.Type == PpsUndoStepType.Undo orderby un.Index descending select un).ToArray();
		///// <summary>Property for the ToolBar which references the available Redo items</summary>
		//public IEnumerable<object> RedoM => (from un in strokeUndoManager where un.Type == PpsUndoStepType.Redo orderby un.Index select un).ToArray();

		///// <summary>The Strokes made on the shown Image</summary>
		//public PpsDetraceableStrokeCollection InkStrokes
		//{
		//	get => (PpsDetraceableStrokeCollection)GetValue(InkStrokesProperty);
		//	private set => SetValue(inkStrokesPropertyKey, value);
		//}

		///// <summary>The state of the Editor</summary>
		//public InkCanvasEditingMode InkEditMode
		//{
		//	get => (InkCanvasEditingMode)GetValue(InkEditModeProperty);
		//	private set
		//	{
		//		SetValue(inkEditModePropertyKey, value);
		//		var t = (InkCanvas)FindChildElement(typeof(InkCanvas), this);
		//		switch ((InkCanvasEditingMode)value)
		//		{
		//			case InkCanvasEditingMode.Ink:
		//				t.MouseMove -= InkCanvasRemoveHitTest;
		//				InkEditCursor = Cursors.Pen;
		//				break;
		//			case InkCanvasEditingMode.EraseByStroke:
		//				InkEditCursor = Cursors.Cross;
		//				t.MouseMove += InkCanvasRemoveHitTest;
		//				break;
		//			case InkCanvasEditingMode.None:
		//				t.MouseMove -= InkCanvasRemoveHitTest;
		//				InkEditCursor = Cursors.Arrow;
		//				break;
		//		}
		//	}
		//} // prop InkEditMode

		///// <summary>Binding for the Cursor used by the Editor</summary>
		//public Cursor InkEditCursor { get => (Cursor)GetValue(InkEditCursorProperty); private set => SetValue(inkEditCursorPropertyKey, value); }

		///// <summary>The Binding point for Color and Thickness for the Pen</summary>
		//public DrawingAttributes InkDrawingAttributes { get => (DrawingAttributes)GetValue(InkDrawingAttributesProperty); private set => SetValue(inkDrawingAttributesPropertyKey, value); }

		///// <summary>The Binding point for Color and Thickness possibilities for the Settings Control</summary>
		//public PpsPecStrokeSettings StrokeSettings { get => (PpsPecStrokeSettings)GetValue(StrokeSettingsProperty); private set => SetValue(strokeSettingsPropertyKey, value); }

		///// <summary></summary>
		//public PpsPecStrokeColor CurrentStrokeColor => (PpsPecStrokeColor)GetValue(CurrentStrokeColorProperty);

		///// <summary></summary>
		//public PpsPecStrokeThickness CurrentStrokeThickness => (PpsPecStrokeThickness)GetValue(CurrentStrokeThicknessProperty);

		//#region DependencyPropertys

		///// <summary>Files attached to the parent object</summary>
		//public static readonly DependencyProperty AttachmentsProperty = DependencyProperty.Register(nameof(Attachments), typeof(IPpsAttachments), typeof(PpsPicturePane));

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
		//private static readonly DependencyPropertyKey selectedCameraPropertyKey = DependencyProperty.RegisterReadOnly(nameof(SelectedCamera), typeof(PpsAforgeCamera), typeof(PpsPicturePane), new FrameworkPropertyMetadata((PpsAforgeCamera)null));
		//public static readonly DependencyProperty SelectedCameraProperty = selectedCameraPropertyKey.DependencyProperty;

		//private static readonly DependencyPropertyKey lastSnapshotPropertyKey = DependencyProperty.RegisterReadOnly(nameof(LastSnapshot), typeof(PpsObject), typeof(PpsPicturePane), new FrameworkPropertyMetadata((PpsObject)null));
		//public static readonly DependencyProperty LastSnapshotProperty = lastSnapshotPropertyKey.DependencyProperty;

		//private static readonly DependencyPropertyKey selectedAttachmentPropertyKey = DependencyProperty.RegisterReadOnly(nameof(SelectedAttachment), typeof(IPpsAttachmentItem), typeof(PpsPicturePane), new FrameworkPropertyMetadata((IPpsAttachmentItem)null));
		//public static readonly DependencyProperty SelectedAttachmentProperty = selectedAttachmentPropertyKey.DependencyProperty;

		//private static readonly DependencyPropertyKey cameraEnumPropertyKey = DependencyProperty.RegisterReadOnly(nameof(CameraEnum), typeof(PpsCameraHandler), typeof(PpsPicturePane), new FrameworkPropertyMetadata((PpsCameraHandler)null));
		//public static readonly DependencyProperty CameraEnumProperty = cameraEnumPropertyKey.DependencyProperty;

		//private static readonly DependencyPropertyKey inkDrawingAttributesPropertyKey = DependencyProperty.RegisterReadOnly(nameof(InkDrawingAttributes), typeof(DrawingAttributes), typeof(PpsPicturePane), new FrameworkPropertyMetadata((DrawingAttributes)null));
		//public static readonly DependencyProperty InkDrawingAttributesProperty = inkDrawingAttributesPropertyKey.DependencyProperty;

		//private static readonly DependencyPropertyKey inkStrokesPropertyKey = DependencyProperty.RegisterReadOnly(nameof(InkStrokes), typeof(PpsDetraceableStrokeCollection), typeof(PpsPicturePane), new FrameworkPropertyMetadata((PpsDetraceableStrokeCollection)null));
		//public static readonly DependencyProperty InkStrokesProperty = inkStrokesPropertyKey.DependencyProperty;

		//private static readonly DependencyPropertyKey inkEditModePropertyKey = DependencyProperty.RegisterReadOnly(nameof(InkEditMode), typeof(InkCanvasEditingMode), typeof(PpsPicturePane), new FrameworkPropertyMetadata(InkCanvasEditingMode.None));
		//public static readonly DependencyProperty InkEditModeProperty = inkEditModePropertyKey.DependencyProperty;

		//private static readonly DependencyPropertyKey inkEditCursorPropertyKey = DependencyProperty.RegisterReadOnly(nameof(InkEditCursor), typeof(Cursor), typeof(PpsPicturePane), new FrameworkPropertyMetadata(Cursors.Arrow));
		//public static readonly DependencyProperty InkEditCursorProperty = inkEditCursorPropertyKey.DependencyProperty;

		//private static readonly DependencyPropertyKey strokeSettingsPropertyKey = DependencyProperty.RegisterReadOnly(nameof(StrokeSettings), typeof(PpsPecStrokeSettings), typeof(PpsPicturePane), new FrameworkPropertyMetadata((PpsPecStrokeSettings)null));
		//public static readonly DependencyProperty StrokeSettingsProperty = strokeSettingsPropertyKey.DependencyProperty;
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

		#endregion
	} // class PpsPicturePane

	#endregion
}

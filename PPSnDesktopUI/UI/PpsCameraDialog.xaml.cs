#region -- copyright --
//
// Licensed to the Apache Software Foundation (ASF) under one
// or more contributor license agreements.  See the NOTICE file
// distributed with this work for additional information
// regarding copyright ownership.  The ASF licenses this file
// to you under the Apache License, Version 2.0 (the
// "License"); you may not use this file except in compliance
// with the License.  You may obtain a copy of the License at
// 
//   http://www.apache.org/licenses/LICENSE-2.0
// 
// Unless required by applicable law or agreed to in writing,
// software distributed under the License is distributed on an
// "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY
// KIND, either express or implied.  See the License for the
// specific language governing permissions and limitations
// under the License.
//
#endregion
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using AForge.Video;
using AForge.Video.DirectShow;
using TecWare.DE.Data;

namespace TecWare.PPSn.UI
{
	#region -- enum PpsCameraDialogStatus ------------------------------------------------

	internal enum PpsCameraDialogStatus
	{
		/// <summary>No camera detected.</summary>
		Idle,
		/// <summary>Camera ready to shoot.</summary>
		Preview,
		/// <summary>Image taken.</summary>
		Image
	} // enum PpsCameraDialogStatus

	#endregion

	#region -- class PpsCameraDeviceProperty ---------------------------------------------

	internal sealed class PpsCameraDeviceProperty : ObservableObject
	{
		#region -- struct CameraPropertyRange -----------------------------------------

		private struct CameraPropertyRange
		{
			public CameraPropertyRange(int minValue, int maxValue, int stepSize, int defaultValue, bool canSetToAuto)
			{
				MinValue = minValue;
				MaxValue = maxValue;
				StepValue = stepSize;
				DefaultValue = defaultValue;
				CanSetToAuto = canSetToAuto;
			} // ctor

			public int MinValue { get; }
			public int MaxValue { get; }
			public int StepValue { get; }
			public int DefaultValue { get; }
			public bool CanSetToAuto { get; }
		} // struct CameraPropertyRange

		#endregion

		#region -- class CameraPropertyDescriptor -------------------------------------

		private sealed class CameraPropertyDescriptor
		{
			private readonly CameraControlProperty property;
			private readonly string name;
			private readonly string displayName;

			internal CameraPropertyDescriptor(CameraControlProperty property, string name, string displayName)
			{
				this.property = property;
				this.name = name;
				this.displayName = displayName;
			} // ctor

			public bool TryGetPropertyRange(VideoCaptureDevice device, out CameraPropertyRange range)
			{
				if (!device.GetCameraPropertyRange(property, out var minValue, out var maxValue, out var stepSize, out var defaultValue, out var flags))
				{
					range = default(CameraPropertyRange);
					return false; // failed to get range
				}
				if (minValue == maxValue)
				{
					range = default(CameraPropertyRange);
					return false; // if MinValue==MaxValue the Property is not changeable by the user - thus will not be shown
				}

				range = new CameraPropertyRange(minValue, maxValue, stepSize, defaultValue, (flags & CameraControlFlags.Auto) == CameraControlFlags.Auto);
				return true;
			} // func TryGetPropertyRange

			public bool TryGetProperty(VideoCaptureDevice device, out int value, out bool isAuto)
			{
				if (device.GetCameraProperty(property, out var curValue, out var curFlags))
				{
					value = curValue;
					isAuto = curFlags == CameraControlFlags.Auto;
					return true;
				}
				else
				{
					value = -1;
					isAuto = false;
					return false;
				}
			} // proc TryGetProperty

			public bool TrySetProperty(VideoCaptureDevice device, int value, bool isAuto)
				=> device.SetCameraProperty(property, value, isAuto ? CameraControlFlags.Auto : CameraControlFlags.Manual);

			public string Name => name;
			public string DisplayName => displayName;
		} // class CameraPropertyDescriptor

		#endregion

		private readonly CameraPropertyDescriptor propertyDescriptor;

		private readonly VideoCaptureDevice device;
		private readonly CameraPropertyRange range;

		private int currentValue = -1;
		private bool currentAuto = false;

		private PpsCameraDeviceProperty(VideoCaptureDevice device, CameraPropertyDescriptor propertyDescriptor, CameraPropertyRange range)
		{
			this.propertyDescriptor = propertyDescriptor ?? throw new ArgumentNullException(nameof(propertyDescriptor));
			this.device = device ?? throw new ArgumentNullException(nameof(device));
			this.range = range;

			Refresh();
		} // ctor

		public void Refresh()
		{
			if (propertyDescriptor.TryGetProperty(device, out var value, out var isAuto))
			{
				Set(ref currentValue, value, nameof(Value));
				if (CanSetAuto)
					Set(ref currentAuto, isAuto, nameof(IsAutomatic));
			}
			else
			{
				Set(ref currentValue, DefaultValue, nameof(Value));
				if (CanSetAuto)
					Set(ref currentAuto, false, nameof(IsAutomatic));
			}
		} // proc Refresh

		/// <summary>Name of the property for the system.</summary>
		public string Name => propertyDescriptor.Name;
		/// <summary>Name of the property for the user.</summary>
		public string DisplayName => propertyDescriptor.DisplayName;

		/// <summary>the lowest possible value</summary>
		public int MinValue => range.MinValue;
		/// <summary>the highest possible value</summary>
		public int MaxValue => range.MaxValue;
		/// <summary>the value which is suggested by the driver</summary>
		public int DefaultValue => range.DefaultValue;
		/// <summary>the distance of one possible value to the next</summary>
		public int StepValue => range.StepValue;

		/// <summary>if the property has Flags (p.e. Automatic, Manual...)</summary>
		public bool CanSetAuto => range.CanSetToAuto;

		/// <summary>actual value of the property</summary>
		public int Value
		{
			get => currentValue;
			set
			{
				if (propertyDescriptor.TrySetProperty(device, value, false))
					Refresh();
			}
		} // prop Value

		/// <summary>true if the value of the property is selected by the camera</summary>
		public bool IsAutomatic
		{
			get => currentAuto;
			set
			{
				if (!CanSetAuto)
					return;

				if (propertyDescriptor.TrySetProperty(device, value ? DefaultValue : Value, value))
					Refresh();
			}
		} // prop IsAutomatic

		// -- Static ----------------------------------------------------------

		private static readonly CameraPropertyDescriptor[] knownProperties = new CameraPropertyDescriptor[]
		{
			new CameraPropertyDescriptor(CameraControlProperty.Exposure, "Exposure", "Belichtung"),
			new CameraPropertyDescriptor(CameraControlProperty.Focus, "Focus", "Fokus"),
			new CameraPropertyDescriptor(CameraControlProperty.Iris, "Iris", "Blende"),
			new CameraPropertyDescriptor(CameraControlProperty.Pan, "Pan", "Schwenkung"),
			new CameraPropertyDescriptor(CameraControlProperty.Roll, "Rotation", "Roll"),
			new CameraPropertyDescriptor(CameraControlProperty.Tilt, "Tilt", "Neigung"),
			new CameraPropertyDescriptor(CameraControlProperty.Zoom, "Zoom", "Vergrößerung")
		};

		internal static IEnumerable<PpsCameraDeviceProperty> GetProperties(VideoCaptureDevice device)
		{
			foreach (var desc in knownProperties)
			{
				if (desc.TryGetPropertyRange(device, out var range))
					yield return new PpsCameraDeviceProperty(device, desc, range);
			}
		} // func GetProperties
	} // class PpsCameraDeviceProperty

	#endregion

	#region -- class PpsCameraDevice -----------------------------------------------------

	internal sealed class PpsCameraDevice : INotifyPropertyChanged, IDisposable
	{
		public event PropertyChangedEventHandler PropertyChanged;

		private readonly IPpsLogger log;
		private readonly string deviceName;
		private readonly VideoCaptureDevice device;
		private readonly PpsCameraDeviceProperty[] properties;
		private bool isDisposed = false;

		private Size? currentPreviewSize = new Size(80, 80);
		private bool snapShotsAttached = false;
		private bool videoAttached = false;
		private bool isCameraLost = false;

		private ImageSource currentPreviewImage;
		private int lastPreviewFrameTick;

		private readonly object lockFrameEventsLock = new object();
		private bool canUpdatePreviewImage = false;
		private TaskCompletionSource<BitmapSource> waitFrameTask;

		#region -- Ctor/Dtor ----------------------------------------------------------

		private PpsCameraDevice(IPpsLogger log, string deviceName, VideoCaptureDevice device)
		{
			this.log = log ?? throw new ArgumentNullException(nameof(log));
			this.deviceName = deviceName ?? "Unknown";
			this.device = device ?? throw new ArgumentNullException(nameof(device));

			properties = PpsCameraDeviceProperty.GetProperties(device).ToArray();

			foreach (var p in properties)
			{
				if (p.CanSetAuto)
					p.PropertyChanged += Property_PropertyChanged;
			}

			device.VideoSourceError += Device_VideoSourceError;
			device.PlayingFinished += Device_PlayingFinished;
		} // ctor

		private void Device_PlayingFinished(object sender, ReasonToFinishPlaying reason)
		{
			switch (reason)
			{
				case ReasonToFinishPlaying.VideoSourceError:
					IsCameraLost = true;
					break; // should invoke Device_VideoSourceError

				case ReasonToFinishPlaying.StoppedByUser:
					break;

				//case ReasonToFinishPlaying.DeviceLost:
				//case ReasonToFinishPlaying.EndOfStreamReached:
				default:
					if (!isDisposed)
					{
						LogMsg(PpsLogType.Warning, reason.ToString());
						IsCameraLost = true;
					}
					break;
			}
		} // event Device_PlayingFinished

		private void Device_VideoSourceError(object sender, VideoSourceErrorEventArgs eventArgs)
		{
			if (!isDisposed)
				LogMsg(PpsLogType.Fail, eventArgs.Description ?? "Video source error");
		} // event Device_VideoSourceError

		private void Property_PropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			if (e.PropertyName == nameof(PpsCameraDeviceProperty.IsAutomatic))
				PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsCameraInAutomaticMode)));
		} // event Property_PropertyChanged

		public async Task InitAsync(Size previewSize, int previewFpsMinimum = 15, bool forTakePicture = false)
		{
			currentPreviewSize = forTakePicture && snapShotsAttached ? (Size?)previewSize : null;

			InitSnapshots();
			if (await InitVideoAsync(previewSize, previewFpsMinimum))
			{
				lock (lockFrameEventsLock)
					canUpdatePreviewImage = true;
			}
		} // proc Init

		~PpsCameraDevice()
		{
			Dispose(false);
		} // ctor

		public void Dispose()
		{
			if (isDisposed)
				throw new ObjectDisposedException(nameof(PpsCameraDevice));

			try
			{
				GC.SuppressFinalize(this);
				Dispose(true);
			}
			finally
			{
				isDisposed = true;
			}
		} // proc Dispose

		private void Dispose(bool disposing)
		{
			if (disposing)
			{
				DoneSnapshots();
				DoneVideoAsync().ContinueWith(t => Debug.Print(t.Exception.ToString()), TaskContinuationOptions.OnlyOnFaulted);

				device.PlayingFinished -= Device_PlayingFinished;
				device.VideoSourceError -= Device_VideoSourceError;
			}
		} // proc Dispose

		#endregion

		#region -- Helper -------------------------------------------------------------

		private void LogMsg(PpsLogType type, string message)
			=> log.Append(type, String.Format("Camera[{0}]: {1}", deviceName, message));

		private static IEnumerable<VideoCapabilities> FindMaxResolution(IEnumerable<VideoCapabilities> capabilities)
			=> capabilities.OrderByDescending(v => v.FrameSize.Width * v.FrameSize.Height);

		private static IEnumerable<VideoCapabilities> FindNearestResolution(IEnumerable<VideoCapabilities> capabilities, Size size)
		{
			var targetSize = (int)(size.Width * size.Height);
			return capabilities.OrderBy(v => Math.Abs((v.FrameSize.Width * v.FrameSize.Height) - targetSize));
		} // func FindNearestResolution

		private BitmapPalette ConvertPalette(System.Drawing.Imaging.ColorPalette palette, int maxColors)
			=> new BitmapPalette(palette.Entries.Take(maxColors).Select(c => Color.FromArgb(c.A, c.R, c.G, c.B)).ToList());

		private bool TryGetFormat(System.Drawing.Bitmap bitmap, out PixelFormat format, out BitmapPalette palette)
		{
			switch (bitmap.PixelFormat)
			{
				case System.Drawing.Imaging.PixelFormat.Format1bppIndexed:
					format = PixelFormats.Indexed1;
					palette = ConvertPalette(bitmap.Palette, 2);
					return true;
				case System.Drawing.Imaging.PixelFormat.Format4bppIndexed:
					format = PixelFormats.Indexed4;
					palette = ConvertPalette(bitmap.Palette, 16);
					return true;
				case System.Drawing.Imaging.PixelFormat.Format8bppIndexed:
					format = PixelFormats.Indexed8;
					palette = ConvertPalette(bitmap.Palette, 256);
					return true;

				case System.Drawing.Imaging.PixelFormat.Format24bppRgb:
					format = PixelFormats.Bgr24;
					palette = null;
					return true;
				case System.Drawing.Imaging.PixelFormat.Format32bppRgb:
					format = PixelFormats.Bgr32;
					palette = null;
					return true;
				case System.Drawing.Imaging.PixelFormat.Format32bppArgb:
					format = PixelFormats.Bgra32;
					palette = null;
					return true;

				default:
					format = PixelFormats.Default;
					palette = null;
					return false;
			}
		} // func TryGetFormat

		private BitmapSource GetBitmapFrame(System.Drawing.Bitmap bitmap)
		{
			//Debug.Print(String.Format("Frame: {0}, {1}", bitmap.Width, bitmap.Width));
			var rc = new System.Drawing.Rectangle(0, 0, bitmap.Width, bitmap.Height);
			var bitmapData = bitmap.LockBits(rc, System.Drawing.Imaging.ImageLockMode.ReadOnly, bitmap.PixelFormat);
			try
			{
				if (!TryGetFormat(bitmap, out var format, out var palette))
					throw new ArgumentException("Invalid bitmap format.");

				var bmp = BitmapSource.Create(
					rc.Width,
					rc.Height,
					bitmap.HorizontalResolution,
					bitmap.VerticalResolution,
					format,
					palette,
					bitmapData.Scan0,
					bitmapData.Stride * bitmapData.Height,
					bitmapData.Stride
				);
				bmp.Freeze();
				return bmp;
			}
			finally
			{
				bitmap.UnlockBits(bitmapData);
			}
		} // func GetBitmapFrame

		#endregion

		#region -- Snapshots ----------------------------------------------------------

		private bool InitSnapshots()
		{
			// find the highest snapshot resolution, will stop the video capturing
			var snapshotCapability = FindMaxResolution(device.SnapshotCapabilities).FirstOrDefault();

			// there are cameras without snapshot capability
			if (snapshotCapability != null)
			{
				device.ProvideSnapshots = true;
				device.SnapshotResolution = snapshotCapability;

				// attach the event handler for snapshots
				if (!snapShotsAttached)
				{
					device.SnapshotFrame += Device_SnapshotFrame;
					snapShotsAttached = true;
					LogMsg(PpsLogType.Information, String.Format("Snapshot initialized with {0},{1}", snapshotCapability.FrameSize.Width, snapshotCapability.FrameSize.Height));
				}
				return true;
			}
			else
				return false;
		} // func InitSnapshots

		private void DoneSnapshots()
		{
			if (!snapShotsAttached)
				return;

			log.Append(PpsLogType.Information, "Close snapshot mode.");

			snapShotsAttached = false;
			device.ProvideSnapshots = false;
			if (snapShotsAttached)
				device.SnapshotFrame -= Device_SnapshotFrame;
		} // proc DoneSnapshots

		private void Device_SnapshotFrame(object sender, NewFrameEventArgs eventArgs)
		{
			try
			{
				TrySetCaptureFrame(eventArgs);
			}
			finally
			{
				eventArgs.Frame.Dispose();
			}
		} // event Device_SnapshotFrame

		#endregion

		#region -- Video --------------------------------------------------------------

		private IEnumerable<VideoCapabilities> SelectVideoCapability(Size? size, int fpsMinimum)
		{
			var videoCapabilities = (IEnumerable<VideoCapabilities>)device.VideoCapabilities;

			if (fpsMinimum > 0)
				videoCapabilities = videoCapabilities.Where(v => v.AverageFrameRate >= fpsMinimum);

			if (size.HasValue)
				videoCapabilities = FindNearestResolution(videoCapabilities, size.Value);
			else
				videoCapabilities = FindMaxResolution(videoCapabilities);

			return videoCapabilities;
		} // func SelectVideoCapability

		private async Task<bool> InitVideoAsync(Size? size, int fpsMinimum)
		{
			await DoneVideoAsync();

			var videoCapability = SelectVideoCapability(size, fpsMinimum).FirstOrDefault();
			if (videoCapability != null)
			{
				lastPreviewFrameTick = Environment.TickCount;
				device.VideoResolution = videoCapability;
				if (!videoAttached)
				{
					videoAttached = true;
					device.NewFrame += Device_NewFrame;
					device.Start();
				}

				LogMsg(PpsLogType.Information, String.Format("Video initialized with {0},{1}", videoCapability.FrameSize.Width, videoCapability.FrameSize.Height));

				return true;
			}
			else
				return false;
		} // func InitVideo

		private Task DoneVideoAsync()
		{
			if (!videoAttached)
				return Task.CompletedTask;

			lock (lockFrameEventsLock)
			{
				canUpdatePreviewImage = false;
				videoAttached = false;
				device.NewFrame -= Device_NewFrame;
			}

			log.Append(PpsLogType.Information, "Stop video mode.");

			return Task.Run(() =>
			{
				device.Stop();
				device.WaitForStop();
			});
		} // proc DoneVideo

		private bool inRenderFrame = false;

		private async Task UpdatePreviewImageAsync(System.Drawing.Bitmap frame)
		{
			if (inRenderFrame)
				frame.Dispose();// drop frame
			else
			{
				inRenderFrame = true;
				try
				{
					try
					{
						currentPreviewImage = GetBitmapFrame(frame);
					}
					finally
					{
						frame.Dispose();
					}

					PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(PreviewImage)));
					await Task.Delay(100);
				}
				finally
				{
					inRenderFrame = false;
				}
			}
		} // proc UpdatePreviewImage

		private void Device_NewFrame(object sender, NewFrameEventArgs eventArgs)
		{
			lastPreviewFrameTick = Environment.TickCount;

			if (canUpdatePreviewImage)
			{
				UpdatePreviewImageAsync(eventArgs.Frame)
					.ContinueWith(t => log.Append(PpsLogType.Exception, t.Exception),TaskContinuationOptions.OnlyOnFaulted);
			}
			else
			{
				try
				{
					TrySetCaptureFrame(eventArgs);
				}
				finally
				{
					eventArgs.Frame.Dispose();
				}
			}
		} // event Device_NewFrame

		#endregion

		private void TrySetCaptureFrame(NewFrameEventArgs eventArgs)
		{
			lock (lockFrameEventsLock)
			{
				if (waitFrameTask != null && !waitFrameTask.Task.IsCompleted)
					waitFrameTask.SetResult(GetBitmapFrame(eventArgs.Frame));
			}
		} // proc TrySetCaptureFrame

		private async Task<BitmapSource> GetOneFrameAsync()
		{
			using (var cancellationTokenSource = new CancellationTokenSource(3000)) // timeout for picture frame
			{
				cancellationTokenSource.Token.Register(() =>
				{
					lock (lockFrameEventsLock)
						waitFrameTask.TrySetCanceled();
				});
				return await waitFrameTask.Task;
			}
		} // func GetOneFrameAsync

		/// <summary>Crap a picture from the camera device.</summary>
		/// <returns></returns>
		public async Task<BitmapSource> TakePictureAsync()
		{
			if (isCameraLost)
				throw new InvalidOperationException("Camera is lost.");
			else if (snapShotsAttached) // snap shot mode, start trigger
			{
				lock (lockFrameEventsLock)
					waitFrameTask = new TaskCompletionSource<BitmapSource>();
				try
				{
					await Task.Run(new Action(device.SimulateTrigger));
					return await GetOneFrameAsync();
				}
				finally
				{
					lock (lockFrameEventsLock)
						waitFrameTask = null;
				}
			}
			else if (videoAttached)
			{
				// suspend preview
				lock (lockFrameEventsLock)
					canUpdatePreviewImage = false;

				lock (lockFrameEventsLock)
					waitFrameTask = new TaskCompletionSource<BitmapSource>();
				try
				{
					// change to highest resolution
					var result = await GetOneFrameAsync();

					// restore preview
					if (await InitVideoAsync(currentPreviewSize, 15))
					{
						lock (lockFrameEventsLock)
							canUpdatePreviewImage = true;
					}

					return result;
				}
				finally
				{
					lock (lockFrameEventsLock)
						waitFrameTask = null;
				}
			}
			else
				throw new InvalidOperationException("Camera has no video stream.");
		} // func TakePictureAsync

		/// <summary>Device id of the camera</summary>
		public string Moniker => device.Source;
		/// <summary>Name of the device</summary>
		public string Name => deviceName;
		/// <summary>Are properties present.</summary>
		public bool HasProperties => properties.Length > 0;
		/// <summary>list of propertys which the camera supports</summary>
		public PpsCameraDeviceProperty[] Properties => properties;
		/// <summary>true if the camera chooses the optimal settings</summary>
		public bool IsCameraInAutomaticMode
		{
			get => Array.TrueForAll(properties, p => !p.CanSetAuto || p.IsAutomatic);
			set
			{
				var isChanged = false;
				Array.ForEach(properties, p =>
				{
					if (p.CanSetAuto)
					{
						if (p.IsAutomatic != value)
						{
							p.IsAutomatic = value;
							isChanged = true;
						}
					}
				});

				if (isChanged)
					PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsCameraInAutomaticMode)));
			}
		} // prop IsCameraInAutomaticMode

		/// <summary>Is camera disposed</summary>
		public bool IsDisposed => isDisposed;
		/// <summary>Is camera lost.</summary>
		public bool IsCameraLost
		{
			get => isCameraLost;
			private set
			{
				if (isCameraLost != value)
				{
					isCameraLost = value;
					PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsCameraLost)));
				}
			}
		} // prop IsCameraLost

		/// <summary>Time out for frames.</summary>
		public bool IsTimeout => unchecked(Environment.TickCount - lastPreviewFrameTick) > 5000;

		/// <summary>bitmaps of the preview stream</summary>
		public ImageSource PreviewImage => currentPreviewImage;

		public static Task<PpsCameraDevice> TryCreateAsync(IPpsLogger log, FilterInfo deviceFilter, Size previewSize)
		{
			PpsCameraDevice device = null;
			try
			{
				device = new PpsCameraDevice(log, deviceFilter.Name, new VideoCaptureDevice(deviceFilter.MonikerString));
				return device.InitAsync(previewSize).ContinueWith(t => device, TaskContinuationOptions.OnlyOnRanToCompletion);
			}
			catch (Exception e)
			{
				log.Append(PpsLogType.Exception, e, String.Format("Device initialization failed for {0}", deviceFilter.Name));

				try { device?.Dispose(); }
				catch { }

				return Task.FromResult<PpsCameraDevice>(null);
			}
		} // func TryCreateAsync
	} // class PpsCameraDevice

	#endregion

	#region -- class PpsCameraDialog -----------------------------------------------------

	internal partial class PpsCameraDialog : Window
	{
		#region -- CurrentDevice - property -------------------------------------------

		private static readonly DependencyPropertyKey currentDevicePropertyKey = DependencyProperty.RegisterReadOnly(nameof(CurrentDevice), typeof(PpsCameraDevice), typeof(PpsCameraDialog), new FrameworkPropertyMetadata(null, new PropertyChangedCallback(OnCurrentDeviceChanged)));
		public static readonly DependencyProperty CurrentDeviceProperty = currentDevicePropertyKey.DependencyProperty;

		private static void OnCurrentDeviceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
			=> ((PpsCameraDialog)d).OnCurrentDeviceChanged((PpsCameraDevice)e.NewValue, (PpsCameraDevice)e.OldValue);

		private void OnCurrentDeviceChanged(PpsCameraDevice newValue, PpsCameraDevice oldValue)
		{
			if (oldValue != null)
				oldValue.InitAsync(initialPreviewSize).SpawnTask(Environment);

			if (newValue != null)
				newValue.InitAsync(fullPreviewSize, forTakePicture: true).SpawnTask(Environment);
		} // proc OnCurrentDeviceChanged

		public PpsCameraDevice CurrentDevice => (PpsCameraDevice)GetValue(CurrentDeviceProperty);

		#endregion

		#region -- CurrentImage - property --------------------------------------------

		private static readonly DependencyPropertyKey currentImagePropertyKey = DependencyProperty.RegisterReadOnly(nameof(CurrentImage), typeof(ImageSource), typeof(PpsCameraDialog), new FrameworkPropertyMetadata(null));
		public static readonly DependencyProperty CurrentImageProperty = currentImagePropertyKey.DependencyProperty;

		public ImageSource CurrentImage => (ImageSource)GetValue(CurrentImageProperty);

		#endregion

		#region -- HasDeviceSelection - property --------------------------------------

		private static readonly DependencyPropertyKey hasDevicePropertyKey = DependencyProperty.RegisterReadOnly(nameof(HasDeviceSelection), typeof(bool), typeof(PpsCameraDialog), new FrameworkPropertyMetadata(BooleanBox.False));
		public static readonly DependencyProperty HasDeviceSelectionProperty = hasDevicePropertyKey.DependencyProperty;

		public bool HasDeviceSelection => BooleanBox.GetBool(GetValue(HasDeviceSelectionProperty));

		#endregion

		#region -- IsSettingsActive - property ----------------------------------------

		private static readonly DependencyPropertyKey isSettingsActivePropertyKey = DependencyProperty.RegisterReadOnly(nameof(IsSettingsActive), typeof(bool), typeof(PpsCameraDialog), new FrameworkPropertyMetadata(BooleanBox.False));
		public static readonly DependencyProperty IsSettingsActiveProperty = isSettingsActivePropertyKey.DependencyProperty;

		public bool IsSettingsActive => BooleanBox.GetBool(GetValue(IsSettingsActiveProperty));

		#endregion

		#region -- CurrentStatus - property -------------------------------------------

		private static readonly DependencyPropertyKey currentStatusPropertyKey = DependencyProperty.RegisterReadOnly(nameof(CurrentStatus), typeof(PpsCameraDialogStatus), typeof(PpsCameraDialog), new FrameworkPropertyMetadata(PpsCameraDialogStatus.Idle));
		public static readonly DependencyProperty CurrentStatusProperty = currentStatusPropertyKey.DependencyProperty;

		public PpsCameraDialogStatus CurrentStatus => (PpsCameraDialogStatus)GetValue(CurrentStatusProperty);

		#endregion

		/// <summary>Template name for the settings box</summary>
		private const string settingsBoxTemplateName = "PART_SettingsBox";

		private readonly PpsShellWpf environment;
		private readonly DispatcherTimer refreshCameraDevices;
		private readonly List<PpsCameraDevice> devices = new List<PpsCameraDevice>(); // list of current camera devices
		private readonly ICollectionView devicesView;
		private readonly Size initialPreviewSize = new Size(80, 80);
		private readonly Size fullPreviewSize = new Size(1024, 1024);

		public PpsCameraDialog(PpsShellWpf environment)
		{
			this.environment = environment ?? throw new ArgumentNullException(nameof(environment));

			InitializeComponent();

			devicesView = CollectionViewSource.GetDefaultView(devices);
			devicesView.CurrentChanged += DevicesView_CurrentChanged;
			devicesView.CollectionChanged += DevicesView_CollectionChanged;

			refreshCameraDevices = new DispatcherTimer(TimeSpan.FromMilliseconds(1000), DispatcherPriority.Send, RefreshDevicesTick, Dispatcher) { IsEnabled = true };

			this.AddCommandBinding(environment, ApplicationCommands.New,
				new PpsAsyncCommand(TakePictureImpl, CanTakePicture)
			);
			this.AddCommandBinding(environment, ApplicationCommands.Close,
				new PpsCommand(ctx =>
					{
						SetValue(currentImagePropertyKey, null);
						DialogResult = false;
					}
				)
			);
			this.AddCommandBinding(environment, ApplicationCommands.Redo,
				new PpsCommand(
					ctx =>
					{
						// clear image and status
						SetValue(currentImagePropertyKey, null);
						SetValue(currentStatusPropertyKey, PpsCameraDialogStatus.Preview);
					},
					CanTakePicture)
			);
			this.AddCommandBinding(environment, ApplicationCommands.Save,
				new PpsCommand(ctx =>
					{
						DialogResult = true;
					},
					ctx => CurrentImage != null
				)
			);
			this.AddCommandBinding(environment, ApplicationCommands.Properties,
				new PpsCommand(ctx =>
					{
						SetValue(isSettingsActivePropertyKey, !IsSettingsActive);
					},
					ctx => CurrentStatus == PpsCameraDialogStatus.Preview
				)
			);
			DataContext = this;
		} // ctor

		// WIP:
		private void DevicesView_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
		{
			if (CurrentDevice == null && !devicesView.IsEmpty)
				devicesView.MoveCurrentToFirst();

			SetValue(hasDevicePropertyKey, devices.Count > 1);

			if (CurrentStatus == PpsCameraDialogStatus.Idle && !devicesView.IsEmpty)
				SetValue(currentStatusPropertyKey, PpsCameraDialogStatus.Preview);

			CommandManager.InvalidateRequerySuggested();
		} // proc DevicesView_CollectionChanged

		#region -- Simulate Popup.StaysOpen = false -----------------------------------

		// simulate Popup.StaysOpen = false
		protected override void OnPreviewMouseDown(MouseButtonEventArgs e)
		{
			if (IsSettingsActive && e.OriginalSource is DependencyObject d && d.GetVisualParent(settingsBoxTemplateName) == null)
				SetValue(isSettingsActivePropertyKey, false);
			base.OnPreviewMouseDown(e);
		} // proc OnPreviewMouseDown

		#endregion

		protected override void OnClosed(EventArgs e)
		{
			refreshCameraDevices.IsEnabled = false;

			// dispose all devices
			foreach (var d in devices)
				d.Dispose();
			devices.Clear();

			base.OnClosed(e);
		} // proc OnClsoed

		private void DevicesView_CurrentChanged(object sender, EventArgs e)
			=> SetValue(currentDevicePropertyKey, devicesView.CurrentItem);

		#region  -- Refresh camera's --------------------------------------------------

		private void RefreshDevicesTick(object sender, EventArgs e)
		{
			refreshCameraDevices.IsEnabled = false;
			Task.Run(new Action(RefreshCameraDevices))
				.ContinueWith(t =>
				{
					try
					{
						t.Wait();
					}
					catch (Exception ex)
					{
						Environment.ShowException(ex);
					}
					finally
					{
						refreshCameraDevices.IsEnabled = true;
					}
				}, TaskContinuationOptions.ExecuteSynchronously
			);
		} // proc RefreshDevicesTick

		private void RefreshCameraDevices()
		{
			var deviceFilterCollection = new FilterInfoCollection(FilterCategory.VideoInputDevice);

			// check for new camera
			var newCameras = new List<PpsCameraDevice>();
			foreach (var deviceFilter in deviceFilterCollection.OfType<FilterInfo>())
			{
				// only usb and pnp:display (directly on cpu/bus)
				if (!deviceFilter.MonikerString.Contains("vid") && !deviceFilter.MonikerString.Contains("pnp:\\\\?\\display"))
					continue;

				try
				{
					if (!devices.Exists(d => d.Moniker == deviceFilter.MonikerString))
					{
						var camera = PpsCameraDevice.TryCreateAsync(environment.Log, deviceFilter, initialPreviewSize).Result;
						if (camera != null)
							newCameras.Add(camera);
					}
				}
				catch (Exception e)
				{
					environment.Log.Append(PpsLogType.Exception, e);
				}
			}

			// check for lost camera's
			var devicesToRemove = devices.Where(d => d.IsCameraLost || d.IsTimeout || d.IsDisposed).ToArray();
			if (devicesToRemove.Length > 0 || newCameras.Count > 0)
			{
				Dispatcher.Invoke(new Action<PpsCameraDevice[], PpsCameraDevice[]>(UpdateDevicesUI),
				  devicesToRemove,
				  newCameras.ToArray()
			  );
			}
		} // proc RefreshCameraDevices

		private void UpdateDevicesUI(PpsCameraDevice[] devicesToRemove, PpsCameraDevice[] devicesToAdd)
		{
			// remove devices
			foreach (var rd in devicesToRemove)
			{
				if (devices.Remove(rd))
				{
					if (!rd.IsDisposed)
						rd.Dispose();
				}
			}

			// append new devices
			foreach (var nd in devicesToAdd)
				devices.Add(nd);

			devicesView.Refresh();
		} // proc UpdateDevices

		#endregion

		private async Task TakePictureImpl(PpsCommandContext ctx)
		{
			try
			{
				var img = await CurrentDevice?.TakePictureAsync();
				if (img != null)
				{
					SetValue(currentImagePropertyKey, img);
					SetValue(currentStatusPropertyKey, PpsCameraDialogStatus.Image);
				}
			}
			catch (Exception ex)
			{
				Environment.ShowException(ex);
			}
		} // func TakePictureImpl

		private bool CanTakePicture(PpsCommandContext ctx)
			=> CurrentDevice != null && !CurrentDevice.IsCameraLost && !CurrentDevice.IsDisposed;

		public ICollectionView Devices => devicesView;
		private PpsShellWpf Environment => environment;

		// -- static  ---------------------------------------------------------

		public static ImageSource TakePicture(DependencyObject owner)
		{
			var window = new PpsCameraDialog(PpsShellWpf.GetShell(owner));
			window.SetFullscreen(owner);
			return owner.ShowModalDialog(window) == true
				? window.CurrentImage
				: null;
		} // func TakePicture
	} // class PpsCameraDialog

	#endregion
}

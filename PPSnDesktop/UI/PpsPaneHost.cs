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
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Neo.IronLua;
using TecWare.PPSn.Controls;

namespace TecWare.PPSn.UI
{
	/// <summary>Base class for window pane hosts.</summary>
	[TemplatePart(Name = "PART_Progress", Type = typeof(PpsProgressOverlay))]
	internal abstract class PpsPaneHost : Control, IPpsWindowPaneHost
	{
		/// <summary>Notify that the pane is unload.</summary>
		public event EventHandler<PaneUnloadedEventArgs> PaneUnloaded;

		private readonly PpsProgressStack progressStack;
		private IPpsWindowPaneManager paneManager;
		
		#region -- Ctor/Dtor ----------------------------------------------------------

		protected PpsPaneHost()
		{
			// initialize progress
			progressStack = Dispatcher.CreateProgressStack();
			SetValue(titlePropertyKey, "Wird geladen...");
		} // ctor

		public override void OnApplyTemplate()
		{
			base.OnApplyTemplate();
			if (GetTemplateChild("PART_Progress") is PpsProgressOverlay progressOverlay)
				progressOverlay.ProgressStack = progressStack;
		} // proc OnApplyTemplate

		#endregion

		#region -- Render -------------------------------------------------------------

		/// <summary>Render a pane image</summary>
		/// <param name="pixelWidth"></param>
		/// <param name="pixelHeight"></param>
		/// <param name="dpiX"></param>
		/// <param name="dpiY"></param>
		/// <returns></returns>
		public ImageSource Render(int pixelWidth, int pixelHeight, double dpiX = 96.0, double dpiY = 96.0)
		{
			var ctrlSize = new Size(Math.Min(ActualWidth, 3000), Math.Min(ActualHeight, 3000));
			if (ctrlSize.Width < 10.0 || ctrlSize.Height < 10.0)
				return null;

			var targetBmp = new RenderTargetBitmap((int)ctrlSize.Width, (int)ctrlSize.Height, dpiX, dpiY, PixelFormats.Pbgra32);
			targetBmp.Render(this);

			var aspectX = pixelWidth / ctrlSize.Width;
			var aspectY = pixelHeight / ctrlSize.Height;
			var aspect = aspectX < aspectY ? aspectX : aspectY;

			var transformed = new TransformedBitmap(targetBmp, new ScaleTransform(aspect, aspect));
			transformed.Freeze();

			return transformed;
		} // func Render

		#endregion

		#region -- Load/Unload pane ---------------------------------------------------

		private IPpsWindowPane CreatePaneBindings(IPpsWindowPane pane)
		{
			pane.PropertyChanged += Pane_PropertyChanged;

			// initialize properties
			SetValue(titlePropertyKey, pane.Title);
			SetValue(subTitlePropertyKey, pane.SubTitle);
			SetValue(imagePropertyKey, pane.Image);
			SetValue(hasPaneSideBarPropertyKey, pane.HasSideBar);
			SetValue(commandsPropertyKey, pane.Commands);

			SetControl(pane.Control);

			SetValue(panePropertyKey, pane);

			return pane;
		} // proc CreatePaneBindings

		private void DestroyPaneBindings(IPpsWindowPane pane)
		{
			pane.PropertyChanged -= Pane_PropertyChanged;

			SetValue(titlePropertyKey, "Entladen");
			SetValue(subTitlePropertyKey, null);
			SetValue(imagePropertyKey, null);
			SetValue(hasPaneSideBarPropertyKey, BooleanBox.False);
			SetValue(commandsPropertyKey, null);

			SetControl(null);

			SetValue(panePropertyKey, null);
		} // proc DestroyPaneBindings

		/// <summary>Load the pane</summary>
		/// <param name="pane"></param>
		/// <param name="arguments"></param>
		/// <returns></returns>
		protected virtual Task OnLoadPaneAsync(IPpsWindowPane pane, LuaTable arguments)
			=> pane.LoadAsync(arguments);

		/// <summary>Load the pane content</summary>
		/// <param name="paneManager"></param>
		/// <param name="paneType"></param>
		/// <param name="arguments"></param>
		/// <returns></returns>
		public async Task<IPpsWindowPane> LoadAsync(IPpsWindowPaneManager paneManager, Type paneType, LuaTable arguments)
		{
			this.paneManager = paneManager ?? throw new ArgumentNullException(nameof(paneManager));

			using (var p = this.CreateProgress(progressText: "Lade..."))
			{
				// create the pane
				var pane = CreatePaneBindings(PpsWindowPaneHelper.CreateEmptyPane(paneManager, this, paneType));

				// load content
				await OnLoadPaneAsync(pane, arguments);

				return pane;
			}
		} // proc LoadAsync

		protected virtual Task<bool> OnUnloadPaneAsync(IPpsWindowPane pane, bool? commit)
			=> pane.UnloadAsync(commit);

		/// <summary>Unload pane content.</summary>
		/// <param name="commit"></param>
		/// <returns></returns>
		public  async Task<bool> UnloadAsync(bool? commit = null)
		{
			var pane = Pane;
			var r = await OnUnloadPaneAsync(pane, commit);
			if (r)
			{
				PaneUnloaded?.Invoke(this, new PaneUnloadedEventArgs(pane));

				DestroyPaneBindings(pane);

				if (pane is IDisposable d)
					d.Dispose();
			}

			return r;
		} // func UnloadPaneAsync

		/// <summary>Close this pane.</summary>
		/// <returns></returns>
		public abstract Task<bool> ClosePaneAsync();

		public void MoveWindowPane(IPpsWindowPaneManager targetPaneManager, IPpsWindowPaneHost targetPaneHost)
		{
			var pane = Pane;
			if (pane == null)
				return;

			if (targetPaneHost is PpsPaneHost targetHost)
			{
				// remove window from current host
				DestroyPaneBindings(pane);

				// copy window pane to new host
				targetHost.paneManager = targetPaneManager;
				targetHost.CreatePaneBindings(pane);
			}
		} // proc MoveWindowPane

		protected virtual void OnPanePropertyChanged(string propertyName)
		{
			switch (propertyName)
			{
				case nameof(IPpsWindowPane.Title):
					SetValue(titlePropertyKey, Pane.Title);
					break;
				case nameof(IPpsWindowPane.SubTitle):
					SetValue(subTitlePropertyKey, Pane.SubTitle);
					break;
				case nameof(IPpsWindowPane.Image):
					SetValue(imagePropertyKey, Pane.Image);
					break;
				case nameof(IPpsWindowPane.HasSideBar):
					SetValue(hasPaneSideBarPropertyKey, Pane.HasSideBar);
					break;
				case nameof(IPpsWindowPane.Commands):
					SetValue(commandsPropertyKey, Pane.Commands);
					break;
				case nameof(IPpsWindowPane.HelpKey):
					SetValue(helpKeyPropertyKey, Pane.HelpKey);
					break;
				case nameof(IPpsWindowPane.IsDirty):
					SetValue(isDirtyPropertyKey, Pane.IsDirty);
					break;

				case nameof(IPpsWindowPane.Control):
					SetControl(Pane.Control);
					break;
			}
		} // proc OnPanePropertyChanged

		object IServiceProvider.GetService(Type serviceType)
		{
			if (serviceType == typeof(IPpsProgressFactory))
				return progressStack;
			else
				return paneManager.GetService(serviceType);
		} // func IServiceProvider.GetService

		private void Pane_PropertyChanged(object sender, PropertyChangedEventArgs e)
			=> OnPanePropertyChanged(e.PropertyName);

		#endregion

		#region -- Title - property ---------------------------------------------------

		private static readonly DependencyPropertyKey titlePropertyKey = DependencyProperty.RegisterReadOnly(nameof(Title), typeof(string), typeof(PpsPaneHost), new FrameworkPropertyMetadata(null));
		public static readonly DependencyProperty TitleProperty = titlePropertyKey.DependencyProperty;

		/// <summary>Current title of the pane.</summary>
		public string Title => (string)GetValue(TitleProperty);

		#endregion

		#region -- SubTitle - property ------------------------------------------------

		private static readonly DependencyPropertyKey subTitlePropertyKey = DependencyProperty.RegisterReadOnly(nameof(SubTitle), typeof(string), typeof(PpsPaneHost), new FrameworkPropertyMetadata(null));
		public static readonly DependencyProperty SubTitleProperty = subTitlePropertyKey.DependencyProperty;

		/// <summary>Current subtitle of the pane.</summary>
		public string SubTitle => (string)GetValue(SubTitleProperty);

		#endregion

		#region -- Image - property ---------------------------------------------------

		private static readonly DependencyPropertyKey imagePropertyKey = DependencyProperty.RegisterReadOnly(nameof(Image), typeof(object), typeof(PpsPaneHost), new FrameworkPropertyMetadata(null));
		public static readonly DependencyProperty ImageProperty = imagePropertyKey.DependencyProperty;

		/// <summary>Image of the window pane.</summary>
		public object Image => GetValue(ImageProperty);

		#endregion

		#region -- Control - property -------------------------------------------------

		private static readonly DependencyPropertyKey controlPropertyKey = DependencyProperty.RegisterReadOnly(nameof(Control), typeof(UIElement), typeof(PpsPaneHost), new FrameworkPropertyMetadata(null));
		public static readonly DependencyProperty ControlProperty = controlPropertyKey.DependencyProperty;

		private void SetControl(object value)
			=> SetValue(controlPropertyKey, (UIElement)value);

		public UIElement Control => (UIElement)GetValue(ControlProperty);

		#endregion

		#region -- Pane - property ----------------------------------------------------

		private static readonly DependencyPropertyKey panePropertyKey = DependencyProperty.RegisterReadOnly(nameof(Pane), typeof(IPpsWindowPane), typeof(PpsPaneHost), new FrameworkPropertyMetadata(null));
		public static readonly DependencyProperty PaneProperty = panePropertyKey.DependencyProperty;
				
		public IPpsWindowPane Pane => (IPpsWindowPane)GetValue(PaneProperty);

		#endregion

		#region -- HasPaneSideBar - property ------------------------------------------

		private static readonly DependencyPropertyKey hasPaneSideBarPropertyKey = DependencyProperty.RegisterReadOnly(nameof(HasPaneSideBar), typeof(bool), typeof(PpsPaneHost), new FrameworkPropertyMetadata(BooleanBox.False));
		public static readonly DependencyProperty HasPaneSideBarProperty = hasPaneSideBarPropertyKey.DependencyProperty;

		/// <summary></summary>
		public bool HasPaneSideBar => BooleanBox.GetBool(GetValue(HasPaneSideBarProperty));

		#endregion

		#region -- Commands - property ------------------------------------------------

		private static readonly DependencyPropertyKey commandsPropertyKey = DependencyProperty.RegisterReadOnly(nameof(Commands), typeof(PpsUICommandCollection), typeof(PpsPaneHost), new FrameworkPropertyMetadata(null));
		public static readonly DependencyProperty CommandsProperty = commandsPropertyKey.DependencyProperty;

		/// <summary>Current commands of the pane</summary>
		public PpsUICommandCollection Commands => (PpsUICommandCollection)GetValue(CommandsProperty);

		#endregion

		#region -- HelpKey - property -------------------------------------------------

		private static readonly DependencyPropertyKey helpKeyPropertyKey = DependencyProperty.RegisterReadOnly(nameof(HelpKey), typeof(string), typeof(PpsPaneHost), new FrameworkPropertyMetadata(null));
		public static readonly DependencyProperty helpProperty = helpKeyPropertyKey.DependencyProperty;

		/// <summary>Current commands of the pane</summary>
		public string HelpKey => (string)GetValue(helpProperty);

		#endregion

		#region -- IsDirty - property -------------------------------------------------

		private static readonly DependencyPropertyKey isDirtyPropertyKey = DependencyProperty.RegisterReadOnly(nameof(IsDirty), typeof(bool), typeof(PpsPaneHost), new FrameworkPropertyMetadata(BooleanBox.False));
		public static readonly DependencyProperty IsDirtyProperty = isDirtyPropertyKey.DependencyProperty;

		/// <summary>Current commands of the pane</summary>
		public bool IsDirty => BooleanBox.GetBool(GetValue(IsDirtyProperty));

		#endregion

		#region -- ProgressStack - property -------------------------------------------

		/// <summary>Access the progress stack</summary>
		public PpsProgressStack ProgressStack => progressStack;

		IPpsProgressFactory IPpsWindowPaneHost.Progress => progressStack;
		
		#endregion

		/// <summary>Access the pane manager</summary>
		public IPpsWindowPaneManager PaneManager => paneManager;
	} // class PpsPaneHost
}

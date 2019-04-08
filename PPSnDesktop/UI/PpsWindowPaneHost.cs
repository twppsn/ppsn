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
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Neo.IronLua;
using TecWare.PPSn.Controls;

namespace TecWare.PPSn.UI
{
	#region -- enum PpsPaneHostState --------------------------------------------------

	/// <summary>Additional pane information.</summary>
	public enum PpsWindowPaneHostState
	{
		/// <summary>This pane host is marked as not closable.</summary>
		Fixed,
		/// <summary>This pane host is a normal pane-host.</summary>
		Root,
		/// <summary>A to a root pane related pane.</summary>
		Related
	} // enum PpsPaneHostState

	#endregion

	#region -- class PpsWindowPaneHost ------------------------------------------------

	/// <summary>Host for panes to support Progress, Load and CharmBar.</summary>
	[
	TemplatePart(Name = "PART_CharmBar", Type = typeof(PpsWindowPaneCharmBarControl)),
	TemplatePart(Name = "PART_CommandBar", Type = typeof(PpsCommandBar)),
	TemplatePart(Name = "PART_Control", Type = typeof(ContentPresenter))
	]
	internal class PpsWindowPaneHost : Control, IPpsWindowPaneHost
	{
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
		private static readonly DependencyPropertyKey currentPanePropertyKey = DependencyProperty.RegisterReadOnly(nameof(CurrentPane), typeof(IPpsWindowPane), typeof(PpsWindowPaneHost), new FrameworkPropertyMetadata(null));
		public static readonly DependencyProperty CurrentPaneProperty = currentPanePropertyKey.DependencyProperty;

		private static readonly DependencyPropertyKey paneProgressPropertyKey = DependencyProperty.RegisterReadOnly(nameof(PaneProgress), typeof(PpsProgressStack), typeof(PpsWindowPaneHost), new FrameworkPropertyMetadata(null));
		public static readonly DependencyProperty PaneProgressProperty = paneProgressPropertyKey.DependencyProperty;
		private static readonly DependencyPropertyKey hasPaneSideBarPropertyKey = DependencyProperty.RegisterReadOnly(nameof(HasPaneSideBar), typeof(bool), typeof(PpsWindowPaneHost), new FrameworkPropertyMetadata(BooleanBox.False));
		public static readonly DependencyProperty HasPaneSideBarProperty = hasPaneSideBarPropertyKey.DependencyProperty;

		private static readonly DependencyPropertyKey titlePropertyKey = DependencyProperty.RegisterReadOnly(nameof(Title), typeof(string), typeof(PpsWindowPaneHost), new FrameworkPropertyMetadata(null));
		public static readonly DependencyProperty TitleProperty = titlePropertyKey.DependencyProperty;
		private static readonly DependencyPropertyKey subTitlePropertyKey = DependencyProperty.RegisterReadOnly(nameof(SubTitle), typeof(string), typeof(PpsWindowPaneHost), new FrameworkPropertyMetadata(null));
		public static readonly DependencyProperty SubTitleProperty = subTitlePropertyKey.DependencyProperty;
		private static readonly DependencyPropertyKey imagePropertyKey = DependencyProperty.RegisterReadOnly(nameof(Image), typeof(object), typeof(PpsWindowPaneHost), new FrameworkPropertyMetadata(null));
		public static readonly DependencyProperty ImageProperty = imagePropertyKey.DependencyProperty;
		private static readonly DependencyPropertyKey commandsPropertyKey = DependencyProperty.RegisterReadOnly(nameof(Commands), typeof(PpsUICommandCollection), typeof(PpsWindowPaneHost), new FrameworkPropertyMetadata(null));
		public static readonly DependencyProperty CommandsProperty = commandsPropertyKey.DependencyProperty;
		private static readonly DependencyPropertyKey controlPropertyKey = DependencyProperty.RegisterReadOnly(nameof(Control), typeof(object), typeof(PpsWindowPaneHost), new FrameworkPropertyMetadata(null));
		public static readonly DependencyProperty ControlProperty = controlPropertyKey.DependencyProperty;
		private static readonly DependencyPropertyKey isFixedPropertyKey = DependencyProperty.RegisterReadOnly(nameof(IsFixed), typeof(bool), typeof(PpsWindowPaneHost), new FrameworkPropertyMetadata(BooleanBox.False));
		public static readonly DependencyProperty IsFixedProperty = isFixedPropertyKey.DependencyProperty;
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

		private PpsWindowPaneCharmBarControl charmBarControl = null;
		private PpsCommandBar commandBar = null;
		private ContentPresenter controlPresenter = null;

		private readonly PpsWindowPaneHostState paneState;

		public PpsWindowPaneHost()
			: this(PpsWindowPaneHostState.Root)
		{
		} // ctor

		public PpsWindowPaneHost(PpsWindowPaneHostState paneState)
		{
			this.paneState = paneState;

			SetValue(paneProgressPropertyKey, new PpsProgressStack(Dispatcher));
			SetValue(isFixedPropertyKey, paneState == PpsWindowPaneHostState.Fixed);
		} // ctor

		public override void OnApplyTemplate()
		{
			base.OnApplyTemplate();

			charmBarControl = (PpsWindowPaneCharmBarControl)GetTemplateChild("PART_CharmBar");
			controlPresenter = (ContentPresenter)GetTemplateChild("PART_Control");
			commandBar = (PpsCommandBar)GetTemplateChild("PART_CommandBar");

			var currentPane = CurrentPane;
			if (currentPane != null)
			{
				charmBarControl.CurrentData = currentPane.CurrentData;
				charmBarControl.HelpKey = currentPane.HelpKey;
			}

			CommandManager.AddExecutedHandler(this, PaneManager.Shell.DefaultExecutedHandler);
			CommandManager.AddCanExecuteHandler(this, PaneManager.Shell.DefaultCanExecuteHandler);

			UpdateFocus(false);
		} // proc OnApplyTemplate

		public void OnActivated()
		{
			Dispatcher.BeginInvoke(new Action<bool>(UpdateFocus), DispatcherPriority.ApplicationIdle, true);
		} // proc OnActivated
		
		public void OnDeactivated()
		{
		} // proc OnDeactivated

		public async Task<IPpsWindowPane> LoadAsync(IPpsWindowPaneManager paneManager, Type paneType, LuaTable arguments)
		{
			this.PaneManager = paneManager;

			var newPane = paneManager.CreateEmptyPane(this, paneType);
			try
			{
				using (var progress = this.DisableUI("Lade..."))
					await newPane.LoadAsync(arguments);

				return SetWindowPane(newPane);
			}
			catch
			{
				// clear new pane
				try { newPane.Dispose(); }
				catch { }

				throw;
			}
			finally
			{
				CommandManager.InvalidateRequerySuggested();
			}
		} // func LoadAsync

		private void UpdateFocus(bool setLogical)
		{
			if (setLogical) // activate the element within the parent focus scope
				FocusManager.SetFocusedElement(FocusManager.GetFocusScope(this), this);

			// if the element is focused
			if (controlPresenter != null && IsFocused && !controlPresenter.IsKeyboardFocusWithin)
			{
				var moveKeyboard = FocusManager.GetFocusedElement(controlPresenter);
				if (moveKeyboard == null)
					controlPresenter?.MoveFocus(new TraversalRequest(FocusNavigationDirection.First));
				else
					Keyboard.Focus(moveKeyboard);
			}
		} // proc UpdateFocus

		public async Task<bool> UnloadAsync(bool? commit)
		{
			var pane = CurrentPane;
			if (pane == null)
				return true;

			var r = await pane.UnloadAsync(commit);
			if (r)
				ClearWindowPane(pane);

			return r;
		} // func UnloadAsync

		private IPpsWindowPane SetWindowPane(IPpsWindowPane newPane)
		{
			Resources[PpsShellWpf.CurrentWindowPaneKey] = newPane;

			newPane.PropertyChanged += CurrentPanePropertyChanged;

			SetValue(titlePropertyKey, newPane.Title);
			SetValue(subTitlePropertyKey, newPane.SubTitle);
			SetValue(imagePropertyKey, newPane.Image);
			SetValue(commandsPropertyKey, newPane.Commands);
			SetValue(controlPropertyKey, newPane.Control);
			SetValue(hasPaneSideBarPropertyKey, newPane.HasSideBar);

			if (charmBarControl != null)
			{
				charmBarControl.CurrentData = newPane.CurrentData;
				charmBarControl.HelpKey = newPane.HelpKey;
			}

			SetValue(currentPanePropertyKey, newPane);

			// update focus, if it takes to long to load the pane
			UpdateFocus(false);

			return newPane;
		} // proc SetWindowPane

		private void ClearWindowPane(IPpsWindowPane pane)
		{			
			// remove object from memory
			pane.PropertyChanged -= CurrentPanePropertyChanged;
			pane.Dispose();

			SetValue(commandsPropertyKey, null);
			SetValue(hasPaneSideBarPropertyKey, false);
			SetValue(currentPanePropertyKey, null);

			// clear document
			charmBarControl.CurrentData = null;
			charmBarControl.HelpKey = null;

			Resources[PpsShellWpf.CurrentWindowPaneKey] = null;
		} // proc ClearWindowPane

		public void MoveWindowPane(IPpsWindowPaneManager targetPaneManager, PpsWindowPaneHost targetPaneHost)
		{
			var pane = CurrentPane;
			if (pane == null)
				return;

			// remove window from current host
			ClearWindowPane(pane);

			// copy window pane to new host
			targetPaneHost.PaneManager = targetPaneManager;
			targetPaneHost.SetWindowPane(pane);
		} // proc MoveWindowPane

		private void CurrentPanePropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			switch (e.PropertyName)
			{
				case nameof(IPpsWindowPane.Title):
					SetValue(titlePropertyKey, CurrentPane.Title);
					break;
				case nameof(IPpsWindowPane.SubTitle):
					SetValue(subTitlePropertyKey, CurrentPane.SubTitle);
					break;
				case nameof(IPpsWindowPane.Image):
					SetValue(imagePropertyKey, CurrentPane.Image);
					break;
				case nameof(IPpsWindowPane.Commands):
					SetValue(commandsPropertyKey, CurrentPane.Commands);
					break;
				case nameof(IPpsWindowPane.Control):
					SetValue(controlPropertyKey, CurrentPane.Control);
					break;
				case nameof(IPpsWindowPane.HasSideBar):
					SetValue(hasPaneSideBarPropertyKey, CurrentPane.HasSideBar);
					break;
				case nameof(IPpsWindowPane.HelpKey):
					charmBarControl.HelpKey = CurrentPane.HelpKey;
					break;
				case nameof(IPpsWindowPane.CurrentData):
					charmBarControl.CurrentData = CurrentPane.CurrentData;
					break;
			}
		} // proc CurrentPanePropertyChanged

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

		IPpsProgressFactory IPpsWindowPaneHost.Progress => PaneProgress;

		/// <summary>Current pane</summary>
		public IPpsWindowPane CurrentPane => (IPpsWindowPane)GetValue(CurrentPaneProperty);
		/// <summary></summary>
		public PpsProgressStack PaneProgress => (PpsProgressStack)GetValue(PaneProgressProperty);
		/// <summary></summary>
		public IPpsWindowPaneManager PaneManager { get; private set; }
		/// <summary></summary>
		public bool HasPaneSideBar => BooleanBox.GetBool(GetValue(HasPaneSideBarProperty));

		/// <summary>Is this pane closable</summary>
		public bool IsFixed => BooleanBox.GetBool(GetValue(IsFixedProperty));

		/// <summary>Current title of the pane.</summary>
		public string Title => (string)GetValue(TitleProperty);
		/// <summary>Current subtitle of the pane.</summary>
		public string SubTitle => (string)GetValue(SubTitleProperty);
		/// <summary>Image of the window pane.</summary>
		public object Image => GetValue(ImageProperty);
		/// <summary>Current commands of the pane</summary>
		public PpsUICommandCollection Commands => (PpsUICommandCollection)GetValue(CommandsProperty);
		/// <summary>Current control</summary>
		public object Control => GetValue(ControlProperty);

		/// <summary>Pane state classification.</summary>
		internal PpsWindowPaneHostState State => paneState;

		static PpsWindowPaneHost()
		{
			DefaultStyleKeyProperty.OverrideMetadata(typeof(PpsWindowPaneHost), new FrameworkPropertyMetadata(typeof(PpsWindowPaneHost)));
		} // ctor
	} // class PpsWindowPaneHost

	#endregion
}

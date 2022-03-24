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
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Neo.IronLua;
using TecWare.PPSn.Controls;
using TecWare.PPSn.UI;

namespace TecWare.PPSn.Main
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
	internal class PpsWindowPaneHost : PpsPaneHost
	{
		private readonly PpsWindowPaneHostState paneState;

		private PpsWindowPaneCharmBarControl charmBarControl = null;
		private ContentPresenter controlPresenter = null;

		#region -- Ctor/Dtor ----------------------------------------------------------

		public PpsWindowPaneHost()
			: this(PpsWindowPaneHostState.Root)
		{
		} // ctor

		public PpsWindowPaneHost(PpsWindowPaneHostState paneState)
		{
			this.paneState = paneState;

			SetValue(isFixedPropertyKey, paneState == PpsWindowPaneHostState.Fixed);
		} // ctor

		public override void OnApplyTemplate()
		{
			base.OnApplyTemplate();

			charmBarControl = (PpsWindowPaneCharmBarControl)GetTemplateChild("PART_CharmBar");
			controlPresenter = (ContentPresenter)GetTemplateChild("PART_Control");

			var currentPane = Pane;
			if (currentPane != null)
			{
				charmBarControl.CurrentData = currentPane.CurrentData;
				charmBarControl.HelpKey = currentPane.HelpKey;
			}

			UpdateFocus(false);
		} // proc OnApplyTemplate

		protected override async Task OnLoadPaneAsync(IPpsWindowPane pane, LuaTable arguments)
		{
			await base.OnLoadPaneAsync(pane, arguments);

			// update focus, if it takes to long to load the pane
			UpdateFocus(false);
		} // proc OnLoadPaneAsync

		public override Task<bool> ClosePaneAsync()
			=> Window.UnloadPaneHostAsync(this, null);

		#endregion

		#region -- Activate/Deactivate ------------------------------------------------

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

		public override void OnActivated()
		{
			base.OnActivated();
			Dispatcher.BeginInvoke(new Action<bool>(UpdateFocus), DispatcherPriority.ApplicationIdle, true);
		} // proc OnActivated

		#endregion

		#region -- IsFixed - property -------------------------------------------------

		private static readonly DependencyPropertyKey isFixedPropertyKey = DependencyProperty.RegisterReadOnly(nameof(IsFixed), typeof(bool), typeof(PpsWindowPaneHost), new FrameworkPropertyMetadata(BooleanBox.False));
		public static readonly DependencyProperty IsFixedProperty = isFixedPropertyKey.DependencyProperty;

		/// <summary>Is this pane closable</summary>
		public bool IsFixed => BooleanBox.GetBool(GetValue(IsFixedProperty));

		#endregion

		/// <summary>Access main window</summary>
		private PpsMainWindow Window => (PpsMainWindow)PaneManager;
		/// <summary>Pane state classification.</summary>
		internal PpsWindowPaneHostState State => paneState;

		static PpsWindowPaneHost()
		{
			DefaultStyleKeyProperty.OverrideMetadata(typeof(PpsWindowPaneHost), new FrameworkPropertyMetadata(typeof(PpsWindowPaneHost)));
		} // ctor
	} // class PpsWindowPaneHost

	#endregion
}

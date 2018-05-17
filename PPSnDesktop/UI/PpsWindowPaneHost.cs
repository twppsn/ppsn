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
using Neo.IronLua;

namespace TecWare.PPSn.UI
{
	#region -- class PpsWindowPaneHost ------------------------------------------------

	/// <summary>Host for panes to support Progress, Load and CharmBar.</summary>
	internal class PpsWindowPaneHost : Control, IPpsWindowPaneHost
	{
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
		private static readonly DependencyPropertyKey currentPanePropertyKey = DependencyProperty.RegisterReadOnly(nameof(CurrentPane), typeof(IPpsWindowPane), typeof(PpsWindowPaneHost), new FrameworkPropertyMetadata(null));
		public static readonly DependencyProperty CurrentPaneProperty = currentPanePropertyKey.DependencyProperty;

		private static readonly DependencyPropertyKey paneProgressPropertyKey = DependencyProperty.RegisterReadOnly(nameof(PaneProgress), typeof(PpsProgressStack), typeof(PpsWindowPaneHost), new FrameworkPropertyMetadata(null));
		public static readonly DependencyProperty PaneProgressProperty = paneProgressPropertyKey.DependencyProperty;
		private static readonly DependencyPropertyKey hasPaneSideBarPropertyKey = DependencyProperty.RegisterReadOnly(nameof(HasPaneSideBar), typeof(bool), typeof(PpsWindowPaneHost), new FrameworkPropertyMetadata(false));
		public static readonly DependencyProperty HasPaneSideBarProperty = hasPaneSideBarPropertyKey.DependencyProperty;

		private static readonly DependencyPropertyKey titlePropertyKey = DependencyProperty.RegisterReadOnly(nameof(Title), typeof(string), typeof(PpsWindowPaneHost), new FrameworkPropertyMetadata(null));
		public static readonly DependencyProperty TitleProperty = titlePropertyKey.DependencyProperty;
		private static readonly DependencyPropertyKey subTitlePropertyKey = DependencyProperty.RegisterReadOnly(nameof(SubTitle), typeof(string), typeof(PpsWindowPaneHost), new FrameworkPropertyMetadata(null));
		public static readonly DependencyProperty SubTitleProperty = subTitlePropertyKey.DependencyProperty;
		private static readonly DependencyPropertyKey commandsPropertyKey = DependencyProperty.RegisterReadOnly(nameof(Commands), typeof(PpsUICommandCollection), typeof(PpsWindowPaneHost), new FrameworkPropertyMetadata(null));
		public static readonly DependencyProperty CommandsProperty = commandsPropertyKey.DependencyProperty;
		private static readonly DependencyPropertyKey controlPropertyKey = DependencyProperty.RegisterReadOnly(nameof(Control), typeof(object), typeof(PpsWindowPaneHost), new FrameworkPropertyMetadata(null));
		public static readonly DependencyProperty ControlProperty = controlPropertyKey.DependencyProperty;
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

		public PpsWindowPaneHost()
		{
			SetValue(paneProgressPropertyKey, new PpsProgressStack(Dispatcher));
		} // ctor

		public async Task<IPpsWindowPane> LoadAsync(IPpsWindowPaneManager paneManager, Type paneType, LuaTable arguments)
		{
			this.PaneManager = paneManager;

			var newPane = paneManager.CreateEmptyPane(this, paneType);
			try
			{
				using (var progress = this.DisableUI("Lade..."))
					await newPane.LoadAsync(arguments);

				newPane.PropertyChanged += CurrentPanePropertyChanged;

				SetValue(titlePropertyKey, newPane.Title);
				SetValue(subTitlePropertyKey, newPane.SubTitle);
				SetValue(commandsPropertyKey, newPane.Commands);
				SetValue(controlPropertyKey, newPane.Control);

				SetValue(currentPanePropertyKey, newPane);

				return newPane;
			}
			catch
			{
				// clear new pane
				try { newPane.Dispose(); }
				catch { }

				throw;
			}
		} // func LoadAsync

		public async Task<bool> UnloadAsync(bool? commit)
		{
			var pane = CurrentPane;
			if (pane == null)
				return true;

			var r = await pane.UnloadAsync(commit);

			// remove object from memory
			pane.PropertyChanged -= CurrentPanePropertyChanged;
			pane.Dispose();

			SetValue(currentPanePropertyKey, null);

			return r;
		} // func UnloadAsync

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
				case nameof(IPpsWindowPane.Commands):
					SetValue(commandsPropertyKey, CurrentPane.Commands);
					break;
				case nameof(IPpsWindowPane.Control):
					SetValue(controlPropertyKey, CurrentPane.Control);
					break;
			}
		} // proc CurrentPanePropertyChanged

		IPpsProgressBar IPpsWindowPaneHost.Progress => PaneProgress;

		/// <summary>Current pane</summary>
		public IPpsWindowPane CurrentPane => (IPpsWindowPane)GetValue(CurrentPaneProperty);
		/// <summary></summary>
		public PpsProgressStack PaneProgress => (PpsProgressStack)GetValue(PaneProgressProperty);
		/// <summary></summary>
		public IPpsWindowPaneManager PaneManager { get; private set; }
		/// <summary></summary>
		public bool HasPaneSideBar { get => (bool)GetValue(HasPaneSideBarProperty); private set => SetValue(hasPaneSideBarPropertyKey, value); }

		/// <summary>Current title of the pane.</summary>
		public string Title => (string)GetValue(TitleProperty);
		/// <summary>Current subtitle of the pane.</summary>
		public string SubTitle => (string)GetValue(TitleProperty);
		/// <summary>Current commands of the pane</summary>
		public PpsUICommandCollection Commands => (PpsUICommandCollection)GetValue(TitleProperty);
		/// <summary>Current control</summary>
		public object Control => GetValue(ControlProperty);

		static PpsWindowPaneHost()
		{
			DefaultStyleKeyProperty.OverrideMetadata(typeof(PpsWindowPaneHost), new FrameworkPropertyMetadata(typeof(PpsWindowPaneHost)));
		} // ctor
	} // class PpsWindowPaneHost

	#endregion
}

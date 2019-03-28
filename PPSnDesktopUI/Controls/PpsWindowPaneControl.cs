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
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Neo.IronLua;
using TecWare.DE.Stuff;
using TecWare.PPSn.Data;
using TecWare.PPSn.UI;

namespace TecWare.PPSn.Controls
{
	/// <summary>Base window pane implementation for user-control based window panes.</summary>
	public class PpsWindowPaneControl : UserControl, IPpsWindowPane
	{
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
		public static readonly RoutedCommand CommitCommand = new RoutedCommand("Commit", typeof(PpsWindowPaneControl));
		public static readonly RoutedCommand PublishCommand = new RoutedCommand("Publish", typeof(PpsWindowPaneControl));
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

		#region -- Title - Property ---------------------------------------------------

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
		public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(nameof(Title), typeof(string), typeof(PpsWindowPaneControl), new FrameworkPropertyMetadata("UserControl", new PropertyChangedCallback(OnTitleChanged)));
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

		private static void OnTitleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
			=> ((PpsWindowPaneControl)d).NotifyWindowPanePropertyChanged(nameof(IPpsWindowPane.Title));

		/// <summary>Title of the window pane.</summary>
		public string Title { get => (string)GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

		#endregion

		#region -- SubTitle - Property ------------------------------------------------

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
		public static readonly DependencyProperty SubTitleProperty = DependencyProperty.Register(nameof(SubTitle), typeof(string), typeof(PpsWindowPaneControl), new FrameworkPropertyMetadata("SubTitle", new PropertyChangedCallback(OnSubTitleChanged)));
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

		private static void OnSubTitleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
			=> ((PpsWindowPaneControl)d).NotifyWindowPanePropertyChanged(nameof(IPpsWindowPane.SubTitle));

		/// <summary>SubTitle of the window pane.</summary>
		public string SubTitle { get => (string)GetValue(SubTitleProperty); set => SetValue(SubTitleProperty, value); }

		#endregion

		#region -- IsDirty - Property -------------------------------------------------

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
		public static readonly DependencyProperty IsDirtyProperty = DependencyProperty.Register(nameof(IsDirty), typeof(bool), typeof(PpsWindowPaneControl), new FrameworkPropertyMetadata(BooleanBox.False, new PropertyChangedCallback(OnIsDirtyChanged)));
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

		private static void OnIsDirtyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
			=> ((PpsWindowPaneControl)d).NotifyWindowPanePropertyChanged(nameof(IPpsWindowPane.IsDirty));

		/// <summary>IsDirty of the window pane.</summary>
		public bool IsDirty { get => BooleanBox.GetBool( GetValue(IsDirtyProperty)); set => SetValue(IsDirtyProperty, BooleanBox.GetObject( value)); }

		#endregion

		#region -- Image - Property ---------------------------------------------------

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
		public static readonly DependencyProperty ImageProperty = DependencyProperty.Register(nameof(Image), typeof(object), typeof(PpsWindowPaneControl), new FrameworkPropertyMetadata(null, new PropertyChangedCallback(OnImageChanged)));
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

		private static void OnImageChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
			=> ((PpsWindowPaneControl)d).NotifyWindowPanePropertyChanged(nameof(IPpsWindowPane.Image));

		/// <summary>Image of the window pane.</summary>
		public object Image { get => GetValue(ImageProperty); set => SetValue(ImageProperty, value); }

		#endregion

		#region -- Commands - Property ------------------------------------------------

		private static readonly DependencyPropertyKey commandsPropertyKey = DependencyProperty.RegisterReadOnly(nameof(Commands), typeof(PpsUICommandCollection), typeof(PpsPdfViewerPane), new FrameworkPropertyMetadata(null));
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
		public static readonly DependencyProperty CommandsProperty = commandsPropertyKey.DependencyProperty;
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

		/// <summary>Command bar</summary>
		public PpsUICommandCollection Commands => (PpsUICommandCollection)GetValue(CommandsProperty);

		#endregion

		private readonly IPpsWindowPaneHost paneHost;

		#region -- Ctor/Dtor ----------------------------------------------------------

		/// <summary>Default ctor</summary>
		[EditorBrowsable(EditorBrowsableState.Never), Obsolete("Do not use.")]
		public PpsWindowPaneControl()
		{
			SetValue(commandsPropertyKey, CreateCommandBar());
		} // ctor

		/// <summary></summary>
		/// <param name="paneHost"></param>
		public PpsWindowPaneControl(IPpsWindowPaneHost paneHost)
		{
			this.paneHost = paneHost ?? throw new ArgumentNullException(nameof(paneHost));

			SetValue(commandsPropertyKey, CreateCommandBar());

			Resources[PpsShellWpf.CurrentWindowPaneKey] = this;
		} // ctor

		private PpsUICommandCollection CreateCommandBar()
		{
			return new PpsUICommandCollection
			{
				AddLogicalChildHandler = AddLogicalChild,
				RemoveLogicalChildHandler = RemoveLogicalChild,
				DefaultCommandTarget = this
			};
		} // func CreateCommandBar

		/// <summary>Dispose content</summary>
		/// <param name="disposing"></param>
		protected virtual void Dispose(bool disposing)
		{
		} // proc Dispose

		void IDisposable.Dispose()
			=> Dispose(true);

		#endregion

		#region -- IPpsWindowPane implementation --------------------------------------

		private event PropertyChangedEventHandler PropertyChanged;
		event PropertyChangedEventHandler INotifyPropertyChanged.PropertyChanged { add => PropertyChanged += value; remove => PropertyChanged -= value; }

		/// <summary></summary>
		/// <param name="propertyName"></param>
		protected void NotifyWindowPanePropertyChanged(string propertyName)
			=> PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

		/// <summary>overwrite to compare pane arguments</summary>
		/// <param name="args"></param>
		/// <returns></returns>
		protected virtual PpsWindowPaneCompareResult CompareArguments(LuaTable args)
			=> PpsWindowPaneCompareResult.Same;

		PpsWindowPaneCompareResult IPpsWindowPane.CompareArguments(LuaTable args)
			=> CompareArguments(args);

		/// <summary>Overwrite to implement load.</summary>
		/// <param name="args"></param>
		/// <returns></returns>
		protected virtual Task OnLoadAsync(LuaTable args)
			=> Task.CompletedTask;

		Task IPpsWindowPane.LoadAsync(LuaTable args)
			=> OnLoadAsync(args);

		/// <summary>Override to unload data.</summary>
		/// <param name="commit"></param>
		/// <returns></returns>
		protected virtual Task<bool> OnUnloadAsync(bool? commit)
			=> Task.FromResult(true);

		Task<bool> IPpsWindowPane.UnloadAsync(bool? commit)
			=> OnUnloadAsync(commit);

		/// <summary>Define help key</summary>
		protected virtual string HelpKey => GetType().Name;

		string IPpsWindowPane.HelpKey => HelpKey;

		/// <summary>Define Sidebar mode</summary>
		protected virtual bool HasSideBar => false;
		
		bool IPpsWindowPane.HasSideBar => HasSideBar;
		
		object IPpsWindowPane.Control => this;

		/// <summary></summary>
		protected virtual IPpsDataInfo CurrentData => null;

		IPpsDataInfo IPpsWindowPane.CurrentData => CurrentData;

		/// <summary>Pane host</summary>
		public IPpsWindowPaneHost PaneHost => paneHost;

		#endregion
		
		/// <summary>Extent logical child collection with commands</summary>
		protected override IEnumerator LogicalChildren
			=> Procs.CombineEnumerator(base.LogicalChildren, Commands?.GetEnumerator());

		/// <summary></summary>
		public PpsShellWpf Shell => paneHost.PaneManager.Shell;
	} // class PpsWindowPaneControl
}
